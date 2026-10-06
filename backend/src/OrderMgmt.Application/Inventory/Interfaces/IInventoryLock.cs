namespace OrderMgmt.Application.Inventory.Interfaces;

/// Transaction-scoped advisory locks. Lock order everywhere (D30): branch gate → product keys → document counter row.
public interface IInventoryLock
{
    // D30 branch costing gate, taken FIRST. Shared for voucher posting / opening stock;
    // exclusive (ascending branch id) for settings-change and manual recalculation.
    // Key: hashtextextended('inv-branch:{branchId:N}', 0). Throws InvalidOperationException without a transaction.
    Task AcquireBranchGateAsync(IEnumerable<Guid> branchIds, bool exclusive, CancellationToken ct = default);

    // pg_advisory_xact_lock per distinct (ProductId, BranchId), ascending by ProductId then BranchId.
    // Taken after the branch gate. Throws InvalidOperationException when no transaction is open.
    Task AcquireAsync(IEnumerable<(Guid ProductId, Guid BranchId)> keys, CancellationToken ct = default);
}
