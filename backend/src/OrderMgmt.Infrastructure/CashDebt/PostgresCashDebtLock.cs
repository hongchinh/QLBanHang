using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Infrastructure.Persistence;

namespace OrderMgmt.Infrastructure.CashDebt;

/// pg_advisory_xact_lock: released when the transaction ends.
public sealed class PostgresCashDebtLock : ICashDebtLock
{
    private readonly AppDbContext _db;

    public PostgresCashDebtLock(AppDbContext db) => _db = db;

    public async Task AcquireAsync(Guid branchId, IEnumerable<Guid> partnerIds, IEnumerable<Guid> fundIds, CancellationToken ct = default)
    {
        await AdvisoryLocks.AcquireAsync(_db, partnerIds.Select(id => $"debt-partner:{branchId:N}:{id:N}"), shared: false, ct);
        await AdvisoryLocks.AcquireAsync(_db, fundIds.Select(id => $"cash-fund:{id:N}"), shared: false, ct);
    }
}
