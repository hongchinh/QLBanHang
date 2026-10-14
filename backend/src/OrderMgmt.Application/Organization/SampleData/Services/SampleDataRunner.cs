using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;

namespace OrderMgmt.Application.Organization.SampleData.Services;

public sealed class SampleDataRunner : ISampleDataRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISampleDataGenerator _generator;
    private readonly IDateTime _clock;
    private readonly ILogger<SampleDataRunner> _logger;

    public SampleDataRunner(IServiceScopeFactory scopeFactory, ISampleDataGenerator generator, IDateTime clock, ILogger<SampleDataRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _generator = generator;
        _clock = clock;
        _logger = logger;
    }

    public Task RunJobAsync(SampleDataJob job, CancellationToken ct = default) => throw new NotImplementedException("Phase 02b");

    public Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default)
        => throw new NotImplementedException("Phase 02b");

    public Task RecoverInterruptedAsync(CancellationToken ct = default) => throw new NotImplementedException("Phase 02b");
}
