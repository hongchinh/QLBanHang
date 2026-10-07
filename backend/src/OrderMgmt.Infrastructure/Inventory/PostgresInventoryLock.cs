using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Infrastructure.Persistence;

namespace OrderMgmt.Infrastructure.Inventory;

/// pg_advisory_xact_lock[_shared]: released when the transaction ends.
public sealed class PostgresInventoryLock : IInventoryLock
{
    private readonly AppDbContext _db;

    public PostgresInventoryLock(AppDbContext db) => _db = db;

    public async Task AcquireBranchGateAsync(IEnumerable<Guid> branchIds, bool exclusive, CancellationToken ct = default)
    {
        EnsureTransaction();
        foreach (var branchId in branchIds.Distinct().OrderBy(id => id))
        {
            var key = $"inv-branch:{branchId:N}";
            if (exclusive)
                await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            else
                await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared(hashtextextended({key}, 0))", ct);
        }
    }

    public async Task AcquireAsync(IEnumerable<(Guid ProductId, Guid BranchId)> keys, CancellationToken ct = default)
    {
        EnsureTransaction();
        foreach (var (productId, branchId) in keys.Distinct().OrderBy(k => k.ProductId).ThenBy(k => k.BranchId))
        {
            var key = $"inv:{productId:N}:{branchId:N}";
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        }
    }

    public async Task AcquireProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        EnsureTransaction();
        foreach (var productId in productIds.Distinct().OrderBy(id => id))
        {
            var key = $"inv-product:{productId:N}";
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        }
    }

    private void EnsureTransaction()
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Inventory locks require an open transaction.");
    }
}
