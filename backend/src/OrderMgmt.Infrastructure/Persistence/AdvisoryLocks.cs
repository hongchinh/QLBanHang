using Microsoft.EntityFrameworkCore;

namespace OrderMgmt.Infrastructure.Persistence;

/// pg_advisory_xact_lock[_shared]: released when the transaction ends.
internal static class AdvisoryLocks
{
    /// Takes every key in one round trip. PostgreSQL evaluates the volatile lock function after the sort, so keys are
    /// locked in ascending byte order (COLLATE "C") whatever the database collation: the same order for every caller.
    /// Throws InvalidOperationException without a transaction, even for an empty key set.
    public static async Task AcquireAsync(DbContext db, IEnumerable<string> keys, bool shared, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Advisory locks require an open transaction.");

        var distinct = keys.Distinct().ToArray();
        if (distinct.Length == 0)
            return;

        if (shared)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock_shared(hashtextextended(k, 0)) FROM unnest({distinct}::text[]) AS k ORDER BY k COLLATE \"C\"", ct);
        else
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended(k, 0)) FROM unnest({distinct}::text[]) AS k ORDER BY k COLLATE \"C\"", ct);
    }
}
