using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.ExecutiveDashboard;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Dashboard;

/// The revenue figures of the overview dashboards come from the stock-outs of the working branch (XBH minus NTL),
/// owned by the voucher's Nhân viên phụ trách.
[Collection(nameof(PostgresCollection))]
public class DashboardRevenueTests : ExecutiveDashboardTestBase
{
    public DashboardRevenueTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Range_revenue_is_the_working_branch_net_of_returns()
    {
        var data = await SeedAsync();

        var summary = await ReadDataAsync<DashboardSummaryDto>(await data.Branch.Client.GetAsync(
            "/api/dashboard/summary?from=2026-10-01&to=2026-10-31"));

        summary.RangeRevenue.Value.Should().Be(750m);
    }

    [Fact]
    public async Task Range_revenue_equals_executive_sales_summary()
    {
        var data = await SeedAsync();

        var summary = await ReadDataAsync<DashboardSummaryDto>(await data.Branch.Client.GetAsync(
            "/api/dashboard/summary?from=2026-10-01&to=2026-10-31"));
        var executive = await GetExecAsync<SalesSummaryDto>(data.Branch.Client,
            $"sales/summary?{OctoberRange}&branchId={data.Branch.BranchId}");

        executive.Revenue.Should().Be(summary.RangeRevenue.Value);
    }

    [Fact]
    public async Task Sales_user_sees_own_vouchers_only()
    {
        var data = await SeedAsync();

        var own = await ReadDataAsync<DashboardSummaryDto>(await data.SalesClient.GetAsync(
            "/api/dashboard/summary?from=2026-10-01&to=2026-10-31"));
        // The owner filter of a user without quotations.view_all is ignored.
        var asked = await ReadDataAsync<DashboardSummaryDto>(await data.SalesClient.GetAsync(
            $"/api/dashboard/summary?from=2026-10-01&to=2026-10-31&saleUserId={data.AdminId}"));

        own.RangeRevenue.Value.Should().Be(300m);
        asked.RangeRevenue.Value.Should().Be(300m);
    }

    [Fact]
    public async Task Admin_sale_filter_uses_voucher_owner()
    {
        var data = await SeedAsync();

        var summary = await ReadDataAsync<DashboardSummaryDto>(await data.Branch.Client.GetAsync(
            $"/api/dashboard/summary?from=2026-10-01&to=2026-10-31&saleUserId={data.SalesId}"));

        summary.RangeRevenue.Value.Should().Be(300m);
    }

    [Fact]
    public async Task Previous_period_delta()
    {
        var data = await SeedAsync();

        // 10-06..10-07: 300 - 150 = 150; the two days before it (10-04..10-05) hold 600.
        var summary = await ReadDataAsync<DashboardSummaryDto>(await data.Branch.Client.GetAsync(
            "/api/dashboard/summary?from=2026-10-06&to=2026-10-07"));

        summary.RangeRevenue.Value.Should().Be(150m);
        summary.RangeRevenue.DeltaPct.Should().Be(-75.00m);
    }

    [Fact]
    public async Task Series_by_day_fills_gaps()
    {
        var data = await SeedAsync();

        var series = await ReadDataAsync<RevenueSeriesDto>(await data.Branch.Client.GetAsync(
            "/api/dashboard/revenue-series?from=2026-10-01&to=2026-10-07&granularity=day"));

        series.Points.Should().HaveCount(7);
        series.Points.Sum(p => p.Total).Should().Be(750m);
        var sold = series.Points.Single(p => p.Date == D(10, 4));
        sold.Total.Should().Be(600m);
        sold.VoucherCount.Should().Be(1);
        series.Points.Single(p => p.Date == D(10, 5)).Total.Should().Be(0m);
    }

    [Fact]
    public async Task Series_by_month()
    {
        var data = await SeedAsync();

        var series = await ReadDataAsync<RevenueSeriesDto>(await data.Branch.Client.GetAsync(
            "/api/dashboard/revenue-series?from=2026-10-01&to=2026-10-31&granularity=month"));

        series.Points.Should().ContainSingle().Which.Total.Should().Be(750m);
    }

    private sealed record SeedData(FreshBranch Branch, Guid ProductId, Guid CustomerId, Guid AdminId, Guid SalesId,
        HttpClient SalesClient, Guid SaleVoucherId, uint SaleVoucherVersion, FreshBranch Other);

    /// Product P in a fresh branch: NMH 20 × 100 on 10-02, XBH 4 × 150 on 10-04 (admin, 600), XBH 2 × 150 on 10-06
    /// (owner: the sales user S, 300), NTL 1 × 150 on 10-07 (admin, -150). Another fresh branch has XBH 1 × 1,000 on
    /// 10-04. In the branch: 750 in all, 300 for S.
    private async Task<SeedData> SeedAsync()
    {
        var branch = await CreateFreshBranchAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var salesClient = await CreateClientForRoleAsync("dr_s_" + suffix, RoleCodes.Sales, branch.BranchId);
        var salesId = await InDbAsync(db => db.Users.Where(u => u.Username == "dr_s_" + suffix).Select(u => u.Id).SingleAsync());
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        var product = await CreateInventoryProductAsync("DR-" + suffix);
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, product, 20, 100);
        var sale = await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, product, 4, 150);
        var ownedByS = await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-06 09:00", branch.CustomerId, product, 2, 150);
        // The request has no owner: the voucher is posted by the admin, so the owner is changed in the database.
        await InDbAsync(db => db.StockVouchers.Where(v => v.Id == ownedByS.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.OwnerUserId, salesId)));
        await PostLineAsync(branch, StockDirection.In, "NTL", "2026-10-07 09:00", branch.CustomerId, product, 1, 150);

        var other = await CreateFreshBranchAsync();
        var otherProduct = await CreateInventoryProductAsync("DRO-" + suffix);
        await PostLineAsync(other, StockDirection.In, "NMH", "2026-10-02 09:00", other.SupplierId, otherProduct, 5, 100);
        await PostLineAsync(other, StockDirection.Out, "XBH", "2026-10-04 09:00", other.CustomerId, otherProduct, 1, 1000);

        return new SeedData(branch, product, branch.CustomerId, adminId, salesId, salesClient, sale.Id, sale.Version, other);
    }
}
