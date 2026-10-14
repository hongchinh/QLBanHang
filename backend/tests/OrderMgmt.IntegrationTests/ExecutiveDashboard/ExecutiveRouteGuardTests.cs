using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// The guards every executive route must keep: the report permission of its domain, the branch scope of a user
/// without branches.access_all, the own-branch default, the company-wide default of a user with it, and the check of
/// its query. One row of `Routes` per route carries the route's own expectations: a phase that adds a route adds its
/// row here (and its documents to `ArrangeAsync`) and the five guards run on it.
[Collection(nameof(PostgresCollection))]
public class ExecutiveRouteGuardTests : ExecutiveDashboardTestBase
{
    /// Route: the path under api/dashboard/executive. Extra: the query string after the period (starts with `&`).
    /// Permission: the report permission the route needs besides dashboard.executive. ReadTotal: the figure of the
    /// response that the two expectations are about. OwnBranch: that figure for the main branch of the arranged data
    /// alone. Company: that figure for every branch of it.
    private sealed record GuardedRoute(
        string Route, string Extra, string Permission, Func<HttpResponseMessage, Task<decimal>> ReadTotal,
        decimal OwnBranch, decimal Company);

    private static readonly GuardedRoute[] Routes =
    {
        new("sales/summary", "", Permissions.Reports.Profit,
            async response => (await ReadDataAsync<SalesSummaryDto>(response)).Revenue,
            OwnBranch: 2_500m, Company: 12_499m),
        new("sales/series", "&granularity=Day", Permissions.Reports.Profit,
            async response => (await ReadDataAsync<SalesSeriesDto>(response)).Points.Sum(point => point.Revenue),
            OwnBranch: 2_500m, Company: 12_499m),
        new("sales/top", "&by=Customer", Permissions.Reports.Profit,
            async response => (await ReadDataAsync<List<SalesTopRowDto>>(response)).Sum(row => row.Revenue),
            OwnBranch: 2_500m, Company: 12_499m),
        new("cash/summary", "&at=2026-10-31", Permissions.Reports.Cash,
            async response => (await ReadDataAsync<CashSummaryDto>(response)).Payments,
            OwnBranch: 5_700m, Company: 5_823m),
        new("cash/series", "&at=2026-10-31&granularity=Day", Permissions.Reports.Cash,
            async response => (await ReadDataAsync<CashSeriesDto>(response)).Points.Sum(point => point.Payments),
            OwnBranch: 5_700m, Company: 5_823m),
        new("cash/breakdown", "&at=2026-10-31", Permissions.Reports.Cash,
            async response => (await ReadDataAsync<CashBreakdownDto>(response)).ByReason
                .Where(row => row.Type == CashVoucherType.Payment).Sum(row => row.Total),
            OwnBranch: 5_700m, Company: 5_823m),
        new("debt/summary", "&at=2026-10-31", Permissions.Reports.Debt,
            async response => (await ReadDataAsync<DebtSummaryDto>(response)).Receivable.Balance,
            OwnBranch: 1_500m, Company: 11_499m),
        new("debt/breakdown", "&at=2026-10-31", Permissions.Reports.Debt,
            async response => (await ReadDataAsync<DebtBreakdownDto>(response)).TopDebtors.Sum(row => row.Balance),
            OwnBranch: 1_500m, Company: 11_499m),
        new("inventory/summary", "", Permissions.Reports.Inventory,
            async response => (await ReadDataAsync<InventorySummaryDto>(response)).ProductCount,
            OwnBranch: 1m, Company: 2m),
    };

    public ExecutiveRouteGuardTests(PostgresFixture pg) : base(pg) { }

    public static IEnumerable<object[]> RouteNames => Routes.Select(route => new object[] { route.Route });

    /// Every route that takes a period, with the rest of its query string.
    public static IEnumerable<object[]> PeriodRoutes => Routes.Select(route => new object[] { route.Route, route.Extra });

