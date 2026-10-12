using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Infrastructure.Persistence;

namespace OrderMgmt.Infrastructure.Inventory;

/// pg_advisory_xact_lock[_shared]: released when the transaction ends.
public sealed class PostgresInventoryLock : IInventoryLock
{
    private readonly AppDbContext _db;

    public PostgresInventoryLock(AppDbContext db) => _db = db;

    public Task AcquireBranchGateAsync(IEnumerable<Guid> branchIds, bool exclusive, CancellationToken ct = default) =>
        AdvisoryLocks.AcquireAsync(_db, branchIds.Select(id => $"inv-branch:{id:N}"), shared: !exclusive, ct);

    public Task AcquireProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default) =>
        AdvisoryLocks.AcquireAsync(_db, productIds.Select(id => $"inv-product:{id:N}"), shared: false, ct);

    public Task AcquireOpeningStockAsync(Guid warehouseId, CancellationToken ct = default) =>
        AdvisoryLocks.AcquireAsync(_db, new[] { $"inv-opening:{warehouseId:N}" }, shared: false, ct);
}
