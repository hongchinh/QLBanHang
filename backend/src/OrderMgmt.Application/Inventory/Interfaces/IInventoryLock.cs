namespace OrderMgmt.Application.Inventory.Interfaces;

/// Transaction-scoped advisory locks. Lock order everywhere (D30): branch gate → product keys → document counter row.
/// A set of keys is taken in one round trip, ordered by the key string (ordinal), so every caller uses the same order.
public interface IInventoryLock
{
    // D30 branch costing gate, taken FIRST. Shared for voucher posting / opening stock;
    // exclusive (ascending key order) for settings-change and manual recalculation.
    // Key: hashtextextended('inv-branch:{branchId:N}', 0). Throws InvalidOperationException without a transaction.
    Task AcquireBranchGateAsync(IEnumerable<Guid> branchIds, bool exclusive, CancellationToken ct = default);

    // One key per product, valid for every branch, taken after the branch gate. Serializes postings of a product and
    // writes that span branches: the D35 cost price and catalog changes guarded by inventory activity.
    // One lock per product keeps large grids within max_locks_per_transaction (review finding).
    // Key: hashtextextended('inv-product:{productId:N}', 0). Throws InvalidOperationException without a transaction.
    Task AcquireProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default);

    // Serializes opening-stock saves of one warehouse: they all replace the same Opening ledger source.
    // Taken after the shared branch gate and before the product keys.
    // Key: hashtextextended('inv-opening:{warehouseId:N}', 0). Throws InvalidOperationException without a transaction.
    Task AcquireOpeningStockAsync(Guid warehouseId, CancellationToken ct = default);
}
