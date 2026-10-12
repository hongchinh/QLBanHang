using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class TransactionHookTests : CashDebtTestBase
{
    public TransactionHookTests(PostgresFixture pg) : base(pg) { }

    private sealed class CountingHook(AppDbContext db) : IBeforeCommitHook
    {
        public int Calls { get; private set; }
        public bool SawTransaction { get; private set; }

        public Task BeforeCommitAsync(CancellationToken ct)
        {
            Calls++;
            SawTransaction = db.Database.CurrentTransaction is not null;
            return Task.CompletedTask;
        }
    }

    /// Writes a row through the db and throws, like a hook that fails half-way.
    private sealed class WriteThenThrowHook(AppDbContext db) : IBeforeCommitHook
    {
        public async Task BeforeCommitAsync(CancellationToken ct)
        {
            await db.Database.ExecuteSqlRawAsync("INSERT INTO hook_probe VALUES (1)", ct);
            throw new InvalidOperationException("hook failed");
        }
    }

    private async Task RunAsync(Func<EfTransactionRunner, Task> test, Action<CountingHook> assert)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hook = new CountingHook(db);
        var services = new ServiceCollection();
        services.AddSingleton<IBeforeCommitHook>(hook);
        await using var provider = services.BuildServiceProvider();
        await test(new EfTransactionRunner(db, provider));
        assert(hook);
    }

    [Fact]
    public Task Hook_runs_once_before_the_commit() => RunAsync(
        runner => runner.RunAsync(_ => Task.CompletedTask),
        hook =>
        {
            hook.Calls.Should().Be(1);
            hook.SawTransaction.Should().BeTrue("the hook runs inside the transaction");
        });

    [Fact]
    public Task Nested_run_does_not_call_the_hook_again() => RunAsync(
        runner => runner.RunAsync(ct => runner.RunAsync(_ => Task.CompletedTask, ct)),
        hook => hook.Calls.Should().Be(1));

    [Fact]
    public Task Failing_work_does_not_call_the_hook() => RunAsync(
        async runner =>
        {
            var act = () => runner.RunAsync(_ => throw new InvalidOperationException("boom"));
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        },
        hook => hook.Calls.Should().Be(0));

    [Fact]
    public async Task Failing_hook_rolls_the_transaction_back()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE hook_probe (id int)");
        var services = new ServiceCollection();
        services.AddSingleton<IBeforeCommitHook>(new WriteThenThrowHook(db));
        await using var provider = services.BuildServiceProvider();
        var runner = new EfTransactionRunner(db, provider);

        var act = () => runner.RunAsync(_ => Task.CompletedTask);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("hook failed");

        var rows = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM hook_probe").SingleAsync();
        rows.Should().Be(0, "the hook's write is rolled back with the transaction");
    }
}
