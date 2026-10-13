using System.Net;
using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// The guards every executive route must keep: the report permission of its domain, the branch scope of a user
/// without branches.access_all, and the own-branch default. One row of `Routes` per route: a phase that adds a
/// route adds its row here and the three guards run on it.
[Collection(nameof(PostgresCollection))]
public class ExecutiveRouteGuardTests : ExecutiveDashboardTestBase
{
    /// Route: the path under api/dashboard/executive. Extra: the query string after the period (starts with `&`).
    /// Permission: the report permission the route needs besides dashboard.executive. ReadTotal: the figure of the
    /// response that must cover the own branch only (2,500 for the main branch of the report dataset).
    private sealed record GuardedRoute(
        string Route, string Extra, string Permission, Func<HttpResponseMessage, Task<decimal>> ReadTotal);

    private static readonly GuardedRoute[] Routes =
    {
        new("sales/summary", "", Permissions.Reports.Profit,
            async response => (await ReadDataAsync<SalesSummaryDto>(response)).Revenue),
        new("sales/series", "&granularity=Day", Permissions.Reports.Profit,
            async response => (await ReadDataAsync<SalesSeriesDto>(response)).Points.Sum(point => point.Revenue)),
        new("sales/top", "&by=Customer", Permissions.Reports.Profit,
            async response => (await ReadDataAsync<List<SalesTopRowDto>>(response)).Sum(row => row.Revenue)),
    };

    public ExecutiveRouteGuardTests(PostgresFixture pg) : base(pg) { }

    public static IEnumerable<object[]> RouteNames => Routes.Select(route => new object[] { route.Route });

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
        var ids = await ReportDataset.SeedAsync(this);
        var user = await BranchUserAsync(ids, route);

        (await GetExecRawAsync(user, UrlOf(route, $"&branchId={ids.BranchBId}")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(RouteNames))]
    public async Task Own_branch_by_default(string routeName)
    {
        var route = RouteOf(routeName);
        var ids = await ReportDataset.SeedAsync(this);
        var user = await BranchUserAsync(ids, route);

        var total = await route.ReadTotal(await GetExecRawAsync(user, UrlOf(route)));

        // The user may read the main branch only: the 9,999 sold in branch B would make it 12,499.
        total.Should().Be(2_500m);
    }

    private static GuardedRoute RouteOf(string routeName) => Routes.Single(route => route.Route == routeName);

    private static string UrlOf(GuardedRoute route, string more = "") => $"{route.Route}?{OctoberRange}{route.Extra}{more}";

    /// A user without branches.access_all whose default branch is the dataset's main branch.
    private Task<HttpClient> BranchUserAsync(ReportIds ids, GuardedRoute route) =>
        CreateClientWithPermissionsAsync("branch_user", ids.MainBranchId, Permissions.Dashboard.Executive, route.Permission);
}
