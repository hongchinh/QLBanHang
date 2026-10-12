using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Biên bản đối chiếu công nợ (Phase 03).
[Collection(nameof(PostgresCollection))]
public class DebtReconciliationReportTests : CashDebtTestBase
{
    public DebtReconciliationReportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Customer_only_partner()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var report = await GetAsync(client, $"partnerId={ids.Kh1Id}&from=2026-10-01&to=2026-10-31");

        report.PartnerId.Should().Be(ids.Kh1Id);
        report.PartnerCode.Should().Be(ids.Kh1Code);
        report.PartnerName.Should().NotBeNullOrEmpty();
        report.BranchName.Should().Be("Chi nhánh RD" + ids.Kh1Code[3..]);
        report.From.Should().Be(new DateOnly(2026, 10, 1));
        report.To.Should().Be(new DateOnly(2026, 10, 31));
        var ledger = await ReadDataAsync<DebtLedgerReportDto>(await client.GetAsync(
            $"/api/reports/debt-ledger?side=Receivable&partnerId={ids.Kh1Id}&from=2026-10-01&to=2026-10-31"));
        ledger.ClosingBalance.Should().Be(1_500m);
        var side = report.Sides.Should().ContainSingle().Which;
        side.Should().BeEquivalentTo(ledger, o => o.WithStrictOrdering());
        report.NetBalance.Should().BeNull();
    }

    [Fact]
    public async Task Partner_on_both_sides()
    {
        var (_, client, both) = await SeedBothAsync();

        var october = await GetAsync(client, $"partnerId={both}&from=2026-10-01&to=2026-10-31");
        october.Sides.Select(s => (s.Side, s.ClosingBalance)).Should()
            .Equal((DebtSide.Receivable, 1_000m), (DebtSide.Payable, 400m));
        october.NetBalance.Should().Be(600m);

        var toTwentieth = await GetAsync(client, $"partnerId={both}&from=2026-10-01&to=2026-10-20");
        toTwentieth.Sides.Select(s => s.Side).Should().Equal(DebtSide.Receivable, DebtSide.Payable);
        var payable = toTwentieth.Sides[1];
        payable.Rows.Should().BeEmpty();
        payable.ClosingBalance.Should().Be(0m);
        toTwentieth.Sides[0].ClosingBalance.Should().Be(1_000m);
        toTwentieth.NetBalance.Should().Be(1_000m);
    }

    [Fact]
    public async Task Partner_on_both_sides_after_an_offset()
    {
        var (_, client, both) = await SeedBothAsync();
        var saleEntry = (await InDbAsync(db => db.DebtEntries.AsNoTracking()
            .Where(e => e.PartnerId == both && e.Side == DebtSide.Receivable).SingleAsync()));
        var purchaseEntry = (await InDbAsync(db => db.DebtEntries.AsNoTracking()
            .Where(e => e.PartnerId == both && e.Side == DebtSide.Payable).SingleAsync()));
        var offsetResponse = await client.PostAsJsonAsync("/api/debt-offsets", new CreateDebtOffsetRequest
        {
            VoucherAt = Vn("2026-10-22 09:00"),
            PartnerId = both,
            Receivables = new List<AllocationRequest> { new() { EntryId = saleEntry.Id, Amount = 400m } },
            Payables = new List<AllocationRequest> { new() { EntryId = purchaseEntry.Id, Amount = 400m } },
        }, TestJson.Options);
        offsetResponse.IsSuccessStatusCode.Should().BeTrue(await offsetResponse.Content.ReadAsStringAsync());

        var report = await GetAsync(client, $"partnerId={both}&from=2026-10-01&to=2026-10-31");

        report.Sides.Select(s => (s.Side, s.ClosingBalance)).Should()
            .Equal((DebtSide.Receivable, 600m), (DebtSide.Payable, 0m));
        report.NetBalance.Should().Be(600m);

        var documents = await ReadDataAsync<DebtDocumentsReportDto>(await client.GetAsync(
            $"/api/reports/debt-documents?side=Receivable&at=2026-10-31&partnerId={both}"));
        var sale = documents.Groups.Should().ContainSingle().Which.Items.Should().ContainSingle().Which;
        sale.Settled.Should().Be(400m);
        sale.Remaining.Should().Be(600m);

        var bySalesperson = await ReadDataAsync<DebtBySalespersonReportDto>(await client.GetAsync(
            $"/api/reports/debt-by-salesperson?side=Receivable&from=2026-10-01&to=2026-10-31&partnerId={both}"));
        var row = bySalesperson.Rows.Should().ContainSingle().Which;
        row.OwnerUserId.Should().Be(saleEntry.OwnerUserId);
        row.Settled.Should().Be(400m);
    }

    [Fact]
    public async Task Side_with_entries_is_listed_without_the_role()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await InDbAsync(async db =>
        {
            var kh1 = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == ids.Kh1Id);
            kh1.IsCustomer = false;
            await db.SaveChangesAsync();
        });

        var report = await GetAsync(MainBranchClient(ids), $"partnerId={ids.Kh1Id}&from=2026-10-01&to=2026-10-31");

        var side = report.Sides.Should().ContainSingle().Which;
        side.Side.Should().Be(DebtSide.Receivable);
        side.ClosingBalance.Should().Be(1_500m);
        report.NetBalance.Should().BeNull();
    }

    /// A customer-and-supplier partner with a sale of 1,000 (10-20) and a purchase of 400 (10-21), both paid 0.
    private async Task<(ReportIds Ids, HttpClient Client, Guid Both)> SeedBothAsync()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var both = await CreatePartnerAsync("BOTH" + ids.Kh1Code[3..], isCustomer: true, isSupplier: true);
        await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", "2026-10-20 09:00",
            ids.MainWarehouseId, both, 1_000m, paymentMethodId: null, paid: 0m);
        await ReportDataset.PostStockAsync(this, client, StockDirection.In, "NMH", "2026-10-21 09:00",
            ids.MainWarehouseId, both, 400m, paymentMethodId: null, paid: 0m);
        return (ids, client, both);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<DebtReconciliationReportDto> GetAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtReconciliationReportDto>(await client.GetAsync($"/api/reports/debt-reconciliation?{query}"));
}
