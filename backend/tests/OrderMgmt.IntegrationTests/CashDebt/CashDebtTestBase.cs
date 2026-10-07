using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;

namespace OrderMgmt.IntegrationTests.CashDebt;

public abstract class CashDebtTestBase : InventoryTestBase
{
    protected CashDebtTestBase(PostgresFixture pg) : base(pg) { }
}
