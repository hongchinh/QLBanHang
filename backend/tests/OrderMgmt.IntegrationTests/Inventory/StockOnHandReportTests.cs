using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class StockOnHandReportTests : InventoryTestBase
{
    public StockOnHandReportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Current_and_point_in_time_snapshots()
    {
        var p1 = await CreateInventoryProductAsync("SH01");
        var p2 = await CreateInventoryProductAsync("SH02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p1, 10, 100_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p1, 3, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-06 08:00", LineRequest(p2, 4, 50_000));

        var current = await GetReportAsync(_client, "");
        current.CanViewCost.Should().BeTrue();
        current.Rows.Should().BeEquivalentTo(new object[]
        {
            new { ProductId = p1, ProductCode = "SH01", WarehouseId = DefaultWarehouseId, WarehouseCode = "KHO01", Quantity = 7m, Value = 700_000m },
            new { ProductId = p2, ProductCode = "SH02", WarehouseId = DefaultWarehouseId, WarehouseCode = "KHO01", Quantity = 4m, Value = 200_000m },
        }, o => o.WithStrictOrdering());
        foreach (var row in current.Rows)
            row.Quantity.Should().Be(await StockOfAsync(row.ProductId, row.WarehouseId));
        current.TotalValue.Should().Be(900_000m);

        // `at` sent with the VN offset is normalized to UTC (D27).
        var beforeOut = await GetReportAsync(_client, $"at={At(Vn("2026-10-04 00:00"))}");
        beforeOut.At.Should().Be(Vn("2026-10-04 00:00"));
        beforeOut.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ProductId = p1, Quantity = 10m, Value = 1_000_000m });
        beforeOut.TotalValue.Should().Be(1_000_000m);

        var afterOut = await GetReportAsync(_client, $"at={At(Vn("2026-10-05 12:00"))}");
        afterOut.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ProductId = p1, Quantity = 7m, Value = 700_000m });
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Filters_branch_scope_and_provisional_flag()
    {
        var p1 = await CreateInventoryProductAsync("SH10");
        var p2 = await CreateInventoryProductAsync("SH11");
        var xps = await InDbAsync(async db =>
        {
            var groupId = await db.ProductGroups.Where(g => g.Code == "XPS").Select(g => g.Id).SingleAsync();
            (await db.Products.SingleAsync(p => p.Id == p2)).ProductGroupId = groupId;
            await db.SaveChangesAsync();
            return groupId;
        });
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p1, 5, 100_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 09:00", r => r.WarehouseId = second,
            LineRequest(p2, 3, 100_000), LineRequest(p1, 2, 100_000));

        var otherBranch = await CreateBranchAsync("CN02");
        var otherWarehouse = await CreateWarehouseAsync(otherBranch, "KHO-B");
        var admin = CloneAdminClient();
        UseBranch(admin, otherBranch);
        await CreateVoucherAsync(admin, StockDirection.In, "NKH", "2026-10-03 08:00", r => r.WarehouseId = otherWarehouse,
            LineRequest(p1, 7, 100_000));

        var all = await GetReportAsync(_client, "");
        all.Rows.Select(r => (r.ProductCode, r.WarehouseCode, r.Quantity)).Should().Equal(
            ("SH10", "KHO01", 5m), ("SH10", "KHO02", 2m), ("SH11", "KHO02", 3m));
        all.Rows.Last().ProductGroupName.Should().Be("Tấm xốp XPS");

        (await GetReportAsync(_client, $"warehouseId={second}")).Rows.Select(r => (r.ProductCode, r.WarehouseCode))
            .Should().Equal(("SH10", "KHO02"), ("SH11", "KHO02"));
        (await GetReportAsync(_client, $"productGroupId={xps}")).Rows.Should().ContainSingle()
            .Which.ProductId.Should().Be(p2);
        (await GetReportAsync(_client, "search=sh11")).Rows.Should().ContainSingle().Which.ProductId.Should().Be(p2);
        (await GetReportAsync(_client, "search=SH10")).Rows.Should().HaveCount(2).And.OnlyContain(r => r.ProductId == p1);

        var other = await GetReportAsync(admin, "");
        other.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { WarehouseCode = "KHO-B", Quantity = 7m });

        (await GetReportAsync(_client, "")).IsProvisional.Should().BeTrue();
        (await GetReportAsync(_client, $"at={At(DateTimeOffset.UtcNow)}")).IsProvisional.Should().BeTrue();
        (await GetReportAsync(_client, $"at={At(Vn("2026-01-15 12:00"))}")).IsProvisional.Should().BeFalse();
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Values_hidden_without_view_cost_and_permission_required()
    {
        var p = await CreateInventoryProductAsync("SH20");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));

        var warehouse = await CreateClientForRoleAsync("kho_soh", RoleCodes.Warehouse);
        var report = await GetReportAsync(warehouse, "");
        report.CanViewCost.Should().BeFalse();
        report.TotalValue.Should().BeNull();
        report.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ProductId = p, Quantity = 10m, Value = (decimal?)null });

        var sales = await CreateClientForRoleAsync("sales_soh", RoleCodes.Sales);
        (await sales.GetAsync("/api/reports/stock-on-hand")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Branch_scope_values_are_split_by_quantity()
    {
        var p = await CreateInventoryProductAsync("SH30");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 09:00", r => r.WarehouseId = second,
            LineRequest(p, 10, 200_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 10, 0));

        // D32: the branch value 1,500,000 is split by quantity, not shown as -500,000 / 2,000,000.
        var branch = await GetReportAsync(_client, "");
        branch.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { WarehouseCode = "KHO02", Quantity = 10m, Value = 1_500_000m });
        branch.TotalValue.Should().Be(1_500_000m);

        (await PutCostingScopeAsync(CostingScope.Warehouse)).Should().Be(HttpStatusCode.OK);
        var warehouse = await GetReportAsync(_client, "");
        warehouse.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { WarehouseCode = "KHO02", Quantity = 10m, Value = 2_000_000m });
        warehouse.TotalValue.Should().Be(2_000_000m);
        await AssertInvariantsAsync();
    }

    private static string At(DateTimeOffset at) => Uri.EscapeDataString(at.ToOffset(VnTime.Offset).ToString("O"));

    private static async Task<StockOnHandReportDto> GetReportAsync(HttpClient client, string query) =>
        await ReadDataAsync<StockOnHandReportDto>(await client.GetAsync($"/api/reports/stock-on-hand?{query}"));

    /// PUT /api/inventory/settings with only the costing scope changed (recalculates costs, D11).
    private async Task<HttpStatusCode> PutCostingScopeAsync(CostingScope scope)
    {
        var current = await ReadDataAsync<InventorySettingsDto>(await _client.GetAsync("/api/inventory/settings"));
        var response = await _client.PutAsJsonAsync("/api/inventory/settings", new UpdateInventorySettingsRequest
        {
            CostingMethod = current.CostingMethod,
            CostingPeriod = current.CostingPeriod,
            CostingScope = scope,
            PurchaseCostIncludesVat = current.PurchaseCostIncludesVat,
            NegativeStockPolicy = current.NegativeStockPolicy,
            NetExcludesVat = current.NetExcludesVat,
            DefaultDateMode = current.DefaultDateMode,
        });
        return response.StatusCode;
    }
}
