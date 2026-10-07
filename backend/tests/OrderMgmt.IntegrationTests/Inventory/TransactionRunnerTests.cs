using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class TransactionRunnerTests : InventoryTestBase
{
    public TransactionRunnerTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Commits_on_success_and_rolls_back_every_save_on_failure()
    {
        await InScopeAsync(async (runner, db) =>
            await runner.RunAsync(async ct =>
            {
                db.Branches.Add(new Branch { Code = "TX01", Name = "Commit" });
                await db.SaveChangesAsync(ct);
            }));

        var failing = () => InScopeAsync(async (runner, db) =>
            await runner.RunAsync(async ct =>
            {
                db.Branches.Add(new Branch { Code = "TX02", Name = "First save" });
                await db.SaveChangesAsync(ct);
                db.Branches.Add(new Branch { Code = "TX03", Name = "Second save" });
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("boom");
            }));
        await failing.Should().ThrowAsync<InvalidOperationException>();

        (await BranchCodesAsync()).Should().Contain("TX01").And.NotContain(new[] { "TX02", "TX03" });
    }

    [Fact]
    public async Task Nested_run_joins_the_outer_transaction()
    {
        var failing = () => InScopeAsync(async (runner, db) =>
            await runner.RunAsync(async ct =>
            {
                await runner.RunAsync(async innerCt =>
                {
                    db.Branches.Add(new Branch { Code = "TX04", Name = "Inner" });
                    await db.SaveChangesAsync(innerCt);
                }, ct);
                throw new InvalidOperationException("outer fails");
            }));
        await failing.Should().ThrowAsync<InvalidOperationException>();

        (await BranchCodesAsync()).Should().NotContain("TX04");
    }

    [Fact]
    public async Task A_failing_rollback_keeps_the_original_exception()
    {
        var failing = () => InScopeAsync(async (runner, db) =>
            await runner.RunAsync(async ct =>
            {
                // Ending the transaction inside the work makes the runner's rollback throw.
                await ((AppDbContext)db).Database.CurrentTransaction!.CommitAsync(ct);
                throw new ApplicationException("original");
            }));

        (await failing.Should().ThrowAsync<ApplicationException>()).WithMessage("original");
    }

    private async Task InScopeAsync(Func<ITransactionRunner, IAppDbContext, Task> work)
    {
        using var scope = _factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<ITransactionRunner>(),
            scope.ServiceProvider.GetRequiredService<IAppDbContext>());
    }

    private Task<List<string>> BranchCodesAsync() =>
        InDbAsync(db => db.Branches.IgnoreQueryFilters().Select(b => b.Code).ToListAsync());
}
