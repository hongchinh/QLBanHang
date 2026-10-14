using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Services;

/// Skipped: the amount the step computed at run time is 0 (S11), so no document was written.
internal enum SampleStepOutcome
{
    Executed,
    Skipped,
}

/// Runs one plan step inside its item scope, which already acts as the step owner in the branch (S7): one method per
/// step type. The runner counts the outcome.
internal sealed class SampleStepExecutor
{
    private readonly IServiceProvider _services;
    private readonly SampleRunState _run;

    public SampleStepExecutor(IServiceProvider services, SampleRunState run)
    {
        _services = services;
        _run = run;
    }

    public Task<SampleStepOutcome> ExecuteAsync(SampleStep step, CancellationToken ct) => step switch
    {
        // One arm per step type (stock voucher, cash voucher, fund transfer, debt offset, cancel).
        _ => throw new SampleDataPlanException($"Loại bước {step.GetType().Name} chưa được hỗ trợ."),
    };
}
