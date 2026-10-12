using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Sổ chi tiết công nợ (Phase 09).
[Collection(nameof(PostgresCollection))]
public class DebtLedgerReportTests : CashDebtTestBase
{
    public DebtLedgerReportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Ledger_rows_and_running_balance()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var report = await GetLedgerAsync(client, $"side=Receivable&partnerId={ids.Kh1Id}&from=2026-10-01&to=2026-10-31");

        report.Side.Should().Be(DebtSide.Receivable);
        report.PartnerId.Should().Be(ids.Kh1Id);
        report.PartnerCode.Should().Be(ids.Kh1Code);
        report.PartnerName.Should().Be($"Đối tượng {ids.Kh1Code}");
        report.From.Should().Be(new DateOnly(2026, 10, 1));
        report.To.Should().Be(new DateOnly(2026, 10, 31));

        // P29: the opening debts (HD-OLD 5,000, TT-OLD −1,000) are the opening balance, not rows.
        report.OpeningBalance.Should().Be(4_000m);
        report.OpeningDebit.Should().Be(4_000m);
        report.OpeningCredit.Should().Be(0m);

        // Same instant 10-03 09:00: the sale (StockOut) sorts before its automatic receipt (Receipt).
        // The cancelled receipt PT00003 has no entry; branch B's sale is in another branch.
        report.Rows.Select(r => (r.DocCode, r.SourceType, r.SourceId, r.Increase, r.Decrease, r.RunningBalance))
            .Should().Equal(
                ("PX00001", DebtSourceType.StockOut, ids.SaleId, 3_000m, 0m, 7_000m),
                ("PT00001", DebtSourceType.Receipt, ids.AutoReceiptId, 0m, 1_000m, 6_000m),
                ("PT00002", DebtSourceType.Receipt, ids.Receipt5Id, 0m, 4_000m, 2_000m),
                ("PN00001", DebtSourceType.StockIn, ids.Return6Id, 0m, 500m, 1_500m));
        report.Rows.Select(r => r.PostedAt).Should().Equal(
            Vn("2026-10-03 09:00"), Vn("2026-10-03 09:00"), Vn("2026-10-05 09:00"), Vn("2026-10-08 09:00"));
        report.Rows.Select(r => r.DocDate).Should().Equal(
            new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 8));

        // Each row is the ledger entry of its source, with its description snapshot.
        var entries = await InDbAsync(db => db.DebtEntries.AsNoTracking()
            .Where(e => e.BranchId == ids.MainBranchId && e.PartnerId == ids.Kh1Id && e.Side == DebtSide.Receivable)
            .ToDictionaryAsync(e => e.SourceId, e => new { e.Id, e.Description }));
        report.Rows.Select(r => (r.EntryId, r.Description))
            .Should().Equal(report.Rows.Select(r => (entries[r.SourceId].Id, entries[r.SourceId].Description)));

        report.TotalIncrease.Should().Be(3_000m);
        report.TotalDecrease.Should().Be(5_500m);
        report.ClosingBalance.Should().Be(1_500m);
        report.ClosingDebit.Should().Be(1_500m);
        report.ClosingCredit.Should().Be(0m);

        // From 10-06 everything before is opening (4,000 + 3,000 − 1,000 − 4,000 = 2,000).
        var later = await GetLedgerAsync(client, $"side=Receivable&partnerId={ids.Kh1Id}&from=2026-10-06&to=2026-10-31");
        later.OpeningBalance.Should().Be(2_000m);
        later.Rows.Select(r => (r.DocCode, r.RunningBalance)).Should().Equal(("PN00001", 1_500m));
        later.ClosingBalance.Should().Be(1_500m);
    }

    [Fact]
    public async Task Closing_matches_balance_report_and_query_service()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var windows = new[]
        {
            (From: new DateOnly(2026, 10, 1), To: new DateOnly(2026, 10, 31)),
            (From: new DateOnly(2026, 10, 1), To: new DateOnly(2026, 10, 5)),
            (From: new DateOnly(2026, 10, 9), To: new DateOnly(2026, 10, 11)),
        };
        foreach (var (from, to) in windows)
        foreach (var partnerId in new[] { ids.Kh1Id, ids.Ncc1Id })
        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            var window = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
            var because = $"{partnerId} {side} {window}";
            var ledger = await GetLedgerAsync(client, $"side={side}&partnerId={partnerId}&{window}");
            var balance = await ReadDataAsync<DebtBalanceReportDto>(
                await client.GetAsync($"/api/reports/debt-balance?side={side}&partnerId={partnerId}&{window}"));
            var row = balance.Rows.SingleOrDefault(r => r.PartnerId == partnerId);

            var expected = await BalanceAsync(ids.MainBranchId, partnerId, side, VnTime.StartOfNextDay(to));
            ledger.ClosingBalance.Should().Be(expected, because);
            ledger.OpeningBalance.Should().Be(
                ledger.ClosingBalance - ledger.TotalIncrease + ledger.TotalDecrease, because);
            (ledger.Rows.Count == 0 ? ledger.OpeningBalance : ledger.Rows[^1].RunningBalance)
                .Should().Be(ledger.ClosingBalance, because);
            ledger.ClosingDebit.Should().Be(row?.ClosingDebit ?? 0m, because);
            ledger.ClosingCredit.Should().Be(row?.ClosingCredit ?? 0m, because);
            ledger.OpeningDebit.Should().Be(row?.OpeningDebit ?? 0m, because);
            ledger.OpeningCredit.Should().Be(row?.OpeningCredit ?? 0m, because);
            ledger.TotalIncrease.Should().Be(row?.Increase ?? 0m, because);
            ledger.TotalDecrease.Should().Be(row?.Decrease ?? 0m, because);
        }

        // NCC1 payable is a Dư Có (P24): opening 0, NMH +6,000, PC00001 −2,000, PC00002 −3,000 → 1,000.
        var payable = await GetLedgerAsync(client, $"side=Payable&partnerId={ids.Ncc1Id}&from=2026-10-01&to=2026-10-31");
        payable.Rows.Select(r => (r.DocCode, r.RunningBalance))
            .Should().Equal(("PN00002", 6_000m), ("PC00001", 4_000m), ("PC00002", 1_000m));
        payable.ClosingDebit.Should().Be(0m);
        payable.ClosingCredit.Should().Be(1_000m);
    }

    [Fact]
    public async Task Validation_and_permission()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        const string october = "from=2026-10-01&to=2026-10-31";

        (await client.GetAsync($"/api/reports/debt-ledger?side=Receivable&{october}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync($"/api/reports/debt-ledger?partnerId={ids.Kh1Id}&{october}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync(
                $"/api/reports/debt-ledger?side=Receivable&partnerId={ids.Kh1Id}&from=2026-10-31&to=2026-10-01"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // An unknown partner has no ledger to show.
        (await client.GetAsync($"/api/reports/debt-ledger?side=Receivable&partnerId={Guid.NewGuid()}&{october}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Permission: WAREHOUSE has no reports.debt.
        var warehouse = await CreateClientForRoleAsync("dlr_wh_" + ids.Kh1Code[3..], RoleCodes.Warehouse, ids.MainBranchId);
        (await warehouse.GetAsync($"/api/reports/debt-ledger?side=Receivable&partnerId={ids.Kh1Id}&{october}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<DebtLedgerReportDto> GetLedgerAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtLedgerReportDto>(await client.GetAsync($"/api/reports/debt-ledger?{query}"));

    private Task<decimal> BalanceAsync(Guid branchId, Guid partnerId, DebtSide side, DateTimeOffset at) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>()
            .BalanceAsync(branchId, partnerId, side, at, ct));
}
