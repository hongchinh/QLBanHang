namespace OrderMgmt.Application.CashDebt.Guards;

/// Credit-limit check of a save (C12, P10): capture the receivable balance before the save, check it after.
public interface ICreditLimitGuard
{
    /// Receivable balance of the customer in the branch, all dates (read after the partner lock).
    Task<decimal> CaptureAsync(Guid branchId, Guid partnerId, CancellationToken ct = default);

    /// Reads CreditLimit (branch, customer) and CashDebtSettings.CreditLimitPolicy; after = current balance.
    /// Warn → ConfirmationRequiredException("CREDIT_LIMIT_WARNING", canConfirm: true) unless acknowledged;
    /// Block → ("CREDIT_LIMIT_BLOCKED", canConfirm: false). Details key "creditLimit".
    Task CheckAsync(Guid branchId, Guid partnerId, decimal before, bool acknowledge, CancellationToken ct = default);
}
