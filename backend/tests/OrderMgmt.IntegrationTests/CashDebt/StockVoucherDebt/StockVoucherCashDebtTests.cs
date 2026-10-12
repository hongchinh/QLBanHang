using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.AutoVouchers;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

/// The stock voucher's cash and debt steps live in StockVoucherCashDebt; StockVoucherService no longer takes the
/// cash and debt services it used to call directly.
[Collection(nameof(PostgresCollection))]
public class StockVoucherCashDebtTests : CashDebtTestBase
{
    public StockVoucherCashDebtTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Coordinator_resolves_from_a_request_scope()
    {
        var coordinator = await InTransactionAsync((sp, ct) => Task.FromResult(sp.GetRequiredService<IStockVoucherCashDebt>()));

        coordinator.Should().BeOfType<StockVoucherCashDebt>();
    }

    [Fact]
    public void StockVoucherService_no_longer_takes_the_cash_and_debt_services()
    {
        var parameters = typeof(StockVoucherService).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType).ToList();

        parameters.Should().NotContain(new[]
        {
            typeof(ICashDebtLock), typeof(IDebtLedgerService), typeof(IAutoCashVoucherService),
            typeof(ICreditLimitGuard), typeof(IFundBalanceGuard),
        });
        parameters.Should().Contain(typeof(IStockVoucherCashDebt));
    }
}
