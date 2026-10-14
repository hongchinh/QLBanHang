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

/// The worker without a host or a database, around a fake runner. SampleDataHostedServiceTests runs it end to end.
public class SampleDataHostedServiceUnitTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static SampleDataJob Job() => new(Guid.NewGuid(), Guid.NewGuid());

    private static SampleDataHostedService Worker(ISampleDataRunner runner, SampleDataQueue queue, RecordingLogger logger)
    {
        var services = new ServiceCollection().AddScoped(_ => runner).BuildServiceProvider();
        return new SampleDataHostedService(queue, services.GetRequiredService<IServiceScopeFactory>(), logger);
    }

    [Fact]
    public async Task Failing_job_is_logged_and_the_next_jobs_run()
    {
        var (job1, job2, job3) = (Job(), Job(), Job());
        var failure = new InvalidOperationException("boom");
        var ran = new ConcurrentQueue<SampleDataJob>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner
        {
            Run = (job, _) =>
            {
                if (job == job1)
                    return Task.FromException(failure);
                ran.Enqueue(job);
                if (ran.Count == 2)
                    done.TrySetResult();
                return Task.CompletedTask;
            },
        };
        var queue = new SampleDataQueue();
        foreach (var job in new[] { job1, job2, job3 })
            queue.Enqueue(job);
        var logger = new RecordingLogger();
        using var worker = Worker(runner, queue, logger);

        await worker.StartAsync(CancellationToken.None);
        await done.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Wait);

        ran.Should().Equal(job2, job3);
        var error = logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error).Subject;
        error.Exception.Should().BeSameAs(failure);
        error.Message.Should().Contain(job1.BranchId.ToString());
        // The runner's own scope, so the worker's lines and the runner's lines of one job share their correlation id.
        error.Scope.Should().Contain("CorrelationId", $"sample-data-{job1.BranchId:N}")
            .And.Contain("UserId", job1.RequestedByUserId);
    }

    [Fact]
    public async Task Jobs_run_one_after_another()
    {
        var (job1, job2, job3) = (Job(), Job(), Job());
        var started = new ConcurrentQueue<SampleDataJob>();
        var active = 0;
        var maxActive = 0;
        var finished = 0;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner
        {
            Run = async (job, ct) =>
            {
                started.Enqueue(job);
                var now = Interlocked.Increment(ref active);
                lock (started)
                    maxActive = Math.Max(maxActive, now);
                await Task.Delay(50, ct);
                Interlocked.Decrement(ref active);
                if (Interlocked.Increment(ref finished) == 3)
                    done.TrySetResult();
            },
        };
        var queue = new SampleDataQueue();
        foreach (var job in new[] { job1, job2, job3 })
            queue.Enqueue(job);
        using var worker = Worker(runner, queue, new RecordingLogger());

        await worker.StartAsync(CancellationToken.None);
        await done.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Wait);

        maxActive.Should().Be(1);
        started.Should().Equal(job1, job2, job3);
    }

    /// Recovery is awaited in StartAsync, so the host does not go on (to the web server) and no job runs before it ends.
    [Fact]
    public async Task Recovery_ends_before_start_completes_and_before_any_job()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ran = new TaskCompletionSource<SampleDataJob>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner
        {
            Recover = _ => gate.Task,
            Run = (job, _) =>
            {
                ran.TrySetResult(job);
                return Task.CompletedTask;
            },
        };
        var queue = new SampleDataQueue();
        var job = Job();
        queue.Enqueue(job);
        using var worker = Worker(runner, queue, new RecordingLogger());

        var start = worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);

        start.IsCompleted.Should().BeFalse("StartAsync waits for the recovery");
        ran.Task.IsCompleted.Should().BeFalse("no job runs before the recovery ends");

        gate.SetResult();
        await start.WaitAsync(Wait);
        (await ran.Task.WaitAsync(Wait)).Should().Be(job);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Wait);
    }

    /// The runner ends with a cancellation or, as a disposed scope or a broken connection would, with another exception:
    /// either way one Information line, which carries the exception only when it is not the cancellation.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Job_interrupted_by_shutdown_is_logged(bool disposedOnStop)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner
        {
            Run = async (_, ct) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException) when (disposedOnStop)
                {
                    throw new ObjectDisposedException("NpgsqlConnection");
                }
            },
        };
        var queue = new SampleDataQueue();
        var job = Job();
        queue.Enqueue(job);
        var logger = new RecordingLogger();
        using var worker = Worker(runner, queue, logger);

        await worker.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Wait);

        var entry = logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information).Subject;
        entry.Message.Should().Contain(job.BranchId.ToString()).And.Contain(job.RequestedByUserId.ToString());
        if (disposedOnStop)
            entry.Exception.Should().BeOfType<ObjectDisposedException>();
        else
            entry.Exception.Should().BeNull();
        logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    /// ReadAllAsync hands out a queued job without looking at the token: once stopping, the next job is left alone
    /// rather than started with a cancelled token and logged as interrupted.
    [Fact]
    public async Task Queued_job_is_left_alone_once_stopping()
    {
        var (job1, job2) = (Job(), Job());
        var ran = new ConcurrentQueue<SampleDataJob>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner
        {
            Run = async (job, ct) =>
            {
                ran.Enqueue(job);
                if (job != job1)
                    return;
                started.TrySetResult();
                // Ends normally once stopping, as a job that finishes just then.
                await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { }, TaskScheduler.Default);
            },
        };
        var queue = new SampleDataQueue();
        queue.Enqueue(job1);
        queue.Enqueue(job2);
        var logger = new RecordingLogger();
        using var worker = Worker(runner, queue, logger);

        await worker.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(Wait);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Wait);

        ran.Should().Equal(job1);
        logger.Entries.Should().BeEmpty();
    }

    private sealed class FakeRunner : ISampleDataRunner
    {
        public Func<SampleDataJob, CancellationToken, Task> Run { get; init; } = (_, _) => Task.CompletedTask;

        public Func<CancellationToken, Task> Recover { get; init; } = _ => Task.CompletedTask;

        public Task RunJobAsync(SampleDataJob job, CancellationToken ct = default) => Run(job, ct);

        public Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task RecoverInterruptedAsync(CancellationToken ct = default) => Recover(ct);
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception,
        IReadOnlyDictionary<string, object?> Scope);

    /// Keeps every entry with the key/value pairs of the scopes open when it was written.
    private sealed class RecordingLogger : ILogger<SampleDataHostedService>
    {
        private readonly AsyncLocal<Scope?> _current = new();

        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var scope = new Scope(this, state, _current.Value);
            _current.Value = scope;
            return scope;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = new Dictionary<string, object?>();
            for (var scope = _current.Value; scope is not null; scope = scope.Parent)
                if (scope.State is IEnumerable<KeyValuePair<string, object?>> pairs)
                    foreach (var (key, value) in pairs)
                        values.TryAdd(key, value);
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception, values));
        }

        private sealed class Scope : IDisposable
        {
            private readonly RecordingLogger _logger;

            public Scope(RecordingLogger logger, object state, Scope? parent)
            {
                _logger = logger;
                State = state;
                Parent = parent;
            }

            public object State { get; }

            public Scope? Parent { get; }

            public void Dispose() => _logger._current.Value = Parent;
        }
    }
}
