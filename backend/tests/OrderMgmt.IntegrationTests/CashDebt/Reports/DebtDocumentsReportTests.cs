using FluentAssertions;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Công nợ theo chứng từ (Phase 01).
[Collection(nameof(PostgresCollection))]
public class DebtDocumentsReportTests : CashDebtTestBase
{
    public DebtDocumentsReportTests(PostgresFixture pg) : base(pg) { }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public async Task Receivable_documents_at_month_end()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var report = await GetDocumentsAsync(client, "side=Receivable&at=2026-10-31");

        report.Side.Should().Be(DebtSide.Receivable);
        report.At.Should().Be(D(10, 31));
        var group = report.Groups.Should().ContainSingle().Which;
        group.PartnerId.Should().Be(ids.Kh1Id);
        group.PartnerCode.Should().Be(ids.Kh1Code);
        group.Items.Select(i => (i.DocCode, i.SourceType, i.SourceId, i.DocDate, i.DueDate, i.Amount, i.Settled, i.Remaining,
                i.DaysOutstanding, i.DaysOverdue))
            .Should().Equal(
                ("HD-OLD", DebtSourceType.Opening, ids.OpeningHdOldId, D(9, 5), D(9, 20), 5_000m, 4_000m, 1_000m, 56, 41),
                ("PX00001", DebtSourceType.StockOut, ids.SaleId, D(10, 3), D(10, 3), 3_000m, 1_000m, 2_000m, 28, 28));
        group.Unallocated.Select(u => (u.DocCode, u.SourceType, u.SourceId, u.DocDate, u.Amount, u.Settled, u.Remaining))
            .Should().Equal(
                ("TT-OLD", DebtSourceType.Opening, ids.OpeningTtOldId, D(9, 15), 1_000m, 0m, 1_000m),
                ("PN00001", DebtSourceType.StockIn, ids.Return6Id, D(10, 8), 500m, 0m, 500m));
        group.TotalRemaining.Should().Be(3_000m);
        group.TotalUnallocated.Should().Be(1_500m);
        group.Balance.Should().Be(1_500m);
        report.TotalRemaining.Should().Be(3_000m);
        report.TotalUnallocated.Should().Be(1_500m);
        report.TotalBalance.Should().Be(1_500m);
    }

    [Fact]
    public async Task Receivable_documents_on_the_posting_day()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetDocumentsAsync(MainBranchClient(ids), "side=Receivable&at=2026-10-03");

        var group = report.Groups.Should().ContainSingle().Which;
        group.Items.Select(i => (i.DocCode, i.Remaining, i.DaysOutstanding, i.DaysOverdue)).Should().Equal(
            ("HD-OLD", 5_000m, 28, 13),
            ("PX00001", 2_000m, 0, 0));
        group.Unallocated.Select(u => (u.DocCode, u.Remaining)).Should().Equal(("TT-OLD", 1_000m));
        report.TotalBalance.Should().Be(6_000m);
    }

    [Fact]
    public async Task Receivable_documents_before_the_opening_date()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetDocumentsAsync(MainBranchClient(ids), "side=Receivable&at=2026-09-01");

        var group = report.Groups.Should().ContainSingle().Which;
        group.Items.Select(i => (i.DocCode, i.Remaining, i.DaysOutstanding, i.DaysOverdue)).Should().Equal(
            ("HD-OLD", 5_000m, 0, 0));
        group.Unallocated.Select(u => (u.DocCode, u.Remaining)).Should().Equal(("TT-OLD", 1_000m));
        report.TotalBalance.Should().Be(4_000m);
    }

    [Fact]
    public async Task Receivable_documents_at_an_earlier_date()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetDocumentsAsync(MainBranchClient(ids), "side=Receivable&at=2026-10-04");

        var group = report.Groups.Should().ContainSingle().Which;
        group.Items.Select(i => (i.DocCode, i.Settled, i.Remaining, i.DaysOutstanding, i.DaysOverdue)).Should().Equal(
            ("HD-OLD", 0m, 5_000m, 29, 14),
            ("PX00001", 1_000m, 2_000m, 1, 1));
        group.Unallocated.Select(u => (u.DocCode, u.Remaining)).Should().Equal(("TT-OLD", 1_000m));
        report.TotalBalance.Should().Be(6_000m);
    }

    [Fact]
    public async Task Payable_documents()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var report = await GetDocumentsAsync(MainBranchClient(ids), "side=Payable&at=2026-10-31");

        var group = report.Groups.Should().ContainSingle().Which;
        group.PartnerId.Should().Be(ids.Ncc1Id);
        var item = group.Items.Should().ContainSingle().Which;
        item.DocCode.Should().Be("PN00002");
        item.SourceId.Should().Be(ids.Purchase7Id);
        item.Amount.Should().Be(6_000m);
        item.Settled.Should().Be(2_000m);
        item.Remaining.Should().Be(4_000m);
        item.DueDate.Should().Be(D(10, 10));
        item.DaysOverdue.Should().Be(21);
        group.Unallocated.Select(u => (u.DocCode, u.Remaining)).Should().Equal(("PC00002", 3_000m));
        report.TotalBalance.Should().Be(1_000m);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<DebtDocumentsReportDto> GetDocumentsAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtDocumentsReportDto>(await client.GetAsync($"/api/reports/debt-documents?{query}"));
}
