using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// inventory/breakdown: the stock value by warehouse and by product group, the pairs below zero and the products worth
/// the most. Under the branch costing scope a product's value is shared out over its warehouses by quantity, as the
/// stock-on-hand report shows it.
[Collection(nameof(PostgresCollection))]
public class InventoryBreakdownTests : ExecutiveDashboardTestBase
{
    public InventoryBreakdownTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task By_warehouse_under_branch_costing()
    {
        var stock = await SeedStockScenarioAsync();
        var codes = await WarehouseCodesAsync(stock);
        var branchCode = await BranchCodeAsync(stock.Branch.BranchId);

        var breakdown = await GetBreakdownAsync(stock);

        // A's 1,600 is split 600 / 1,000 by quantity (6 and 10); W1 also holds B (1,000).
        breakdown.CanViewCost.Should().BeTrue();
        breakdown.ByWarehouse.Select(w => (w.Id, w.Code, w.BranchCode, w.Value, w.ProductCount)).Should().Equal(
            ((Guid?)stock.W1, codes[stock.W1], (string?)branchCode, (decimal?)1_600m, 2),
            ((Guid?)stock.W2, codes[stock.W2], (string?)branchCode, (decimal?)1_000m, 1));
        breakdown.ByWarehouse.Select(w => w.Name).Should().Equal($"Kho {codes[stock.W1]}", $"Kho {codes[stock.W2]}");
    }

