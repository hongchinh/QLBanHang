namespace OrderMgmt.Application.Common.Interfaces;

/// Runs work in one database transaction; a nested call joins the outer transaction.
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
    Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);

    /// Read-only work in one REPEATABLE READ snapshot, so several queries see the same committed state.
    Task<T> RunSnapshotAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
