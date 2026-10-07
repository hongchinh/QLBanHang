using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Infrastructure.Persistence;

namespace OrderMgmt.Infrastructure.Inventory;

/// pg_advisory_xact_lock[_shared]: released when the transaction ends.
public sealed class PostgresInventoryLock : IInventoryLock
{
    private readonly AppDbContext _db;

    public PostgresInventoryLock(AppDbContext db) => _db = db;

    public Task AcquireBranchGateAsync(IEnumerable<Guid> branchIds, bool exclusive, CancellationToken ct = default) =>
        LockAsync(branchIds.Select(id => $"inv-branch:{id:N}"), shared: !exclusive, ct);

    public Task AcquireProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default) =>
        LockAsync(productIds.Select(id => $"inv-product:{id:N}"), shared: false, ct);

    public Task AcquireOpeningStockAsync(Guid warehouseId, CancellationToken ct = default) =>
        LockAsync(new[] { $"inv-opening:{warehouseId:N}" }, shared: false, ct);

    /// Takes every key in one round trip. PostgreSQL evaluates the volatile lock function after the sort, so keys are
    /// locked in ascending byte order (COLLATE "C") whatever the database collation: the same order for every caller.
    private async Task LockAsync(IEnumerable<string> keys, bool shared, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Inventory locks require an open transaction.");

        var distinct = keys.Distinct().ToArray();
        if (distinct.Length == 0)
            return;

        if (shared)
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock_shared(hashtextextended(k, 0)) FROM unnest({distinct}::text[]) AS k ORDER BY k COLLATE \"C\"", ct);
        else
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended(k, 0)) FROM unnest({distinct}::text[]) AS k ORDER BY k COLLATE \"C\"", ct);
    }
}
