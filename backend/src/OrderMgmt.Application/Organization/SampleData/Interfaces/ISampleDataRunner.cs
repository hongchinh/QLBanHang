using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Interfaces;

public interface ISampleDataRunner
{
    /// Owners, generate, then RunPlanAsync; never throws (except cancellation).
    Task RunJobAsync(SampleDataJob job, CancellationToken ct = default);

    /// Status/progress/error; never throws (except cancellation).
    Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default);

    /// Marks every Running branch Failed (S9); called once when the worker starts.
    Task RecoverInterruptedAsync(CancellationToken ct = default);
}
