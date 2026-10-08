namespace OrderMgmt.Application.CashDebt.Interfaces;

/// Transaction-scoped advisory locks for debt and cash writes (P14).
/// Global order: branch gate (IInventoryLock) → product keys → partner keys → fund keys → counter row.
public interface ICashDebtLock
{
    /// Partner keys "debt-partner:{branchId:N}:{partnerId:N}", then fund keys "cash-fund:{fundId:N}".
    /// One statement per non-empty key set (P14). Throws InvalidOperationException without a transaction.
    Task AcquireAsync(Guid branchId, IEnumerable<Guid> partnerIds, IEnumerable<Guid> fundIds, CancellationToken ct = default);
}
