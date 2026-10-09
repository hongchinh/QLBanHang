namespace OrderMgmt.Application.CashDebt.Interfaces;

/// Transaction-scoped advisory locks for debt and cash writes (P14).
/// Global order: branch gate (IInventoryLock) → product keys → partner keys → fund keys → reason keys (cash or stock)
/// → counter row.
public interface ICashDebtLock
{
    /// Partner keys "debt-partner:{branchId:N}:{partnerId:N}", then fund keys "cash-fund:{fundId:N}".
    /// One statement per non-empty key set (P14). Throws InvalidOperationException without a transaction.
    Task AcquireAsync(Guid branchId, IEnumerable<Guid> partnerIds, IEnumerable<Guid> fundIds, CancellationToken ct = default);

    /// Reason key "cash-reason:{reasonId:N}": shared for a cash voucher save with that reason, exclusive for an edit or
    /// delete of the reason, so its in-use check (P19) sees every voucher saved with it. A reason edit takes no other
    /// key. Throws InvalidOperationException without a transaction.
    Task AcquireReasonAsync(Guid reasonId, bool exclusive, CancellationToken ct = default);

    /// Stock reason key "stock-reason:{reasonId:N}": shared for a stock voucher save with that reason, exclusive for an
    /// edit or delete of the reason, so its in-use check (P19) sees every stock voucher saved with it. A reason edit
    /// takes no other key. Throws InvalidOperationException without a transaction.
    Task AcquireStockReasonAsync(Guid reasonId, bool exclusive, CancellationToken ct = default);
}
