using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// inventory/summary: what the stock is worth now, how many products and warehouses hold it, the pairs below zero, and
/// the value received and issued by stock vouchers in the period.
[Collection(nameof(PostgresCollection))]
public class InventorySummaryTests : ExecutiveDashboardTestBase
{
    public InventorySummaryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Stock_value_equals_the_stock_on_hand_report()
    {
        var stock = await SeedStockScenarioAsync();

        var summary = await GetInventorySummaryAsync(stock.Branch.Client, $"{October}&branchId={stock.Branch.BranchId}");
        var report = await ReadDataAsync<StockOnHandReportDto>(await stock.Branch.Client.GetAsync("/api/reports/stock-on-hand"));

        summary.From.Should().Be(D(10, 1));
        summary.To.Should().Be(D(10, 31));
        summary.CanViewCost.Should().BeTrue();
        summary.StockValue.Should().Be(2_600m);
        summary.ProductCount.Should().Be(2);
        summary.WarehouseCount.Should().Be(2);
        summary.NegativeCount.Should().Be(0);
        summary.StockValue.Should().Be(report.TotalValue);
    }

    [Fact]
    public async Task Period_in_and_out()
    {
        var stock = await SeedStockScenarioAsync();
        var client = stock.Branch.Client;

        var october = await GetInventorySummaryAsync(client, $"{October}&branchId={stock.Branch.BranchId}");
        var november = await GetInventorySummaryAsync(client, $"from=2026-11-01&to=2026-11-30&branchId={stock.Branch.BranchId}");

        // Three purchases of 1,000 came in; the sale took out 4 of A at its average cost of 100.
        (october.InValue, october.OutCost).Should().Be(((decimal?)3_000m, (decimal?)400m));
        // Nothing moved in November, and the stock is worth the same: its value is now, not at the end of the period.
        (november.InValue, november.OutCost).Should().Be(((decimal?)0m, (decimal?)0m));
        november.StockValue.Should().Be(2_600m);
    }

    [Fact]
    public async Task Company_wide_sums_branches()
    {
        await SeedStockScenarioAsync();
        var branchY = await CreateFreshBranchAsync();
        var productE = await CreateInventoryProductAsync("STE" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(branchY, StockDirection.In, "NMH", "2026-10-02 09:00", branchY.SupplierId, productE, 2, 50);
        var admin = CloneAdminClient();

        var company = await GetInventorySummaryAsync(admin, October);
        var onlyY = await GetInventorySummaryAsync(admin, $"{October}&branchId={branchY.BranchId}");

        company.StockValue.Should().Be(2_700m);
        company.ProductCount.Should().Be(3);
        company.WarehouseCount.Should().Be(3);
        onlyY.StockValue.Should().Be(100m);
        (onlyY.ProductCount, onlyY.WarehouseCount).Should().Be((1, 1));
    }

    [Fact]
    public async Task Negative_stock_is_counted()
    {
        var stock = await SeedStockScenarioAsync();
        var productC = await CreateInventoryProductAsync("STC" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(stock.Branch, StockDirection.Out, "XBH", "2026-10-05 09:00", stock.Branch.CustomerId,
            productC, 3, 100, request => request.AcknowledgeNegativeStock = true);

        var summary = await GetInventorySummaryAsync(stock.Branch.Client, $"{October}&branchId={stock.Branch.BranchId}");

        summary.NegativeCount.Should().Be(1);
        // A quantity below zero is not zero: C is one of the products in stock.
        summary.ProductCount.Should().Be(3);
    }

    [Fact]
    public async Task Values_are_hidden_without_view_cost()
    {
        var stock = await SeedStockScenarioAsync();
        var user = await CreateClientWithPermissionsAsync("inv_no_cost", stock.Branch.BranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Inventory);

        var summary = await GetInventorySummaryAsync(user, October);

        summary.CanViewCost.Should().BeFalse();
        (summary.StockValue, summary.InValue, summary.OutCost).Should().Be(((decimal?)null, (decimal?)null, (decimal?)null));
        summary.ProductCount.Should().Be(2);
        summary.WarehouseCount.Should().Be(2);
    }

    [Fact]
    public async Task Sold_out_stock_is_not_counted()
    {
        var stock = await SeedStockScenarioAsync();
        var branch = stock.Branch;
        // Z is bought and sold in full in a third warehouse: its pair is at zero, so neither Z nor W3 holds stock.
        var w3 = await CreateWarehouseAsync(branch.BranchId, "FZZ" + Guid.NewGuid().ToString("N")[..8]);
        var productZ = await CreateInventoryProductAsync("STZ" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, productZ, 2, 100,
            request => request.WarehouseId = w3);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, productZ, 2, 150,
            request => request.WarehouseId = w3);

        var summary = await GetInventorySummaryAsync(branch.Client, $"{October}&branchId={branch.BranchId}");

        (summary.ProductCount, summary.WarehouseCount, summary.NegativeCount).Should().Be((2, 2, 0));
        summary.StockValue.Should().Be(2_600m);
    }

    [Fact]
    public async Task Period_runs_from_midnight_to_midnight_in_vn_time()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("STP" + Guid.NewGuid().ToString("N")[..8]);
        // Three purchases of different sizes around the one-day period 10-05.
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-04 23:59", branch.SupplierId, product, 1, 10);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-05 00:00", branch.SupplierId, product, 1, 20);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-06 00:00", branch.SupplierId, product, 1, 40);

        var summary = await GetInventorySummaryAsync(branch.Client, $"from=2026-10-05&to=2026-10-05&branchId={branch.BranchId}");

        summary.InValue.Should().Be(20m);
        summary.StockValue.Should().Be(70m);
    }

    [Fact]
    public async Task Opening_stock_is_not_received_in_the_period()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("STO" + Guid.NewGuid().ToString("N")[..8]);
        await SetBranchOpeningDateAsync(branch.BranchId, D(10, 1));
        var response = await branch.Client.PutAsJsonAsync("/api/inventory/opening-stock", new SaveOpeningStockRequest
        {
            WarehouseId = branch.WarehouseId,
            Lines = new List<SaveOpeningStockLine> { new() { ProductId = product, Quantity = 5, Amount = 500m } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var summary = await GetInventorySummaryAsync(branch.Client, $"{October}&branchId={branch.BranchId}");

        // The opening stock is posted at 10-01 00:00, inside the period, and it is stock worth 500, but nothing was
        // received by a stock-in voucher.
        summary.StockValue.Should().Be(500m);
        summary.InValue.Should().Be(0m);
    }

    private static Task<InventorySummaryDto> GetInventorySummaryAsync(HttpClient client, string query) =>
        GetExecAsync<InventorySummaryDto>(client, $"inventory/summary?{query}");
}
