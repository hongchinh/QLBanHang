using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class InventorySettingsTests : InventoryTestBase
{
    public InventorySettingsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Defaults_are_readable_by_anyone_and_update_persists()
    {
        var sales = await CreateClientForRoleAsync("is_sales", RoleCodes.Sales);
        var defaults = await ReadDataAsync<InventorySettingsDto>(await sales.GetAsync("/api/inventory/settings"));
        defaults.CostingMethod.Should().Be(CostingMethod.PeriodicAverage);
        defaults.CostingPeriod.Should().Be(CostingPeriod.Month);
        defaults.CostingScope.Should().Be(CostingScope.Branch);
        defaults.PurchaseCostIncludesVat.Should().BeTrue();
        defaults.NegativeStockPolicy.Should().Be(NegativeStockPolicy.Warn);
        defaults.NetExcludesVat.Should().BeFalse();
        defaults.DefaultDateMode.Should().Be(DefaultDateMode.Now);

        var updated = await ReadDataAsync<InventorySettingsDto>(await _client.PutAsJsonAsync("/api/inventory/settings",
            new UpdateInventorySettingsRequest
            {
                CostingMethod = CostingMethod.PeriodicAverage,
                CostingPeriod = CostingPeriod.Quarter,
                CostingScope = CostingScope.Warehouse,
                PurchaseCostIncludesVat = false,
                NegativeStockPolicy = NegativeStockPolicy.Block,
                NetExcludesVat = true,
                DefaultDateMode = DefaultDateMode.PreviousVoucher,
            }));
        updated.CostingPeriod.Should().Be(CostingPeriod.Quarter);

        var reread = await ReadDataAsync<InventorySettingsDto>(await _client.GetAsync("/api/inventory/settings"));
        reread.CostingScope.Should().Be(CostingScope.Warehouse);
        reread.PurchaseCostIncludesVat.Should().BeFalse();
        reread.NegativeStockPolicy.Should().Be(NegativeStockPolicy.Block);
        reread.NetExcludesVat.Should().BeTrue();
        reread.DefaultDateMode.Should().Be(DefaultDateMode.PreviousVoucher);
    }

    [Fact]
    public async Task Update_rejects_fifo_and_requires_permission()
    {
        var request = new UpdateInventorySettingsRequest
        {
            CostingMethod = CostingMethod.Fifo,
            CostingPeriod = CostingPeriod.Month,
            CostingScope = CostingScope.Branch,
            PurchaseCostIncludesVat = true,
            NegativeStockPolicy = NegativeStockPolicy.Warn,
            DefaultDateMode = DefaultDateMode.Now,
        };
        (await _client.PutAsJsonAsync("/api/inventory/settings", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sales = await CreateClientForRoleAsync("is_sales2", RoleCodes.Sales);
        request.CostingMethod = CostingMethod.PeriodicAverage;
        (await sales.PutAsJsonAsync("/api/inventory/settings", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
