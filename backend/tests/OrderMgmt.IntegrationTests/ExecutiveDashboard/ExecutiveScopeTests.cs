using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Dashboards.Executive.Models;
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
        var ids = await ReportDataset.SeedAsync(this);

        var response = await GetExecRawAsync(MainClient(ids), $"sales/summary?{OctoberRange}&branchId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task To_before_from_is_rejected()
    {
        var ids = await ReportDataset.SeedAsync(this);

        await AssertBadRequestKeyAsync(MainClient(ids), "sales/summary?from=2026-10-10&to=2026-10-01", "to",
            "Đến ngày phải sau hoặc bằng từ ngày.");
    }

    [Fact]
    public async Task Range_over_three_years_is_rejected()
    {
        var ids = await ReportDataset.SeedAsync(this);

        await AssertBadRequestKeyAsync(MainClient(ids), "sales/summary?from=2020-01-01&to=2026-10-01", "to",
            "Khoảng thời gian tối đa là 3 năm.");
    }

    [Fact]
    public async Task Only_from_far_in_the_past_is_rejected()
    {
        var ids = await ReportDataset.SeedAsync(this);

        await AssertBadRequestKeyAsync(MainClient(ids), "sales/summary?from=2000-01-01", "to",
            "Khoảng thời gian tối đa là 3 năm.");
    }

    [Fact]
    public async Task Only_to_is_accepted()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetSummaryAsync(MainClient(ids), "to=2026-09-20");

        summary.From.Should().Be(new DateOnly(2026, 9, 1));
        summary.To.Should().Be(new DateOnly(2026, 9, 20));
    }

    [Fact]
    public async Task Only_from_is_accepted()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetSummaryAsync(MainClient(ids), $"from=2026-10-01&branchId={ids.MainBranchId}");

        summary.From.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public async Task Defaults_to_the_current_month()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetSummaryAsync(MainClient(ids), string.Empty);

        summary.From.Should().Be(new DateOnly(summary.To.Year, summary.To.Month, 1));
        summary.To.Should().BeOnOrAfter(summary.From);
    }

    /// A user without branches.access_all whose default branch is the dataset's main branch.
    private Task<HttpClient> BranchUserAsync(ReportIds ids) =>
        CreateClientWithPermissionsAsync("branch_user", ids.MainBranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Profit);

    private static Task<SalesSummaryDto> GetSummaryAsync(HttpClient client, string query) =>
        GetExecAsync<SalesSummaryDto>(client, $"sales/summary?{query}");
}
