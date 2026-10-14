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
    public async Task Vat_exclusive_price_is_taken_as_is_on_the_product_list()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("TOP-VEX", track: false, priceIncludesVat: false);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-20 09:00", branch.CustomerId, product, 1, 1_000,
            request => request.Lines[0].VatRate = 10m);

        var rows = await GetTopAsync(branch.Client, $"{October}&by=Product&branchId={branch.BranchId}");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(product);
        row.Revenue.Should().Be(1_000m);
    }

    [Fact]
    public async Task Vat_inclusive_return_is_not_reduced_by_vat()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("TOP-RET", track: false, priceIncludesVat: true);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-20 09:00", branch.CustomerId, product, 2, 1_100,
            request => request.Lines[0].VatRate = 10m);
        await PostLineAsync(branch, StockDirection.In, "NTL", "2026-10-21 09:00", branch.CustomerId, product, 1, 1_100,
            request => request.Lines[0].VatRate = 10m);
        var query = $"{October}&branchId={branch.BranchId}";

        var summary = await GetSummaryAsync(branch.Client, query);
        var rows = await GetTopAsync(branch.Client, $"{query}&by=Product");

        // The sale is 2,200 less the 200 VAT inside its price; a stock-in line is priced before VAT, so all 1,100 comes back.
        summary.Revenue.Should().Be(900m);
        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(product);
        row.Revenue.Should().Be(900m);
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
        var (branch, customer1, customer2) = await SeedTwoCustomersAsync();
        var query = $"{October}&by=Customer&branchId={branch.BranchId}";

        var byRevenue = await GetTopAsync(branch.Client, $"{query}&sort=Revenue");
        var byProfit = await GetTopAsync(branch.Client, $"{query}&sort=GrossProfit");

        byRevenue.Select(r => r.Id).Should().Equal(customer2, customer1);
        byProfit.Select(r => r.Id).Should().Equal(customer1, customer2);
        byProfit.Select(r => r.GrossProfit).Should().Equal(100m, 40m);
    }

    [Fact]
    public async Task Employee_gross_profit_adds_up_the_sales_of_both_customers()
    {
        var (branch, _, _) = await SeedTwoCustomersAsync();

        var rows = await GetTopAsync(branch.Client, $"{October}&by=Employee&branchId={branch.BranchId}");

        // Both sales were posted by the admin user: revenue 300 + 440, profit 100 + 40.
        var row = rows.Should().ContainSingle().Which;
        row.Revenue.Should().Be(740m);
        row.GrossProfit.Should().Be(140m);
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
        var product1 = await CreateInventoryProductAsync("LIM-1", track: false);
        var product2 = await CreateInventoryProductAsync("LIM-2", track: false);
        var smaller = product1.CompareTo(product2) < 0 ? product1 : product2;
        var larger = smaller == product1 ? product2 : product1;
        // Ids are random: the name that sorts first goes to the LARGER Id, so neither the order of the Ids nor the
        // order the database returns the groups in can give the expected row by accident.
        await RenameProductAsync(larger, "A sorts first");
        await RenameProductAsync(smaller, "B sorts second");
        foreach (var product in new[] { smaller, larger })
            await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-20 09:00", branch.CustomerId, product, 1, 500);
        var query = $"{October}&by=Product&branchId={branch.BranchId}&limit=1";

        // The same revenue: the name decides (ordinal), not the Id.
        (await GetTopAsync(branch.Client, query)).Select(r => r.Id).Should().Equal(larger);

        // The same revenue and the same name: the smaller Id decides, on every call.
        await RenameProductAsync(larger, "Cùng tên");
        await RenameProductAsync(smaller, "Cùng tên");
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

    private Task RenameProductAsync(Guid productId, string name) =>
        InDbAsync(db => db.Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, name)));

    /// A fresh branch where one tracked product (VAT 0) is bought 10 × 100 and sold by the admin user to two customers:
    /// customer 1 2 × 150 (revenue 300, cost 200, profit 100), customer 2 4 × 110 (revenue 440, cost 400, profit 40).
    private async Task<(FreshBranch Branch, Guid Customer1, Guid Customer2)> SeedTwoCustomersAsync()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("TOP-C");
        var customer2 = await CreatePartnerAsync("FKH2" + Guid.NewGuid().ToString("N")[..8], isCustomer: true,
            isSupplier: false);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, product, 10, 100);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, product, 2, 150);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-05 09:00", customer2, product, 4, 110);
        return (branch, branch.CustomerId, customer2);
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
