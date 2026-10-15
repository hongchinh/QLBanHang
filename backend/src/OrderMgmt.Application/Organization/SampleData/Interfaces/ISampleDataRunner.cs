using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Interfaces;

/// Both run methods fill a demo branch that is Running and never write to any other branch. While `ct` is not
/// cancelled they record every failure on that branch while it is still Running (Failed, "{key}: {message}") and do
/// not throw. Once `ct` is cancelled any exception propagates (OperationCanceledException, or what a stopping host
/// causes, such as ObjectDisposedException or Npgsql errors) and the branch stays Running for RecoverInterruptedAsync
/// at the next start; `ct` must be the host's stopping token.
public interface ISampleDataRunner
{
    /// Demo branches in Running only: for a branch that is missing, real or no longer Running nothing is written and no
    /// plan is made. The owners are resolved once and the generator gets their count.
    Task RunJobAsync(SampleDataJob job, CancellationToken ct = default);

    /// Resolves the owners itself.
    Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default);

    /// Marks every Running branch Failed (S9); called once when the worker starts.
    Task RecoverInterruptedAsync(CancellationToken ct = default);
}
