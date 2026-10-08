using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Guards;

public class FundBalanceGuard : IFundBalanceGuard
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IAppDbContext _db;

    public FundBalanceGuard(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BalanceMovement>> MovementsAsync(Guid fundId, CancellationToken ct = default)
    {
        var openingAmount = await _db.Funds.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.Id == fundId)
            .Select(f => f.OpeningAmount)
            .SingleAsync(ct);
        var vouchers = await _db.CashVouchers.AsNoTracking()
            .Where(v => v.FundId == fundId && v.Status == DocumentStatus.Active)
            .Select(v => new { v.VoucherAt, v.Type, v.Code, v.Total })
            .ToListAsync(ct);
        var transfers = await _db.FundTransfers.AsNoTracking()
            .Where(t => (t.FromFundId == fundId || t.ToFundId == fundId) && t.Status == DocumentStatus.Active)
            .Select(t => new { t.VoucherAt, t.FromFundId, t.Code, t.Amount })
            .ToListAsync(ct);

        // The opening amount precedes every document, whatever its date (P17, P29).
        var movements = new List<BalanceMovement> { new(DateTimeOffset.MinValue, 0, string.Empty, openingAmount) };
        movements.AddRange(vouchers.Select(v => v.Type == CashVoucherType.Receipt
            ? new BalanceMovement(v.VoucherAt, 0, v.Code, v.Total)
            : new BalanceMovement(v.VoucherAt, 1, v.Code, -v.Total)));
        // A transfer leaves its source fund and enters its destination fund (the two always differ).
        movements.AddRange(transfers.Select(t => t.FromFundId == fundId
            ? new BalanceMovement(t.VoucherAt, 1, t.Code, -t.Amount)
            : new BalanceMovement(t.VoucherAt, 0, t.Code, t.Amount)));
        return movements
            .OrderBy(m => m.At)
            .ThenBy(m => m.Order)
            .ThenBy(m => m.Code, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<FundCapture> CaptureAsync(IEnumerable<(Guid FundId, DateTimeOffset From)> funds,
        CancellationToken ct = default)
    {
        var items = new List<FundCaptureItem>();
        foreach (var group in funds.GroupBy(f => f.FundId))
        {
            var from = group.Min(f => f.From);
            items.Add(new FundCaptureItem(group.Key, from, await EvaluateAsync(group.Key, from, ct)));
        }
        return new FundCapture(items);
    }

    public async Task CheckAsync(FundCapture capture, bool acknowledge, CancellationToken ct = default)
    {
        var worse = new List<(Guid FundId, RunningBalanceResult After)>();
        foreach (var item in capture.Items)
        {
            var after = await EvaluateAsync(item.FundId, item.From, ct);
            if (RunningBalance.IsWorse(item.Before, after))
                worse.Add((item.FundId, after));
        }
        if (worse.Count == 0)
            return;

        var policy = await _db.CashDebtSettings.AsNoTracking()
            .Where(s => s.Id == 1)
            .Select(s => s.NegativeFundPolicy)
            .SingleAsync(ct);
        var outcome = LimitPolicyDecision.Decide(policy, violated: true, acknowledge);
        if (outcome == PolicyOutcome.Pass)
            return;

        var fundIds = worse.Select(w => w.FundId).ToList();
        var codes = await _db.Funds.IgnoreQueryFilters().AsNoTracking()
            .Where(f => fundIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Code, ct);
        var details = worse.ToDictionary(
            w => codes[w.FundId],
            w => new[] { Describe(w.After) });

        throw outcome == PolicyOutcome.Warn
            ? new ConfirmationRequiredException("NEGATIVE_FUND_WARNING", "Quỹ sẽ bị âm.", details, canConfirm: true)
            : new ConfirmationRequiredException("NEGATIVE_FUND_BLOCKED", "Không đủ tiền trong quỹ.", details, canConfirm: false);
    }

    private async Task<RunningBalanceResult> EvaluateAsync(Guid fundId, DateTimeOffset from, CancellationToken ct)
        => RunningBalance.Evaluate(0m, await MovementsAsync(fundId, ct), from);

    /// "Âm 500 tại 05/10/2026 09:00" (VN time). A worse result is always negative, so FirstNegativeAt is set.
    private static string Describe(RunningBalanceResult after)
    {
        var at = after.FirstNegativeAt!.Value.ToOffset(VnTime.Offset);
        return $"Âm {(-after.Min).ToString("N0", Vi)} tại {at.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}";
    }
}
