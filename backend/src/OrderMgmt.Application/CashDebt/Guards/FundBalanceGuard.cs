using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.CashDebt.Guards;

public class FundBalanceGuard : IFundBalanceGuard
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IAppDbContext _db;

    public FundBalanceGuard(IAppDbContext db)
    {
        _db = db;
    }

    /// The same source as the cash book (FundMovementQuery), already in P17 order.
    public async Task<IReadOnlyList<BalanceMovement>> MovementsAsync(Guid fundId, CancellationToken ct = default)
    {
        var movements = await FundMovementQuery.LoadAsync(_db, new[] { fundId }, ct);
        return movements[fundId]
            .Select(m => new BalanceMovement(m.At, m.Order, m.Code, m.Delta))
            .ToList();
    }

    public async Task<FundCapture> CaptureAsync(IEnumerable<(Guid FundId, DateTimeOffset From)> funds,
        CancellationToken ct = default)
    {
        var froms = funds.GroupBy(f => f.FundId).Select(g => (FundId: g.Key, From: g.Min(f => f.From))).ToList();
        var results = await EvaluateAsync(froms, ct);
        return new FundCapture(froms.Select(f => new FundCaptureItem(f.FundId, f.From, results[f.FundId])).ToList());
    }

    public async Task CheckAsync(FundCapture capture, bool acknowledge, CancellationToken ct = default)
    {
        var after = await EvaluateAsync(capture.Items.Select(i => (i.FundId, i.From)).ToList(), ct);
        var worse = capture.Items
            .Where(i => RunningBalance.IsWorse(i.Before, after[i.FundId]))
            .Select(i => (i.FundId, After: after[i.FundId]))
            .ToList();
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

    /// Evaluates each fund from its own `from` on its window: the balance before `from` is summed in SQL and only the
    /// movements from `from` on are loaded, so the cost does not grow with the fund's history.
    private async Task<Dictionary<Guid, RunningBalanceResult>> EvaluateAsync(
        IReadOnlyCollection<(Guid FundId, DateTimeOffset From)> funds, CancellationToken ct)
    {
        if (funds.Count == 0)
            return new Dictionary<Guid, RunningBalanceResult>();
        var windows = await FundBalanceWindowQuery.LoadAsync(_db, funds, ct);
        return funds.ToDictionary(f => f.FundId,
            f => RunningBalance.Evaluate(windows[f.FundId].Start, windows[f.FundId].Movements, f.From));
    }

    /// "Âm 500 tại 05/10/2026 09:00" (VN time). A worse result is always negative, so FirstNegativeAt is set.
    private static string Describe(RunningBalanceResult after)
    {
        var at = after.FirstNegativeAt!.Value.ToOffset(VnTime.Offset);
        return $"Âm {(-after.Min).ToString("N0", Vi)} tại {at.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}";
    }
}