    [Theory]
    [MemberData(nameof(RouteNames))]
    public async Task Requires_the_domain_permission(string routeName)
    {
        var route = RouteOf(routeName);
        var user = await CreateClientWithPermissionsAsync("executive_only", MainBranchId, Permissions.Dashboard.Executive);

        (await GetExecRawAsync(user, UrlOf(route))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(RouteNames))]
    public async Task Foreign_branch_is_refused(string routeName)
    {
        var route = RouteOf(routeName);
        // The refusal needs no document: two branches and a user of the first one.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var own = await CreateBranchAsync("GA" + suffix);
        var other = await CreateBranchAsync("GB" + suffix);
        var user = await BranchUserAsync(own, route);

        (await GetExecRawAsync(user, UrlOf(route, $"&branchId={other}")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(RouteNames))]
    public async Task Own_branch_by_default(string routeName)
    {
        var route = RouteOf(routeName);
        var ids = await ArrangeAsync();
        var user = await BranchUserAsync(ids.MainBranchId, route);

        var total = await route.ReadTotal(await GetExecRawAsync(user, UrlOf(route)));

        // The user may read the main branch only: what branch B sold must not be in it.
        total.Should().Be(route.OwnBranch);
    }

    [Theory]
    [MemberData(nameof(RouteNames))]
    public async Task Company_wide_for_access_all(string routeName)
    {
        var route = RouteOf(routeName);
        await ArrangeAsync();

        var total = await route.ReadTotal(await GetExecRawAsync(CloneAdminClient(), UrlOf(route)));

        // An admin without branchId reads every branch.
        total.Should().Be(route.Company);
    }

    [Theory]
    [MemberData(nameof(PeriodRoutes))]
    public async Task Period_that_ends_before_it_starts_is_rejected(string route, string extra)
    {
        await AssertBadRequestKeyAsync(CloneAdminClient(), $"{route}?from=2026-10-10&to=2026-10-01{extra}",
            "to", "Đến ngày phải sau hoặc bằng từ ngày.");
    }

    /// The documents the cases read: the report dataset plus one payment of 123 from branch B's cash fund, because the
    /// dataset has no cash document in branch B, and one purchase of a tracked product in each branch (1 × 77 in the
    /// main branch, 1 × 50 in branch B), because the dataset holds no tracked stock. The payment's reason CK has no
    /// debt effect and the purchases add to the payable only, so the sales, cash and receivable figures of the dataset
    /// stay as they are.
    private async Task<ReportIds> ArrangeAsync()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var branchB = CloneAdminClient();
        UseBranch(branchB, ids.BranchBId);
        var payment = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            await CashFundIdAsync(ids.BranchBId), "2026-10-06 09:00", partnerId: null, 123m);
        payment.PersonName = "Nguyễn B";
        payment.AcknowledgeNegativeFund = true;
        await CreateCashAsync(branchB, payment);

        var branchBWarehouse = await InDbAsync(db => db.Warehouses.Where(w => w.BranchId == ids.BranchBId)
            .Select(w => w.Id).SingleAsync());
        await PostTrackedPurchaseAsync(MainClient(ids), ids.MainWarehouseId, ids.Ncc1Id, 77m);
        await PostTrackedPurchaseAsync(branchB, branchBWarehouse, ids.Ncc1Id, 50m);
        return ids;
    }

    /// An unpaid purchase (NMH) of one unit of a new tracked product into the warehouse, on 10-07.
    private async Task PostTrackedPurchaseAsync(HttpClient client, Guid warehouseId, Guid supplierId, decimal price)
    {
        var product = await CreateInventoryProductAsync("GRD-" + Guid.NewGuid().ToString("N")[..8]);
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-10-07 09:00",
            LineRequest(product, 1, price));
        request.WarehouseId = warehouseId;
        request.PartnerId = supplierId;
        var (status, _, error) = await PostVoucherAsync(client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
    }

    private static GuardedRoute RouteOf(string routeName) => Routes.Single(route => route.Route == routeName);

    private static string UrlOf(GuardedRoute route, string more = "") => $"{route.Route}?{OctoberRange}{route.Extra}{more}";

    /// A user without branches.access_all whose default branch is the given one.
    private Task<HttpClient> BranchUserAsync(Guid branchId, GuardedRoute route) =>
        CreateClientWithPermissionsAsync("branch_user", branchId, Permissions.Dashboard.Executive, route.Permission);
}
