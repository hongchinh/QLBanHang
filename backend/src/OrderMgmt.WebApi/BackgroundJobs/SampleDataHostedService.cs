using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Services;

namespace OrderMgmt.WebApi.BackgroundJobs;

/// The single worker of the sample data queue: the jobs of new demo branches run one after another, each in a fresh
/// scope.
public sealed class SampleDataHostedService : BackgroundService
{
    private readonly ISampleDataQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SampleDataHostedService> _logger;

    public SampleDataHostedService(ISampleDataQueue queue, IServiceScopeFactory scopeFactory,
        ILogger<SampleDataHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Awaited before the server takes requests, so recovery can never fail a demo branch this process has just
        // created and queued. This rests on two framework defaults: hosted services start one after another
        // (HostOptions.ServicesStartConcurrently = false), and WebApplicationBuilder registers the web server's hosted
        // service last. Enabling concurrent start would let requests in before recovery ends.
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>().RecoverInterruptedAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The test template host has no tables yet.
            _logger.LogWarning(ex, "Sample data recovery skipped");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            // ReadAllAsync hands out a queued job without looking at the token: once stopping, leave it alone.
            if (stoppingToken.IsCancellationRequested)
                break;
            // The runner's scope, so the lines of the worker and of the runner for one job share their correlation id.
            using var logScope = _logger.BeginScope(SampleDataRunner.LogScope(job));
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISampleDataRunner>().RunJobAsync(job, stoppingToken);
            }
            catch (Exception ex) when (stoppingToken.IsCancellationRequested)
            {
                // Stopping: the job ends with a cancellation, or with whatever failed as the host went away (a disposed
                // scope, a broken connection). The runner logs nothing for it, and the branch stays Running until the
                // next start recovers it.
                _logger.LogInformation(ex is OperationCanceledException ? null : ex,
                    "Sample data job of branch {BranchId} requested by {UserId} interrupted by shutdown",
                    job.BranchId, job.RequestedByUserId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sample data job of branch {BranchId} requested by {UserId} failed",
                    job.BranchId, job.RequestedByUserId);
            }
        }
    }
}
