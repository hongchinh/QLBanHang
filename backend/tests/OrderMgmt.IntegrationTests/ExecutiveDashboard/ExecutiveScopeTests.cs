using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// Branch scope and query validation, through sales/summary (Phase 01, Task 1.4).
[Collection(nameof(PostgresCollection))]
public class ExecutiveScopeTests : ExecutiveDashboardTestBase
{
    public ExecutiveScopeTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task User_without_access_all_sees_own_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await BranchUserAsync(ids);

        var summary = await GetSummaryAsync(user, OctoberRange);

        summary.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task User_without_access_all_may_name_own_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await BranchUserAsync(ids);

        var summary = await GetSummaryAsync(user, $"{OctoberRange}&branchId={ids.MainBranchId}");

        summary.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task User_without_access_all_cannot_name_another_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await BranchUserAsync(ids);

        var response = await GetExecRawAsync(user, $"sales/summary?{OctoberRange}&branchId={ids.BranchBId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Message
            .Should().Be("Bạn không có quyền xem chi nhánh này.");
    }

    [Fact]
    public async Task Header_does_not_widen_the_scope()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await BranchUserAsync(ids);
        UseBranch(user, ids.BranchBId);

        var summary = await GetSummaryAsync(user, OctoberRange);

        summary.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Unknown_branch_returns_404()
    {
        var response = await GetExecRawAsync(CloneAdminClient(), $"sales/summary?{OctoberRange}&branchId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task To_before_from_is_rejected()
    {
        await AssertBadRequestKeyAsync(CloneAdminClient(), "sales/summary?from=2026-10-10&to=2026-10-01", "to",
            "Đến ngày phải sau hoặc bằng từ ngày.");
    }

    [Fact]
    public async Task Range_over_three_years_is_rejected()
    {
        await AssertBadRequestKeyAsync(CloneAdminClient(), "sales/summary?from=2020-01-01&to=2026-10-01", "to",
            "Khoảng thời gian tối đa là 3 năm.");
    }

    [Fact]
    public async Task Only_from_far_in_the_past_is_rejected()
    {
        await AssertBadRequestKeyAsync(CloneAdminClient(), "sales/summary?from=2000-01-01", "to",
            "Khoảng thời gian tối đa là 3 năm.");
    }

    [Fact]
    public async Task Dates_outside_the_accepted_range_are_rejected()
    {
        var client = CloneAdminClient();

        await AssertBadRequestKeyAsync(client, "sales/summary?from=1999-12-31", "from", "Từ ngày không hợp lệ.");
        await AssertBadRequestKeyAsync(client, "sales/summary?to=2101-01-01", "to", "Đến ngày không hợp lệ.");
        await AssertBadRequestKeyAsync(client, "sales/summary?at=1999-12-31", "at", "Ngày xem số dư không hợp lệ.");

        // Dates at the edge of the calendar cannot build a period: the date rules answer, the server does not fail.
        await AssertBadRequestKeyAsync(client, "sales/summary?from=0001-01-01", "from", "Từ ngày không hợp lệ.");
        await AssertBadRequestKeyAsync(client, "sales/summary?to=9999-12-31", "to", "Đến ngày không hợp lệ.");
    }

    [Fact]
    public async Task Only_to_is_accepted()
    {
        var summary = await GetSummaryAsync(CloneAdminClient(), "to=2026-09-20");

        summary.From.Should().Be(new DateOnly(2026, 9, 1));
        summary.To.Should().Be(new DateOnly(2026, 9, 20));
    }

    [Fact]
    public async Task Only_from_is_accepted()
    {
        var ids = await ReportDataset.SeedAsync(this);
        // Day 1 of the current VN month stays inside the 1,100-day limit whatever day the test runs on.
        var today = VnTime.ToVnDate(DateTimeOffset.UtcNow);
        var from = new DateOnly(today.Year, today.Month, 1);

        var summary = await GetSummaryAsync(MainClient(ids), $"from={from:yyyy-MM-dd}&branchId={ids.MainBranchId}");

        summary.From.Should().Be(from);
    }

    [Fact]
    public async Task Defaults_to_the_current_month()
    {
        var today = VnTime.ToVnDate(DateTimeOffset.UtcNow);

        var summary = await GetSummaryAsync(CloneAdminClient(), string.Empty);

        // A UTC "today" would be a day behind between 00:00 and 07:00 in Vietnam.
        summary.To.Should().Be(today);
        summary.From.Should().Be(new DateOnly(today.Year, today.Month, 1));
    }

    /// A user without branches.access_all whose default branch is the dataset's main branch.
    private Task<HttpClient> BranchUserAsync(ReportIds ids) =>
        CreateClientWithPermissionsAsync("branch_user", ids.MainBranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Profit);
}
