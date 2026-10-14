namespace OrderMgmt.Application.Organization.SampleData.Models;

public sealed record SampleDataJob(Guid BranchId, Guid RequestedByUserId);

public sealed record SampleDataRunResult(bool Succeeded, int StepsExecuted, int StepsSkipped, TimeSpan Elapsed, string? Error);
