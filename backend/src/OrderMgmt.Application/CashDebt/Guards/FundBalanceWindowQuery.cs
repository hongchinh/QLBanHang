using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Guards;

/// What the negative-fund guard needs of a fund (P17, D31): the balance strictly before `from`, and the light
/// movements from `from` on. Same filters as FundMovementQuery (Active, non-deleted; receipts +Total, payments −Total,
/// transfers −Amount on FromFundId and +Amount on ToFundId), but the history before `from` is summed in SQL instead of
/// loaded, and no names are read. RunningBalance.Evaluate(Start, Movements, from) equals evaluating the whole history.
internal sealed record FundBalanceWindow(decimal Start, IReadOnlyList<BalanceMovement> Movements);

internal static class FundBalanceWindowQuery
{
    /// One window per distinct fund. Every requested fund must exist (soft-deleted ones included). A document at
    /// exactly `from` is part of the movements, not of Start (RunningBalance: Start = opening + Σ deltas with At &lt; from).
    public static async Task<IReadOnlyDictionary<Guid, FundBalanceWindow>> LoadAsync(
        IAppDbContext db, IReadOnlyCollection<(Guid FundId, DateTimeOffset From)> funds, CancellationToken ct)
    {
        var fromOf = funds.GroupBy(f => f.FundId).ToDictionary(g => g.Key, g => g.Min(f => f.From));
        var ids = fromOf.Keys.ToList();
        // One shared lower bound for the batched queries; a fund with a later `from` applies it in memory.
        var floor = fromOf.Values.Min();

        var openings = await db.Funds.IgnoreQueryFilters().AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, f.OpeningAmount })
            .ToDictionaryAsync(f => f.Id, f => f.OpeningAmount, ct);
        if (openings.Count != ids.Count)
            throw new InvalidOperationException("Every fund of a movement query must exist.");

        var start = ids.ToDictionary(id => id, id => openings[id]);

        // Σ of everything strictly before the floor, per fund.
        var voucherSums = await db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => !v.IsDeleted && ids.Contains(v.FundId) && v.Status == DocumentStatus.Active
                && v.VoucherAt < floor)
            .GroupBy(v => new { v.FundId, v.Type })
            .Select(g => new { g.Key.FundId, g.Key.Type, Sum = g.Sum(v => v.Total) })
            .ToListAsync(ct);
        foreach (var s in voucherSums)
            start[s.FundId] += s.Type == CashVoucherType.Receipt ? s.Sum : -s.Sum;

        var outSums = await db.FundTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(t => !t.IsDeleted && ids.Contains(t.FromFundId) && t.Status == DocumentStatus.Active
                && t.VoucherAt < floor)
            .GroupBy(t => t.FromFundId)
            .Select(g => new { FundId = g.Key, Sum = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        foreach (var s in outSums)
            start[s.FundId] -= s.Sum;
        var inSums = await db.FundTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(t => !t.IsDeleted && ids.Contains(t.ToFundId) && t.Status == DocumentStatus.Active
                && t.VoucherAt < floor)
            .GroupBy(t => t.ToFundId)
            .Select(g => new { FundId = g.Key, Sum = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        foreach (var s in inSums)
            start[s.FundId] += s.Sum;

        // Light rows from the floor on (no reason or fund names).
        var vouchers = await db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => !v.IsDeleted && ids.Contains(v.FundId) && v.Status == DocumentStatus.Active
                && v.VoucherAt >= floor)
            .Select(v => new { v.FundId, v.Type, v.VoucherAt, v.Code, v.Total })
            .ToListAsync(ct);
        var transfers = await db.FundTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(t => !t.IsDeleted && (ids.Contains(t.FromFundId) || ids.Contains(t.ToFundId))
                && t.Status == DocumentStatus.Active && t.VoucherAt >= floor)
            .Select(t => new { t.FromFundId, t.ToFundId, t.VoucherAt, t.Code, t.Amount })
            .ToListAsync(ct);

        var rows = ids.ToDictionary(id => id, _ => new List<BalanceMovement>());
        foreach (var v in vouchers)
        {
            var receipt = v.Type == CashVoucherType.Receipt;
            rows[v.FundId].Add(new BalanceMovement(v.VoucherAt, receipt ? 0 : 1, v.Code, receipt ? v.Total : -v.Total));
        }
        // A transfer leaves its source fund and enters its destination fund (the two always differ).
        foreach (var t in transfers)
        {
            if (rows.TryGetValue(t.FromFundId, out var from))
                from.Add(new BalanceMovement(t.VoucherAt, 1, t.Code, -t.Amount));
            if (rows.TryGetValue(t.ToFundId, out var to))
                to.Add(new BalanceMovement(t.VoucherAt, 0, t.Code, t.Amount));
        }

        // A fund whose own `from` is later than the floor folds the rows in [floor, from) into its start balance.
        return ids.ToDictionary(id => id, id =>
        {
            var from = fromOf[id];
            var earlier = rows[id].Where(m => m.At < from).Sum(m => m.Delta);
            return new FundBalanceWindow(start[id] + earlier, rows[id].Where(m => m.At >= from).ToList());
        });
    }
}
