using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// sales/top (Phase 01, Task 1.7).
[Collection(nameof(PostgresCollection))]
public class SalesTopTests : ExecutiveDashboardTestBase
{
    public SalesTopTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Top_customers_of_main_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Customer&branchId={ids.MainBranchId}");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(ids.Kh1Id);
        row.Code.Should().Be(ids.Kh1Code);
        row.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Top_customers_company_wide_merge_branches()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Customer");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(ids.Kh1Id);
        row.Revenue.Should().Be(12_499m);
    }

    [Fact]
    public async Task Top_products_ordered_by_revenue()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Product&branchId={ids.MainBranchId}");

        rows.Select(r => r.Revenue).Should().Equal(3_000m, -500m);
        rows.Should().OnlyContain(r => r.Code != null && r.Name != string.Empty);
    }

    [Fact]
    public async Task Top_employees()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var admin = await InDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Username == "admin")
            .Select(u => new { u.Id, u.FullName })
            .SingleAsync());

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Employee&branchId={ids.MainBranchId}");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(admin.Id);
        row.Name.Should().Be(admin.FullName);
        row.Code.Should().BeNull();
        row.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Sort_by_gross_profit()
    {
        var (branch, a, b) = await SeedTwoProductsAsync();
        var query = $"{October}&by=Product&branchId={branch.BranchId}";

        var byRevenue = await GetTopAsync(branch.Client, $"{query}&sort=Revenue");
        var byProfit = await GetTopAsync(branch.Client, $"{query}&sort=GrossProfit");

        byRevenue.Select(r => r.Id).Should().Equal(b, a);
        byProfit.Select(r => r.Id).Should().Equal(a, b);
        byProfit.Select(r => r.Revenue).Should().Equal(300m, 440m);
        byProfit.Select(r => r.GrossProfit).Should().Equal(100m, 40m);
    }

    [Fact]
    public async Task Gross_profit_sort_without_view_cost_falls_back_to_revenue()
    {
        var (branch, _, b) = await SeedTwoProductsAsync();
        var user = await CreateClientWithPermissionsAsync("top_no_cost", branch.BranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Profit);

        var rows = await GetTopAsync(user, $"{October}&by=Product&sort=GrossProfit");

        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be(b);
        rows.Should().OnlyContain(r => r.GrossProfit == null);
    }

    [Fact]
    public async Task Vat_inclusive_price_is_reduced_on_the_product_list()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("TOP-VAT", track: false, priceIncludesVat: true);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-20 09:00", branch.CustomerId, product, 1, 1_100,
            request => request.Lines[0].VatRate = 10m);

        var rows = await GetTopAsync(branch.Client, $"{October}&by=Product&branchId={branch.BranchId}");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(product);
        row.Revenue.Should().Be(1_000m);
    }

    [Fact]
    public async Task Tracked_return_reduces_product_cost()
    {
        var (branch, product, sale) = await SeedTrackedSaleAsync();
        var returned = await PostLineAsync(branch, StockDirection.In, "NTL", "2026-10-06 09:00", branch.CustomerId,
            product, 1, 150);
        var soldCost = (await LedgerOfAsync(sale.Id))
            .Where(e => e.SourceType == LedgerSourceType.StockOut).Sum(e => e.CostAmount ?? 0m);
        var returnedValue = (await LedgerOfAsync(returned.Id))
            .Where(e => e.SourceType == LedgerSourceType.StockIn).Sum(e => e.InValue);

        var rows = await GetTopAsync(branch.Client, $"{October}&by=Product&branchId={branch.BranchId}");

        returnedValue.Should().BePositive();
        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(product);
        row.Revenue.Should().Be(450m);
        row.GrossProfit.Should().Be(450m - (soldCost - returnedValue));
    }

    [Fact]
    public async Task Customers_sorted_by_gross_profit()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("TOP-C");
        var customer1 = branch.CustomerId;
        var customer2 = await CreatePartnerAsync("FKH2" + Guid.NewGuid().ToString("N")[..8], isCustomer: true,
            isSupplier: false);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, product, 10, 100);
        // Customer 1: revenue 300, cost 200, profit 100. Customer 2: revenue 440, cost 400, profit 40.
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", customer1, product, 2, 150);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-05 09:00", customer2, product, 4, 110);
        var query = $"{October}&by=Customer&branchId={branch.BranchId}";

        var byRevenue = await GetTopAsync(branch.Client, $"{query}&sort=Revenue");
        var byProfit = await GetTopAsync(branch.Client, $"{query}&sort=GrossProfit");

        byRevenue.Select(r => r.Id).Should().Equal(customer2, customer1);
        byProfit.Select(r => r.Id).Should().Equal(customer1, customer2);
        byProfit.Select(r => r.GrossProfit).Should().Equal(100m, 40m);
    }

    [Fact]
    public async Task Limit_is_applied_and_validated()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);

        var rows = await GetTopAsync(client, $"{October}&by=Product&branchId={ids.MainBranchId}&limit=1");

        rows.Should().HaveCount(1);
        await AssertBadRequestKeyAsync(client, $"sales/top?{October}&by=Product&limit=0", "limit", "Số dòng từ 1 đến 50.");
        await AssertBadRequestKeyAsync(client, $"sales/top?{October}&by=Product&limit=51", "limit", "Số dòng từ 1 đến 50.");
    }

    [Fact]
    public async Task Limit_keeps_the_first_row_of_the_order()
    {
        var branch = await CreateFreshBranchAsync();
        // Created in reverse name order, so the order by name is not the order of creation.
        var second = await CreateInventoryProductAsync("LIM-B", track: false);
        var first = await CreateInventoryProductAsync("LIM-A", track: false);
        foreach (var product in new[] { second, first })
            await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-20 09:00", branch.CustomerId, product, 1, 500);
        var query = $"{October}&by=Product&branchId={branch.BranchId}&limit=1";

        // The same revenue: the name decides (ordinal).
        (await GetTopAsync(branch.Client, query)).Select(r => r.Id).Should().Equal(first);

        // The same revenue and the same name: the smaller Id decides, on every call.
        await InDbAsync(db => db.Products.Where(p => p.Id == first || p.Id == second)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "Cùng tên")));
        var smaller = new[] { first, second }.Min();
        (await GetTopAsync(branch.Client, query)).Select(r => r.Id).Should().Equal(smaller);
        (await GetTopAsync(branch.Client, query)).Select(r => r.Id).Should().Equal(smaller);
    }

    [Fact]
    public async Task Customers_with_the_same_name_and_revenue_are_ordered_by_id()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("TIE-01", track: false);
        var other = await CreatePartnerAsync("FKH3" + Guid.NewGuid().ToString("N")[..8], isCustomer: true,
            isSupplier: false);
        await InDbAsync(db => db.Customers.Where(c => c.Id == branch.CustomerId || c.Id == other)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, "Cùng tên")));
        var smaller = new[] { branch.CustomerId, other }.Min();
        var larger = new[] { branch.CustomerId, other }.Max();
        // The customer with the larger Id is sold to first, so that is the order the vouchers come back from the database.
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-20 09:00", larger, product, 1, 500);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-21 09:00", smaller, product, 1, 500);

        var rows = await GetTopAsync(branch.Client, $"{October}&by=Customer&branchId={branch.BranchId}&limit=1");

        rows.Select(r => r.Id).Should().Equal(smaller);
    }

    /// Pins the framework: model binding refuses a number outside the enum by itself, before any validator runs.
    /// The body is a ValidationProblemDetails, not the app envelope, so only the status is asserted.
    [Fact]
    public async Task Undefined_by_or_sort_number_is_rejected()
    {
        var client = CloneAdminClient();

        (await GetExecRawAsync(client, $"sales/top?{October}&by=7")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetExecRawAsync(client, $"sales/top?{October}&by=Customer&sort=7"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// A fresh branch with two tracked products (VAT 0), each bought 10 × 100. A is sold 2 × 150 (revenue 300, cost 200,
    /// profit 100) and B 4 × 110 (revenue 440, cost 400, profit 40).
    private async Task<(FreshBranch Branch, Guid ProductA, Guid ProductB)> SeedTwoProductsAsync()
    {
        var branch = await CreateFreshBranchAsync();
        var a = await CreateInventoryProductAsync("TOP-A");
        var b = await CreateInventoryProductAsync("TOP-B");
        foreach (var product in new[] { a, b })
            await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, product, 10, 100);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, a, 2, 150);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, b, 4, 110);
        return (branch, a, b);
    }

    private static Task<List<SalesTopRowDto>> GetTopAsync(HttpClient client, string query) =>
        GetExecAsync<List<SalesTopRowDto>>(client, $"sales/top?{query}");
}
