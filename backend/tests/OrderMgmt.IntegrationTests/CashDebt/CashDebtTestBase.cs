using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;

namespace OrderMgmt.IntegrationTests.CashDebt;

public abstract class CashDebtTestBase : InventoryTestBase
{
    protected CashDebtTestBase(PostgresFixture pg) : base(pg) { }

    /// Direct DB update of the settings singleton. Callers that change policies must restore Warn/Warn in a finally block.
    protected Task UpdateCashDebtSettingsAsync(Action<CashDebtSettings> mutate) => InDbAsync(async db =>
    {
        var settings = await db.CashDebtSettings.SingleAsync(s => s.Id == 1);
        mutate(settings);
        await db.SaveChangesAsync();
    });
}
