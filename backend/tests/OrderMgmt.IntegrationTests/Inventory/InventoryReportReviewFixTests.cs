using FluentAssertions;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

/// Regression tests for the Phase 06 review findings.
[Collection(nameof(PostgresCollection))]
public class InventoryReportReviewFixTests : InventoryTestBase
{
    public InventoryReportReviewFixTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Zero_quantity_rows_are_dropped_for_users_without_view_cost()
    {
        await UpdateInventorySettingsAsync(s => s.CostingScope = CostingScope.Warehouse);
        var p = await CreateInventoryProductAsync("RR01");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-03 08:00", LineRequest(p, 10, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-20 08:00", LineRequest(p, 10, 200_000));
        var at = Uri.EscapeDataString(Vn("2026-10-05 12:00").ToString("O"));

        // With view_cost the pair (qty 0, value 1,000,000 − 1,500,000) is still listed.
        var full = await ReadDataAsync<StockOnHandReportDto>(await _client.GetAsync($"/api/reports/stock-on-hand?at={at}"));
        full.Rows.Should().ContainSingle(r => r.ProductId == p).Which.Value.Should().Be(-500_000m);

        var noCost = await CreateClientWithPermissionsAsync("kho_report", null, "reports.inventory");
        var masked = await ReadDataAsync<StockOnHandReportDto>(await noCost.GetAsync($"/api/reports/stock-on-hand?at={at}"));
        masked.Rows.Should().NotContain(r => r.ProductId == p);
    }
}
