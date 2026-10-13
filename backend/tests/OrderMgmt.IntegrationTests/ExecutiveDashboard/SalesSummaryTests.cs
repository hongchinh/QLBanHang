using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// sales/summary: revenue of the report dataset (Phase 01, Task 1.3).
[Collection(nameof(PostgresCollection))]
public class SalesSummaryTests : ExecutiveDashboardTestBase
{
    public SalesSummaryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Revenue_of_main_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetSummaryAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        summary.Revenue.Should().Be(2_500m);
        summary.PrevRevenue.Should().Be(0m);
        summary.RevenueDeltaPct.Should().BeNull();
        summary.From.Should().Be(new DateOnly(2026, 10, 1));
        summary.To.Should().Be(new DateOnly(2026, 10, 31));
        summary.PrevFrom.Should().Be(new DateOnly(2026, 8, 31));
        summary.PrevTo.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task Revenue_company_wide()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetSummaryAsync(MainClient(ids), October);

        summary.Revenue.Should().Be(12_499m);
    }

    [Fact]
    public async Task Delta_against_previous_period()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetSummaryAsync(MainClient(ids), $"from=2026-10-05&to=2026-10-08&branchId={ids.MainBranchId}");

        summary.Revenue.Should().Be(-500m);
        summary.PrevRevenue.Should().Be(3_000m);
        summary.RevenueDeltaPct.Should().Be(-116.67m);
    }

    [Fact]
    public async Task Cancelled_voucher_is_not_counted()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var query = $"{October}&branchId={ids.MainBranchId}";
        var sale = await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", "2026-10-20 09:00",
            ids.MainWarehouseId, ids.Kh1Id, 700m, paymentMethodId: null, paid: 0m);

        (await GetSummaryAsync(client, query)).Revenue.Should().Be(3_200m);

        var cancel = await client.PostAsJsonAsync($"/api/stock-vouchers/{sale.Id}/cancel",
            new StockVoucherActionRequest { Version = sale.Version });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());

        (await GetSummaryAsync(client, query)).Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Deleted_voucher_and_removed_line_are_not_counted()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var query = $"{October}&branchId={ids.MainBranchId}";
        var seven = await CreateInventoryProductAsync("DEL-700", track: false);
        var three = await CreateInventoryProductAsync("DEL-300", track: false);
        var sale = await PostSaleAsync(ids, "2026-10-20 09:00", LineRequest(seven, 1, 700m), LineRequest(three, 1, 300m));

        (await GetSummaryAsync(client, query)).Revenue.Should().Be(3_500m);

        var update = UpdateRequestFrom(sale);
        update.Lines = update.Lines.Where(l => l.ProductId == seven).ToList();
        var (updateStatus, updated, updateError) = await PutVoucherAsync(client, sale.Id, update);
        updateStatus.Should().Be(HttpStatusCode.OK, updateError?.Message);

        (await GetSummaryAsync(client, query)).Revenue.Should().Be(3_200m);

        var delete = await client.DeleteAsync($"/api/stock-vouchers/{sale.Id}?version={updated!.Version}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK, await delete.Content.ReadAsStringAsync());

        (await GetSummaryAsync(client, query)).Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Vat_inclusive_price_is_reduced_to_before_vat()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var product = await CreateInventoryProductAsync("VAT-IN", track: false, priceIncludesVat: true);
        var line = LineRequest(product, 1, 1_100m);
        line.VatRate = 10m;
        await PostSaleAsync(ids, "2026-10-20 09:00", line);

        var summary = await GetSummaryAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        summary.Revenue.Should().Be(3_500m);
    }

    [Fact]
    public async Task Vat_exclusive_price_is_taken_as_is()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var product = await CreateInventoryProductAsync("VAT-EX", track: false, priceIncludesVat: false);
        var line = LineRequest(product, 1, 1_000m);
        line.VatRate = 10m;
        await PostSaleAsync(ids, "2026-10-20 09:00", line);

        var summary = await GetSummaryAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        summary.Revenue.Should().Be(3_500m);
    }

    [Fact]
    public async Task Line_and_order_discounts_reduce_revenue()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var product = await CreateInventoryProductAsync("DISC-01", track: false);
        var line = LineRequest(product, 10, 100m);
        line.DiscountRate = 10m;
        // Amount 1,000 less the line discount 100 less the order discount 90 allocated to the only line.
        await PostSaleAsync(ids, "2026-10-20 09:00", request => request.OrderDiscount = 90m, line);
        var client = MainClient(ids);

        var summary = await GetSummaryAsync(client, $"{October}&branchId={ids.MainBranchId}");
        var products = await GetExecAsync<List<SalesTopRowDto>>(client,
            $"sales/top?{October}&by=Product&branchId={ids.MainBranchId}");

        summary.Revenue.Should().Be(3_310m);
        products.Single(row => row.Id == product).Revenue.Should().Be(810m);
    }

    [Fact]
    public async Task Requires_dashboard_executive()
    {
        var client = await CreateClientWithPermissionsAsync("profit_only", MainBranchId, Permissions.Reports.Profit);

        (await GetExecRawAsync(client, $"sales/summary?{October}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Requires_reports_profit()
    {
        var client = await CreateClientWithPermissionsAsync("executive_only", MainBranchId,
            Permissions.Dashboard.Executive);

        (await GetExecRawAsync(client, $"sales/summary?{October}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// An unpaid stock-out voucher (XBH) to KH1 in the main branch; asserts 200.
    private Task<StockVoucherDto> PostSaleAsync(ReportIds ids, string at, params UpsertStockVoucherLineRequest[] lines) =>
        PostSaleAsync(ids, at, _ => { }, lines);

    /// The same, after `configure` has changed the request (an order discount, for instance).
    private async Task<StockVoucherDto> PostSaleAsync(ReportIds ids, string at,
        Action<UpsertStockVoucherRequest> configure, params UpsertStockVoucherLineRequest[] lines)
    {
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), at, lines);
        request.WarehouseId = ids.MainWarehouseId;
        request.PartnerId = ids.Kh1Id;
        configure(request);
        var (status, voucher, error) = await PostVoucherAsync(MainClient(ids), request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }
}
