using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Guards;

public class CreditLimitGuard : ICreditLimitGuard
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IAppDbContext _db;
    private readonly IDebtQueryService _debtQuery;

    public CreditLimitGuard(IAppDbContext db, IDebtQueryService debtQuery)
    {
        _db = db;
        _debtQuery = debtQuery;
    }

    public Task<decimal> CaptureAsync(Guid branchId, Guid partnerId, CancellationToken ct = default)
        => _debtQuery.BalanceAsync(branchId, partnerId, DebtSide.Receivable, null, ct);

    public async Task CheckAsync(Guid branchId, Guid partnerId, decimal before, bool acknowledge,
        CancellationToken ct = default)
    {
        var limit = await _db.CreditLimits.AsNoTracking()
            .Where(l => l.BranchId == branchId && l.CustomerId == partnerId)
            .Select(l => (decimal?)l.Amount)
            .SingleOrDefaultAsync(ct);
        if (limit is null)
            return;

        var policy = await _db.CashDebtSettings.AsNoTracking()
            .Where(s => s.Id == 1)
            .Select(s => s.CreditLimitPolicy)
            .SingleAsync(ct);
        var after = await CaptureAsync(branchId, partnerId, ct);

        var violated = CreditLimitRule.IsViolation(limit, before, after);
        var outcome = LimitPolicyDecision.Decide(policy, violated, acknowledge);
        if (outcome == PolicyOutcome.Pass)
            return;

        var details = new Dictionary<string, string[]>
        {
            ["creditLimit"] = new[]
            {
                $"Hạn mức {limit.Value.ToString("N0", Vi)}, dư nợ sau khi lưu {after.ToString("N0", Vi)} " +
                $"(trước {before.ToString("N0", Vi)}).",
            },
        };
        throw outcome == PolicyOutcome.Warn
            ? new ConfirmationRequiredException("CREDIT_LIMIT_WARNING",
                "Dư nợ của khách vượt hạn mức. Xác nhận để tiếp tục lưu.", details, canConfirm: true)
            : new ConfirmationRequiredException("CREDIT_LIMIT_BLOCKED",
                "Dư nợ của khách vượt hạn mức, không thể lưu.", details, canConfirm: false);
    }
}
