using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Application.Organization.SampleData.Services;
using OrderMgmt.WebApi.BackgroundJobs;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

/// The worker without a host or a database: a runner that only ends when its token is cancelled, with a cancellation
/// or, as a disposed scope or a broken connection would, with another exception.
public class SampleDataWorkerShutdownTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Job_interrupted_by_shutdown_is_logged(bool disposedOnStop)
    {
        var runner = new BlockingRunner(disposedOnStop);
        var logger = new RecordingLogger();
        await using var services = new ServiceCollection()
            .AddScoped<ISampleDataRunner>(_ => runner)
            .BuildServiceProvider();
        var queue = new SampleDataQueue();
        var job = new SampleDataJob(Guid.NewGuid(), Guid.NewGuid());
        queue.Enqueue(job);
        var worker = new SampleDataHostedService(queue, services.GetRequiredService<IServiceScopeFactory>(), logger);

        await worker.StartAsync(CancellationToken.None);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        var entry = logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information).Subject;
        entry.Message.Should().Contain(job.BranchId.ToString()).And.Contain(job.RequestedByUserId.ToString());
        logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    private sealed class BlockingRunner : ISampleDataRunner
    {
        private readonly bool _disposedOnStop;

        public BlockingRunner(bool disposedOnStop) => _disposedOnStop = disposedOnStop;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RunJobAsync(SampleDataJob job, CancellationToken ct = default)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException) when (_disposedOnStop)
            {
                throw new ObjectDisposedException("NpgsqlConnection");
            }
        }

        public Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task RecoverInterruptedAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RecordingLogger : ILogger<SampleDataHostedService>
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
