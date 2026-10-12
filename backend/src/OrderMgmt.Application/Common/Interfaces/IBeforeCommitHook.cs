namespace OrderMgmt.Application.Common.Interfaces;

/// Runs inside the outermost transaction of ITransactionRunner.RunAsync, after the work and before the commit.
/// An exception rolls the transaction back.
public interface IBeforeCommitHook
{
    Task BeforeCommitAsync(CancellationToken ct);
}