    [Fact]
    public async Task By_warehouse_equals_the_report_under_both_scopes()
    {
        var stock = await SeedStockScenarioAsync();
        var branch = stock.Branch;
        // D costs 100 in W1 and 200 in W2; all of W1's 10 are sold.
        var productD = await CreateInventoryProductAsync("STD" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, productD, 10, 100);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, productD, 10, 200,
            request => request.WarehouseId = stock.W2);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, productD, 10, 250);

        // Branch scope: D's average cost is 150, its 1,500 left goes to W2 by quantity.
        await AssertEqualsTheReportAsync(stock, w1: 1_600m, w2: 2_500m, total: 4_100m);

        // Warehouse scope: each warehouse keeps its own cost, so W2 holds D at 200.
        (await PutCostingScopeAsync(CostingScope.Warehouse)).Should().Be(HttpStatusCode.OK);
        await AssertEqualsTheReportAsync(stock, w1: 1_600m, w2: 3_000m, total: 4_600m);
    }

    [Fact]
    public async Task By_product_group()
    {
        var stock = await SeedStockScenarioAsync();
        var groupName = await InDbAsync(db => db.ProductGroups.Where(g => g.Id == stock.GroupId)
            .Select(g => g.Name).SingleAsync());

        var breakdown = await GetBreakdownAsync(stock);

        breakdown.ByProductGroup.Select(g => (g.Id, g.Code, g.Name, g.BranchCode, g.Value, g.ProductCount)).Should().Equal(
            ((Guid?)stock.GroupId, (string?)null, (string?)groupName, (string?)null, (decimal?)1_600m, 1),
            ((Guid?)null, (string?)null, (string?)null, (string?)null, (decimal?)1_000m, 1));
    }

    [Fact]
    public async Task Top_by_value()
    {
        var stock = await SeedStockScenarioAsync();

        var breakdown = await GetBreakdownAsync(stock);

        breakdown.TopByValue.Select(p => (p.ProductId, p.Value, p.Quantity)).Should().Equal(
            (stock.ProductA, (decimal?)1_600m, 16m), (stock.ProductB, (decimal?)1_000m, 5m));
        var first = breakdown.TopByValue[0];
        first.Code.Should().Be(stock.ProductACode);
        first.Name.Should().Be($"Hàng {stock.ProductACode}");
        first.UnitName.Should().Be(await UnitNameAsync(stock.ProductA));
    }

    [Fact]
    public async Task Negative_rows()
    {
        var stock = await SeedStockScenarioAsync();
        var codeC = "STC" + Guid.NewGuid().ToString("N")[..8];
        var productC = await CreateInventoryProductAsync(codeC);
        await PostLineAsync(stock.Branch, StockDirection.Out, "XBH", "2026-10-05 09:00", stock.Branch.CustomerId,
            productC, 3, 100, request => request.AcknowledgeNegativeStock = true);
        var codes = await WarehouseCodesAsync(stock);
        var branchCode = await BranchCodeAsync(stock.Branch.BranchId);

        var breakdown = await GetBreakdownAsync(stock);

        var row = breakdown.Negative.Should().ContainSingle().Which;
        (row.ProductId, row.ProductCode, row.ProductName).Should().Be((productC, codeC, $"Hàng {codeC}"));
        (row.WarehouseId, row.WarehouseCode, row.BranchCode).Should().Be((stock.W1, codes[stock.W1], branchCode));
        row.Quantity.Should().Be(-3m);
        row.UnitName.Should().Be(await UnitNameAsync(productC));
    }

    [Fact]
    public async Task Values_are_hidden_without_view_cost()
    {
        var stock = await SeedStockScenarioAsync();
        var user = await CreateClientWithPermissionsAsync("inv_no_cost", stock.Branch.BranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Inventory);

        var breakdown = await GetExecAsync<InventoryBreakdownDto>(user, "inventory/breakdown");

        breakdown.CanViewCost.Should().BeFalse();
        breakdown.TopByValue.Should().BeEmpty();
        breakdown.ByWarehouse.Select(w => (w.Id, w.ProductCount, w.Value)).Should().Equal(
            ((Guid?)stock.W1, 2, (decimal?)null), ((Guid?)stock.W2, 1, (decimal?)null));
        breakdown.ByProductGroup.Should().OnlyContain(g => g.Value == null);
        breakdown.ByProductGroup.Should().HaveCount(2);
    }

    [Fact]
    public async Task Split_residual_follows_the_warehouse_codes_and_warehouse_costing_keeps_own_values()
    {
        var branch = await CreateFreshBranchAsync();
        var w2 = await CreateWarehouseAsync(branch.BranchId, "FZ" + Guid.NewGuid().ToString("N")[..8]);
        var productF = await CreateInventoryProductAsync("STF" + Guid.NewGuid().ToString("N")[..8]);
        // One unit at 50 in W1 and one at 51 in W2. Under the branch scope the 101 is split 50.5 / 50.5, rounded to
        // 51 / 51, and the residual of -1 goes to the last warehouse by code: W1 51, W2 50.
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, productF, 1, 50);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, productF, 1, 51,
            request => request.WarehouseId = w2);
        var query = $"inventory/breakdown?branchId={branch.BranchId}";

        var underBranch = await GetExecAsync<InventoryBreakdownDto>(branch.Client, query);
        (await PutCostingScopeAsync(CostingScope.Warehouse)).Should().Be(HttpStatusCode.OK);
        var underWarehouse = await GetExecAsync<InventoryBreakdownDto>(branch.Client, query);

        using (new AssertionScope())
        {
            underBranch.ByWarehouse.Select(w => (w.Id, w.Value)).Should().Equal(
                ((Guid?)branch.WarehouseId, (decimal?)51m), ((Guid?)w2, (decimal?)50m));
            // Each warehouse keeps the cost of its own purchase.
            underWarehouse.ByWarehouse.Select(w => (w.Id, w.Value)).Should().Equal(
                ((Guid?)branch.WarehouseId, (decimal?)50m), ((Guid?)w2, (decimal?)51m));
        }
    }

    [Fact]
    public async Task Sold_out_stock_is_not_counted()
    {
        var stock = await SeedStockScenarioAsync();
        var branch = stock.Branch;
        // Z (group G, like A) is bought and sold in full in a third warehouse: its pair is at zero quantity and value.
        var w3 = await CreateWarehouseAsync(branch.BranchId, "FZZ" + Guid.NewGuid().ToString("N")[..8]);
        var productZ = await CreateInventoryProductAsync("STZ" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, productZ, 2, 100,
            request => request.WarehouseId = w3);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, productZ, 2, 150,
            request => request.WarehouseId = w3);

        var breakdown = await GetBreakdownAsync(stock);

        using (new AssertionScope())
        {
            // W3 holds nothing: it has no row.
            breakdown.ByWarehouse.Select(w => (w.Id, w.ProductCount)).Should().Equal(
                ((Guid?)stock.W1, 2), ((Guid?)stock.W2, 1));
            // Z is in group G but not in stock.
            breakdown.ByProductGroup.Select(g => (g.Id, g.ProductCount)).Should().Equal(
                ((Guid?)stock.GroupId, 1), ((Guid?)null, 1));
            breakdown.Negative.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Product_groups_without_values_are_ordered_by_product_count()
    {
        var stock = await SeedStockScenarioAsync();
        // A second product of group G in stock: G has two products, the row without a group (B) one. Its name would
        // put the row without a group first.
        var productA2 = await CreateInventoryProductAsync("STA2" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(stock.Branch, StockDirection.In, "NMH", "2026-10-02 09:00", stock.Branch.SupplierId,
            productA2, 1, 10);
        var user = await CreateClientWithPermissionsAsync("inv_no_cost", stock.Branch.BranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Inventory);

        var breakdown = await GetExecAsync<InventoryBreakdownDto>(user, "inventory/breakdown");

        breakdown.ByProductGroup.Select(g => (g.Id, g.ProductCount)).Should().Equal(
            ((Guid?)stock.GroupId, 2), ((Guid?)null, 1));
    }

    [Fact]
    public async Task Long_lists_are_cut()
    {
        var stock = await SeedStockScenarioAsync();
        var branch = stock.Branch;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        // 52 tracked products without stock, created from the highest code down. N00 is sold from W2, N01 … N51 from
        // W1: 52 pairs below zero.
        var products = new Dictionary<int, Guid>();
        foreach (var number in Enumerable.Range(0, 52).Reverse())
            products[number] = await CreateInventoryProductAsync($"N{number:00}{suffix}");
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-05 09:00", branch.CustomerId, products[0], 1, 10,
            request =>
            {
                request.WarehouseId = stock.W2;
                request.AcknowledgeNegativeStock = true;
            });
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-05 09:00", branch.CustomerId, products[1], 1, 10,
            request =>
            {
                request.Lines = Enumerable.Range(1, 51)
                    .Select((number, index) =>
                    {
                        var line = LineRequest(products[number], 1, 10);
                        line.SortOrder = index;
                        return line;
                    })
                    .ToList();
                request.AcknowledgeNegativeStock = true;
            });

        var breakdown = await GetBreakdownAsync(stock);

        using (new AssertionScope())
        {
            // W1 sorts before W2, so its 51 pairs come first, by product code, and the list stops at 50: N51 and N00
            // (in W2) are cut.
            breakdown.Negative.Should().HaveCount(50);
            breakdown.Negative.Select(r => r.ProductId)
                .Should().Equal(Enumerable.Range(1, 50).Select(number => products[number]));
            breakdown.TopByValue.Should().HaveCount(10);
        }
    }

    [Fact]
    public async Task Each_branch_splits_its_own_value()
    {
        var branchX = await CreateFreshBranchAsync();
        var branchY = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("STY" + Guid.NewGuid().ToString("N")[..8]);
        // The same product costs 100 in X and 300 in Y: each branch has its own average cost.
        await PostLineAsync(branchX, StockDirection.In, "NMH", "2026-10-02 09:00", branchX.SupplierId, product, 1, 100);
        await PostLineAsync(branchY, StockDirection.In, "NMH", "2026-10-02 09:00", branchY.SupplierId, product, 1, 300);

        var breakdown = await GetExecAsync<InventoryBreakdownDto>(CloneAdminClient(), "inventory/breakdown");

        breakdown.ByWarehouse.Where(w => w.Id == branchX.WarehouseId || w.Id == branchY.WarehouseId)
            .Select(w => (w.Id, w.Value)).Should().BeEquivalentTo(new[]
            {
                ((Guid?)branchX.WarehouseId, (decimal?)100m), ((Guid?)branchY.WarehouseId, (decimal?)300m),
            });
    }

    /// Each warehouse's value equals the sum of the report rows of that warehouse, and the summary's stock value the
    /// report's total; the dashboard figures are also the given ones.
    private async Task AssertEqualsTheReportAsync(StockScenario stock, decimal w1, decimal w2, decimal total)
    {
        var client = stock.Branch.Client;
        var breakdown = await GetBreakdownAsync(stock);
        var summary = await GetExecAsync<InventorySummaryDto>(client,
            $"inventory/summary?{October}&branchId={stock.Branch.BranchId}");
        var report = await ReadDataAsync<StockOnHandReportDto>(await client.GetAsync("/api/reports/stock-on-hand"));

        breakdown.ByWarehouse.Select(w => (w.Id, w.Value)).Should().Equal(
            ((Guid?)stock.W1, (decimal?)w1), ((Guid?)stock.W2, (decimal?)w2));
        foreach (var warehouse in breakdown.ByWarehouse)
            warehouse.Value.Should().Be(report.Rows.Where(r => r.WarehouseId == warehouse.Id).Sum(r => r.Value ?? 0m),
                $"warehouse {warehouse.Code}");
        summary.StockValue.Should().Be(total);
        summary.StockValue.Should().Be(report.TotalValue);
    }

    /// The unit the stock of a product counted per unit is shown in.
    private Task<string> UnitNameAsync(Guid productId) =>
        InDbAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.Unit!.Name).SingleAsync());

    private Task<Dictionary<Guid, string>> WarehouseCodesAsync(StockScenario stock) =>
        InDbAsync(db => db.Warehouses.Where(w => w.BranchId == stock.Branch.BranchId)
            .ToDictionaryAsync(w => w.Id, w => w.Code));

    private static Task<InventoryBreakdownDto> GetBreakdownAsync(StockScenario stock) =>
        GetExecAsync<InventoryBreakdownDto>(stock.Branch.Client, $"inventory/breakdown?branchId={stock.Branch.BranchId}");

    /// PUT /api/inventory/settings with only the costing scope changed: the costs are recalculated.
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
