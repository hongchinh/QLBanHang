using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.Debt.Models;
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

    [Fact]
    public async Task Documents_balance_equals_balance_report()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        // A manual matching batch: PX00001 (posted 10-03) against TT-OLD. EffectiveAt is 10-03 09:00.
        var saleEntry = (await EntriesOfAsync(DebtSourceType.StockOut, ids.SaleId)).Single();
        var match = await client.PostAsJsonAsync("/api/debt/settlements", new CreateSettlementBatchRequest
        {
            PartnerId = ids.Kh1Id,
            Side = DebtSide.Receivable,
            Items = new List<SettlementItemRequest>
            {
                new() { IncreaseEntryId = saleEntry.Id, DecreaseEntryId = ids.OpeningTtOldEntryId, Amount = 1_000m },
            },
        }, TestJson.Options);
        match.IsSuccessStatusCode.Should().BeTrue(await match.Content.ReadAsStringAsync());

        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            foreach (var at in new[] { D(10, 2), D(10, 3), D(10, 4), D(10, 31) })
            {
                var documents = await GetDocumentsAsync(client, $"side={side}&at={at:yyyy-MM-dd}");
                var balance = await ReadDataAsync<DebtBalanceReportDto>(await client.GetAsync(
                    $"/api/reports/debt-balance?side={side}&from=2026-10-01&to={at:yyyy-MM-dd}"));
                var context = $"{side} at {at:yyyy-MM-dd}";

                decimal Signed(DebtBalanceRowDto r) => side == DebtSide.Receivable
                    ? r.ClosingDebit - r.ClosingCredit
                    : r.ClosingCredit - r.ClosingDebit;

                foreach (var row in balance.Rows)
                {
                    var group = documents.Groups.SingleOrDefault(g => g.PartnerId == row.PartnerId);
                    if (Signed(row) == 0m)
                    {
                        (group?.Balance ?? 0m).Should().Be(0m, context);
                        continue;
                    }

                    group.Should().NotBeNull(context);
                    group!.Balance.Should().Be(Signed(row), context);
                }

                documents.Groups.Select(g => g.PartnerId).Should()
                    .BeSubsetOf(balance.Rows.Select(r => r.PartnerId!.Value), context);
                documents.TotalBalance.Should().Be(Signed(balance.Totals), context);
            }
        }

        // The matched state.
        foreach (var at in new[] { D(10, 31), D(10, 4), D(10, 3) })
        {
            var report = await GetDocumentsAsync(client, $"side=Receivable&at={at:yyyy-MM-dd}");
            var group = report.Groups.Should().ContainSingle().Which;
            group.Items.Single(i => i.DocCode == "PX00001").Remaining.Should().Be(1_000m, $"at {at}");
            group.Unallocated.Select(u => u.DocCode).Should().NotContain("TT-OLD", $"at {at}");
        }

        (await GetDocumentsAsync(client, "side=Receivable&at=2026-10-31")).TotalBalance.Should().Be(1_500m);
        (await GetDocumentsAsync(client, "side=Receivable&at=2026-10-04")).TotalBalance.Should().Be(6_000m);
        (await GetDocumentsAsync(client, "side=Receivable&at=2026-10-03")).TotalBalance.Should().Be(6_000m);

        // At 10-02 the manual settlement is not counted: its PX00001 entry is posted on 10-03.
        var early = (await GetDocumentsAsync(client, "side=Receivable&at=2026-10-02")).Groups.Should().ContainSingle().Which;
        early.Items.Select(i => (i.DocCode, i.Remaining)).Should().Equal(("HD-OLD", 5_000m));
        early.Unallocated.Select(u => (u.DocCode, u.Remaining)).Should().Equal(("TT-OLD", 1_000m));
        early.Balance.Should().Be(4_000m);
    }

    [Fact]
    public async Task Opening_to_opening_settlement_counts_before_the_opening_date()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        // HD-OLD against TT-OLD: both are Opening entries, so the match counts even before the opening date (B3).
        var match = await client.PostAsJsonAsync("/api/debt/settlements", new CreateSettlementBatchRequest
        {
            PartnerId = ids.Kh1Id,
            Side = DebtSide.Receivable,
            Items = new List<SettlementItemRequest>
            {
                new() { IncreaseEntryId = ids.OpeningHdOldEntryId, DecreaseEntryId = ids.OpeningTtOldEntryId, Amount = 1_000m },
            },
        }, TestJson.Options);
        match.IsSuccessStatusCode.Should().BeTrue(await match.Content.ReadAsStringAsync());

        var early = await GetDocumentsAsync(client, "side=Receivable&at=2026-09-25");
        var group = early.Groups.Should().ContainSingle().Which;
        group.Items.Select(i => (i.DocCode, i.Amount, i.Settled, i.Remaining, i.DaysOutstanding, i.DaysOverdue))
            .Should().Equal(("HD-OLD", 5_000m, 1_000m, 4_000m, 20, 5));
        group.Unallocated.Should().BeEmpty();
        group.Balance.Should().Be(4_000m);

        var late = await GetDocumentsAsync(client, "side=Receivable&at=2026-10-31");
        var lateGroup = late.Groups.Should().ContainSingle().Which;
        lateGroup.Items.Select(i => (i.DocCode, i.Remaining)).Should().Equal(("PX00001", 2_000m));
        lateGroup.Unallocated.Select(u => (u.DocCode, u.Remaining)).Should().Equal(("PN00001", 500m));
        lateGroup.Balance.Should().Be(1_500m);
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
