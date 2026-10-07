using System.Data;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;

namespace OrderMgmt.Infrastructure.Persistence;

public sealed class EfTransactionRunner : ITransactionRunner
{
    private readonly AppDbContext _db;

    public EfTransactionRunner(AppDbContext db) => _db = db;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is not null)
            return await work(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var result = await work(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // A failed rollback (e.g. a broken connection) must not hide the original exception;
                // PostgreSQL drops the transaction with the connection anyway.
            }
            // Tracked entities still hold the rolled-back state.
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<T> RunSnapshotAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        if (_db.Database.CurrentTransaction is not null)
            return await work(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var result = await work(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            await work(c);
            return true;
        }, ct);
}
