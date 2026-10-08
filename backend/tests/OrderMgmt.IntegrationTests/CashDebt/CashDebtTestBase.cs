using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;

namespace OrderMgmt.IntegrationTests.CashDebt;

public abstract class CashDebtTestBase : InventoryTestBase
{
    protected CashDebtTestBase(PostgresFixture pg) : base(pg) { }

    /// Runs `work` in a new scope inside ITransactionRunner (copied from InventoryEngineTestBase: the class chains differ).
    protected async Task<T> InTransactionAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
        return await runner.RunAsync(ct => work(scope.ServiceProvider, ct));
    }

    /// Direct DB update of the settings singleton. Callers that change policies must restore Warn/Warn in a finally block.
    protected Task UpdateCashDebtSettingsAsync(Action<CashDebtSettings> mutate) => InDbAsync(async db =>
    {
        var settings = await db.CashDebtSettings.SingleAsync(s => s.Id == 1);
        mutate(settings);
        await db.SaveChangesAsync();
    });
}
