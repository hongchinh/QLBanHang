namespace OrderMgmt.Application.Common.Interfaces;

/// Runs inside the outermost transaction of ITransactionRunner.RunAsync, after the work and before the commit.
/// An exception rolls the transaction back.
/// A hook must save its own changes: the commit does not call SaveChanges, so unsaved changes are discarded.
/// Hooks run once, in registration order, so a hook must not create work for a hook that already ran.
public interface IBeforeCommitHook
{
    Task BeforeCommitAsync(CancellationToken ct);
}
