using OrderMgmt.Domain.Constants;
using OrderMgmt.Application.Common.Models;
using System.Net.Http.Json;
using System.Net;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Tuổi nợ (Phase 02).
[Collection(nameof(PostgresCollection))]
public class DebtAgingReportTests : CashDebtTestBase
{
    public DebtAgingReportTests(PostgresFixture pg) : base(pg) { }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public async Task Receivable_aging_default_buckets()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetAgingAsync(MainBranchClient(ids), "side=Receivable&at=2026-10-31");

        report.Side.Should().Be(DebtSide.Receivable);
        report.At.Should().Be(D(10, 31));
        report.Buckets.Select(b => (b.From, b.To)).Should().Equal((0, 30), (31, 60), (61, 90), (91, (int?)null));
        var row = report.Rows.Should().ContainSingle().Which;
        row.PartnerId.Should().Be(ids.Kh1Id);
        row.PartnerCode.Should().Be(ids.Kh1Code);
        row.Amounts.Should().Equal(2_000m, 1_000m, 0m, 0m);
        row.TotalOpen.Should().Be(3_000m);
        row.Unallocated.Should().Be(1_500m);
        row.Balance.Should().Be(1_500m);
        report.Totals.PartnerId.Should().BeNull();
        report.Totals.Amounts.Should().Equal(2_000m, 1_000m, 0m, 0m);
        report.Totals.TotalOpen.Should().Be(3_000m);
        report.Totals.Unallocated.Should().Be(1_500m);
        report.Totals.Balance.Should().Be(1_500m);
    }

    [Fact]
    public async Task Custom_buckets()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetAgingAsync(MainBranchClient(ids), "side=Receivable&at=2026-10-31&buckets=10,20");

        report.Buckets.Select(b => (b.From, b.To)).Should().Equal((0, 10), (11, 20), (21, (int?)null));
        report.Rows.Should().ContainSingle().Which.Amounts.Should().Equal(0m, 0m, 3_000m);
    }

    [Fact]
    public async Task Payable_aging()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetAgingAsync(MainBranchClient(ids), "side=Payable&at=2026-10-31");

        var row = report.Rows.Should().ContainSingle().Which;
        row.PartnerId.Should().Be(ids.Ncc1Id);
        row.Amounts.Should().Equal(4_000m, 0m, 0m, 0m);
        row.TotalOpen.Should().Be(4_000m);
        row.Unallocated.Should().Be(3_000m);
        row.Balance.Should().Be(1_000m);
    }

    [Fact]
    public async Task Opening_debt_before_the_opening_date_and_empty_result()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var early = await GetAgingAsync(client, "side=Receivable&at=2026-09-01");
        var row = early.Rows.Should().ContainSingle().Which;
        row.Amounts.Should().Equal(5_000m, 0m, 0m, 0m);
        row.TotalOpen.Should().Be(5_000m);
        row.Unallocated.Should().Be(1_000m);
        row.Balance.Should().Be(4_000m);

        var empty = await GetAgingAsync(client, "side=Payable&at=2026-10-04");
        empty.Rows.Should().BeEmpty();
        empty.Totals.Amounts.Should().Equal(0m, 0m, 0m, 0m);
        empty.Totals.TotalOpen.Should().Be(0m);
    }

    [Fact]
    public async Task Totals_equal_documents_report()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            foreach (var at in new[] { D(10, 3), D(10, 4), D(10, 31) })
            {
                var query = $"side={side}&at={at:yyyy-MM-dd}";
                var aging = await GetAgingAsync(client, query);
                var documents = await ReadDataAsync<DebtDocumentsReportDto>(
                    await client.GetAsync($"/api/reports/debt-documents?{query}"));
                var context = $"{side} at {at:yyyy-MM-dd}";

                aging.Totals.TotalOpen.Should().Be(documents.TotalRemaining, context);
                aging.Totals.Unallocated.Should().Be(documents.TotalUnallocated, context);
                aging.Totals.Balance.Should().Be(documents.TotalBalance, context);
                aging.Rows.Select(r => r.PartnerId!.Value).Should()
                    .BeEquivalentTo(documents.Groups.Select(g => g.PartnerId), context);
                foreach (var row in aging.Rows)
                    row.Balance.Should().Be(documents.Groups.Single(g => g.PartnerId == row.PartnerId).Balance, context);
            }
        }
    }

    [Fact]
    public async Task Validation_and_permission()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        await AssertBadRequestKeyAsync(client, "side=Receivable&at=2026-10-31&buckets=60,30", "buckets");
        await AssertBadRequestKeyAsync(client, "side=Receivable&at=2026-10-31&buckets=abc", "buckets");
        await AssertBadRequestKeyAsync(client, "side=Receivable&at=2026-10-31&buckets=2147483647", "buckets");
        await AssertBadRequestKeyAsync(client, "side=Receivable", "at");

        var cashOnly = await CreateClientWithPermissionsAsync("da_cash_" + ids.Kh1Code[3..], ids.MainBranchId, Permissions.Reports.Cash);
        (await cashOnly.GetAsync("/api/reports/debt-aging?side=Receivable&at=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task AssertBadRequestKeyAsync(HttpClient client, string query, string key)
    {
        var response = await client.GetAsync($"/api/reports/debt-aging?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, query);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey(key, query);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<DebtAgingReportDto> GetAgingAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtAgingReportDto>(await client.GetAsync($"/api/reports/debt-aging?{query}"));
}
