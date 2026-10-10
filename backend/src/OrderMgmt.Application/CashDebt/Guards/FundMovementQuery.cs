using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Guards;

internal enum FundMovementKind
{
    Opening = 0,
    Receipt = 1,
    Payment = 2,
    TransferIn = 3,
    TransferOut = 4,
}

/// One movement of a fund (P17). Code, PartnerOrPerson and Description are what the cash book shows; the opening
/// amount has an empty code and no text. OtherFundId is the transfer's other fund.
internal sealed record FundMovement(
    Guid FundId,
    FundMovementKind Kind,
    DateTimeOffset At,
    Guid SourceId,
    string Code,
    decimal Delta,
    string? PartnerOrPerson,
    string? Description,
    Guid? OtherFundId)
{
    /// Inflows (and the opening amount) before outflows at the same instant.
    public int Order => Kind is FundMovementKind.Payment or FundMovementKind.TransferOut ? 1 : 0;
}

/// The single source of fund movements, shared by the negative-fund guard and the cash book: the fund's
/// OpeningAmount first, then Active non-deleted receipts (+Total) and payments (−Total) of the fund, automatic ones
/// included, and Active non-deleted fund transfers (−Amount on FromFundId, +Amount on ToFundId) (P17).
internal static class FundMovementQuery
{
    /// Movements of each fund, ordered by At, then inflows before outflows, then code. The opening amount precedes
    /// every document, whatever its date (P17, P29). Every requested fund must exist (soft-deleted ones included).
    public static async Task<IReadOnlyDictionary<Guid, List<FundMovement>>> LoadAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> fundIds, CancellationToken ct)
    {
        var ids = fundIds.Distinct().ToList();
        var funds = await db.Funds.IgnoreQueryFilters().AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, f.OpeningAmount })
            .ToListAsync(ct);
        if (funds.Count != ids.Count)
            throw new InvalidOperationException("Every fund of a movement query must exist.");

        // Reason and fund names are read even when the reason or fund was soft-deleted later. IgnoreQueryFilters on
        // those subqueries switches the filters off for the whole query, so the documents' own soft-delete filter is
        // applied by hand.
        var vouchers = await db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => !v.IsDeleted && ids.Contains(v.FundId) && v.Status == DocumentStatus.Active)
            .Select(v => new
            {
                v.Id,
                v.FundId,
                v.Type,
                v.VoucherAt,
                v.Code,
                v.Total,
                v.PartnerName,
                v.PersonName,
                v.Description,
                ReasonName = db.CashReasons.IgnoreQueryFilters()
                    .Where(r => r.Id == v.ReasonId).Select(r => r.Name).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var transfers = await db.FundTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(t => !t.IsDeleted && (ids.Contains(t.FromFundId) || ids.Contains(t.ToFundId))
                && t.Status == DocumentStatus.Active)
            .Select(t => new
            {
                t.Id,
                t.FromFundId,
                t.ToFundId,
                t.VoucherAt,
                t.Code,
                t.Amount,
                t.Description,
                FromCode = db.Funds.IgnoreQueryFilters()
                    .Where(f => f.Id == t.FromFundId).Select(f => f.Code).FirstOrDefault(),
                ToCode = db.Funds.IgnoreQueryFilters()
                    .Where(f => f.Id == t.ToFundId).Select(f => f.Code).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var movements = funds.ToDictionary(f => f.Id, f => new List<FundMovement>
        {
            new(f.Id, FundMovementKind.Opening, DateTimeOffset.MinValue, f.Id, string.Empty, f.OpeningAmount,
                null, null, null),
        });
        foreach (var v in vouchers)
        {
            var receipt = v.Type == CashVoucherType.Receipt;
            movements[v.FundId].Add(new FundMovement(v.FundId,
                receipt ? FundMovementKind.Receipt : FundMovementKind.Payment,
                v.VoucherAt, v.Id, v.Code, receipt ? v.Total : -v.Total,
                FirstText(v.PartnerName, v.PersonName), FirstText(v.Description, v.ReasonName), null));
        }
        // A transfer leaves its source fund and enters its destination fund (the two always differ).
        foreach (var t in transfers)
        {
            var description = FirstText(t.Description, $"Chuyển quỹ {t.FromCode} → {t.ToCode}");
            if (movements.TryGetValue(t.FromFundId, out var from))
                from.Add(new FundMovement(t.FromFundId, FundMovementKind.TransferOut, t.VoucherAt, t.Id, t.Code,
                    -t.Amount, null, description, t.ToFundId));
            if (movements.TryGetValue(t.ToFundId, out var to))
                to.Add(new FundMovement(t.ToFundId, FundMovementKind.TransferIn, t.VoucherAt, t.Id, t.Code,
                    t.Amount, null, description, t.FromFundId));
        }

        return movements.ToDictionary(
            m => m.Key,
            m => m.Value
                .OrderBy(x => x.At)
                .ThenBy(x => x.Order)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .ToList());
    }

    private static string? FirstText(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
