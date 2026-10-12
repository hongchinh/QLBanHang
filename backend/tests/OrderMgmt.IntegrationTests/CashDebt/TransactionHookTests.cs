using FluentAssertions;
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

    private async Task RunAsync(Func<EfTransactionRunner, Task> test, Action<CountingHook> assert)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hook = new CountingHook(db);
        var services = new ServiceCollection();
        services.AddSingleton<IBeforeCommitHook>(hook);
        await using var provider = services.BuildServiceProvider();
        try
        {
            await test(new EfTransactionRunner(db, provider));
        }
        finally
        {
            assert(hook);
        }
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
}
