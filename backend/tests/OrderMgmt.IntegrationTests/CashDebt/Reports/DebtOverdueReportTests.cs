using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Nợ quá hạn (Phase 02).
[Collection(nameof(PostgresCollection))]
public class DebtOverdueReportTests : CashDebtTestBase
{
    public DebtOverdueReportTests(PostgresFixture pg) : base(pg) { }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public async Task Receivable_overdue_at_month_end()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetOverdueAsync(MainBranchClient(ids), "side=Receivable&at=2026-10-31");

        report.Side.Should().Be(DebtSide.Receivable);
        report.At.Should().Be(D(10, 31));
        report.Buckets.Select(b => (b.From, b.To)).Should().Equal((1, 30), (31, 60), (61, 90), (91, (int?)null));
        var group = report.Groups.Should().ContainSingle().Which;
        group.PartnerId.Should().Be(ids.Kh1Id);
        group.PartnerCode.Should().Be(ids.Kh1Code);
        group.Items.Should().HaveCount(2);
        var old = group.Items[0];
        old.DocCode.Should().Be("HD-OLD");
        old.DueDate.Should().Be(D(9, 20));
        old.Remaining.Should().Be(1_000m);
        old.DaysOverdue.Should().Be(41);
        old.BucketIndex.Should().Be(1);
        var sale = group.Items[1];
        sale.DocCode.Should().Be("PX00001");
        sale.DueDate.Should().Be(D(10, 3));
        sale.Remaining.Should().Be(2_000m);
        sale.DaysOverdue.Should().Be(28);
        sale.BucketIndex.Should().Be(0);
        group.Amounts.Should().Equal(2_000m, 1_000m, 0m, 0m);
        group.TotalOverdue.Should().Be(3_000m);
        group.Unallocated.Should().Be(1_500m);
        report.TotalAmounts.Should().Equal(2_000m, 1_000m, 0m, 0m);
        report.TotalOverdue.Should().Be(3_000m);
        report.TotalUnallocated.Should().Be(1_500m);
    }

    [Fact]
    public async Task Due_today_is_not_overdue()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetOverdueAsync(MainBranchClient(ids), "side=Receivable&at=2026-10-03");

        var group = report.Groups.Should().ContainSingle().Which;
        var item = group.Items.Should().ContainSingle().Which;
        item.DocCode.Should().Be("HD-OLD");
        item.Remaining.Should().Be(5_000m);
        item.DaysOverdue.Should().Be(13);
    }

    [Fact]
    public async Task Opening_debts_count_before_the_opening_date()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetOverdueAsync(MainBranchClient(ids), "side=Receivable&at=2026-09-25");

        var item = report.Groups.Should().ContainSingle().Which.Items.Should().ContainSingle().Which;
        item.DocCode.Should().Be("HD-OLD");
        item.Remaining.Should().Be(5_000m);
        item.DaysOverdue.Should().Be(5);
    }

    [Fact]
    public async Task Payable_overdue()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var none = await GetOverdueAsync(client, "side=Payable&at=2026-10-10");
        none.Groups.Should().BeEmpty();
        none.TotalAmounts.Should().Equal(0m, 0m, 0m, 0m);
        none.TotalOverdue.Should().Be(0m);

        var report = await GetOverdueAsync(client, "side=Payable&at=2026-10-31");
        var group = report.Groups.Should().ContainSingle().Which;
        group.PartnerId.Should().Be(ids.Ncc1Id);
        var item = group.Items.Should().ContainSingle().Which;
        item.DocCode.Should().Be("PN00002");
        item.Remaining.Should().Be(4_000m);
        item.DaysOverdue.Should().Be(21);
        group.Unallocated.Should().Be(3_000m);
    }

    [Fact]
    public async Task Validation_and_permission()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        await AssertBadRequestKeyAsync(client, "side=Receivable&at=2026-10-31&buckets=0,30", "buckets");
        await AssertBadRequestKeyAsync(client, "side=Receivable&at=2026-10-31&buckets=1,2,3,4,5,6,7", "buckets");
        await AssertBadRequestKeyAsync(client, "side=Receivable", "at");

        var cashOnly = await CreateClientWithPermissionsAsync("do_cash_" + ids.Kh1Code[3..], ids.MainBranchId, Permissions.Reports.Cash);
        (await cashOnly.GetAsync("/api/reports/debt-overdue?side=Receivable&at=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task AssertBadRequestKeyAsync(HttpClient client, string query, string key)
    {
        var response = await client.GetAsync($"/api/reports/debt-overdue?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, query);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey(key, query);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<DebtOverdueReportDto> GetOverdueAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtOverdueReportDto>(await client.GetAsync($"/api/reports/debt-overdue?{query}"));
}
