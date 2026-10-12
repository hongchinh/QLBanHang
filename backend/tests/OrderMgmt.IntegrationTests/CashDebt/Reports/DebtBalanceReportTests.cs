using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Số dư công nợ (Phase 09).
[Collection(nameof(PostgresCollection))]
public class DebtBalanceReportTests : CashDebtTestBase
{
    public DebtBalanceReportTests(PostgresFixture pg) : base(pg) { }

    /// The dataset builds through the API, keeps the invariants and matches the plan's hand-computed numbers.
    [Fact]
    public async Task Dataset_builds_with_invariants()
    {
        var ids = await ReportDataset.SeedAsync(this);

        // Codes.
        var cashCodes = await InDbAsync(db => db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.BranchId == ids.MainBranchId)
            .ToDictionaryAsync(v => v.Id, v => v.Code));
        cashCodes.Should().BeEquivalentTo(new Dictionary<Guid, string>
        {
            [ids.AutoReceiptId] = "PT00001",
            [ids.Receipt5Id] = "PT00002",
            [ids.Receipt11Id] = "PT00003",
            [ids.AutoPaymentId] = "PC00001",
            [ids.Payment8Id] = "PC00002",
            [ids.Payment10Id] = "PC00003",
        });
        (await InDbAsync(db => db.CashVouchers.Where(v => v.Id == ids.Receipt11Id).Select(v => v.Status).SingleAsync()))
            .Should().Be(DocumentStatus.Cancelled);
        (await InDbAsync(db => db.StockVouchers.AsNoTracking()
                .Where(v => v.Id == ids.Return6Id || v.Id == ids.Purchase7Id)
                .ToDictionaryAsync(v => v.Id, v => v.Code)))
            .Should().BeEquivalentTo(new Dictionary<Guid, string> { [ids.Return6Id] = "PN00001", [ids.Purchase7Id] = "PN00002" });
        (await InDbAsync(db => db.FundTransfers.Where(f => f.Id == ids.Transfer9Id).Select(f => f.Code).SingleAsync()))
            .Should().Be("CQ00001");

        // KH1 receivable movements in the main branch: (PostedAt, signed amount) in ledger order.
        var kh1Movements = await InDbAsync(db => db.DebtEntries.AsNoTracking()
            .Where(e => e.BranchId == ids.MainBranchId && e.PartnerId == ids.Kh1Id && e.Side == DebtSide.Receivable)
            .OrderBy(e => e.PostedAt).ThenBy(e => e.SourceType).ThenBy(e => e.DocCode)
            .Select(e => new { e.PostedAt, Signed = e.Direction == DebtDirection.Increase ? e.Amount : -e.Amount })
            .ToListAsync());
        kh1Movements.Select(m => (m.PostedAt, m.Signed)).Should().Equal(
            (Vn("2026-10-01 00:00"), 5_000m),   // HD-OLD
            (Vn("2026-10-01 00:00"), -1_000m),  // TT-OLD
            (Vn("2026-10-03 09:00"), 3_000m),   // XBH
            (Vn("2026-10-03 09:00"), -1_000m),  // auto receipt PT00001
            (Vn("2026-10-05 09:00"), -4_000m),  // PT00002
            (Vn("2026-10-08 09:00"), -500m));   // NTL PN00001

        // Balances via the query service: KH1 opening 4,000 → closing 1,500, NCC1 payable 1,000.
        var opening = VnTime.StartOfDay(ReportDataset.OpeningDate);
        (await BalanceAsync(ids.MainBranchId, ids.Kh1Id, DebtSide.Receivable, opening)).Should().Be(4_000m);
        (await BalanceAsync(ids.MainBranchId, ids.Kh1Id, DebtSide.Receivable, null)).Should().Be(1_500m);
        (await BalanceAsync(ids.MainBranchId, ids.Ncc1Id, DebtSide.Payable, null)).Should().Be(1_000m);
        (await BalanceAsync(ids.MainBranchId, ids.Kh1Id, DebtSide.Payable, null)).Should().Be(0m);
        // Branch B keeps its own sale.
        (await BalanceAsync(ids.BranchBId, ids.Kh1Id, DebtSide.Receivable, null)).Should().Be(9_999m);

        // Funds via the P17 movements: QTM 19,300, NH01 10,000.
        var qtm = await MovementsAsync(ids.QtmId);
        qtm.Should().Equal(10_000m, 1_000m, 4_000m, 5_000m, -700m);
        qtm.Sum().Should().Be(19_300m);
        var nh01 = await MovementsAsync(ids.Nh01Id);
        nh01.Should().Equal(20_000m, -2_000m, -3_000m, -5_000m);
        nh01.Sum().Should().Be(10_000m);

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Receivable_balance_for_october()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var report = await GetBalanceAsync(client, "side=Receivable&from=2026-10-01&to=2026-10-31");

        report.Side.Should().Be(DebtSide.Receivable);
        report.From.Should().Be(new DateOnly(2026, 10, 1));
        report.To.Should().Be(new DateOnly(2026, 10, 31));
        // KH1 only: branch B's sale (9,999) is absent and the cancelled receipt PT00003 has no entry.
        var expected = new DebtBalanceRowDto
        {
            PartnerId = ids.Kh1Id,
            PartnerCode = ids.Kh1Code,
            PartnerName = $"Đối tượng {ids.Kh1Code}",
            OpeningDebit = 4_000m,
            OpeningCredit = 0m,
            Increase = 3_000m,
            Decrease = 5_500m,
            ClosingDebit = 1_500m,
            ClosingCredit = 0m,
        };
        report.Rows.Should().BeEquivalentTo(new[] { expected });
        report.Totals.Should().BeEquivalentTo(expected, o => o
            .Excluding(r => r.PartnerId).Excluding(r => r.PartnerCode).Excluding(r => r.PartnerName));
        report.Totals.PartnerId.Should().BeNull();
    }

    [Fact]
    public async Task Window_and_sign()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        // From 10-06: everything up to 10-05 is opening (4,000 + 3,000 − 1,000 − 4,000).
        var receivable = await GetBalanceAsync(client, "side=Receivable&from=2026-10-06&to=2026-10-31");
        receivable.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PartnerId = (Guid?)ids.Kh1Id,
            OpeningDebit = 2_000m,
            OpeningCredit = 0m,
            Increase = 0m,
            Decrease = 500m,
            ClosingDebit = 1_500m,
            ClosingCredit = 0m,
        });

        // P29: a report starting on the opening date still shows the opening debts as opening, not as movements.
        var fromOpening = await GetBalanceAsync(client, "side=Receivable&from=2026-10-01&to=2026-10-02");
        fromOpening.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            OpeningDebit = 4_000m, OpeningCredit = 0m, Increase = 0m, Decrease = 0m, ClosingDebit = 4_000m, ClosingCredit = 0m,
        });

        // Payable side: NCC1 positive → Dư Có; a supplier paid in advance (negative payable) → Dư Nợ (P24).
        var ncc2 = await CreatePartnerAsync("NCC2" + ids.Ncc1Code[4..], isCustomer: false, isSupplier: true);
        await CreateCashAsync(client, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("TNCC"), ids.QtmId,
            "2026-10-20 09:00", ncc2, 800m));

        var payable = await GetBalanceAsync(client, "side=Payable&from=2026-10-01&to=2026-10-31");
        payable.Rows.Select(r => r.PartnerId).Should().Equal(ids.Ncc1Id, ncc2);
        payable.Rows[0].Should().BeEquivalentTo(new
        {
            OpeningDebit = 0m, OpeningCredit = 0m, Increase = 6_000m, Decrease = 5_000m, ClosingDebit = 0m, ClosingCredit = 1_000m,
        });
        payable.Rows[1].Should().BeEquivalentTo(new
        {
            OpeningDebit = 0m, OpeningCredit = 0m, Increase = 0m, Decrease = 800m, ClosingDebit = 800m, ClosingCredit = 0m,
        });
        payable.Totals.Should().BeEquivalentTo(new
        {
            PartnerId = (Guid?)null,
            OpeningDebit = 0m, OpeningCredit = 0m, Increase = 6_000m, Decrease = 5_800m, ClosingDebit = 800m, ClosingCredit = 1_000m,
        });

        // From 10-21 the prepaid supplier's negative balance is an opening Dư Nợ.
        var later = await GetBalanceAsync(client, "side=Payable&from=2026-10-21&to=2026-10-31");
        later.Rows.Single(r => r.PartnerId == ncc2).Should().BeEquivalentTo(new
        {
            OpeningDebit = 800m, OpeningCredit = 0m, Increase = 0m, Decrease = 0m, ClosingDebit = 800m, ClosingCredit = 0m,
        });

        // Before the payment that supplier has no non-zero value, so it has no row.
        (await GetBalanceAsync(client, "side=Payable&from=2026-10-01&to=2026-10-19"))
            .Rows.Select(r => r.PartnerId).Should().Equal(ids.Ncc1Id);
    }

    /// P29: Opening entries are opening balance in every window, even one that ends before the opening date,
    /// so one period's closing is always the next period's opening.
    [Fact]
    public async Task Opening_entries_count_in_windows_before_the_opening_date()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        // A receipt dated before the branch opening date (10-01). The API rejects one now (OpeningDateGuard), so it is
        // made as legacy data: the opening date is lifted around the save.
        await InDbAsync(db => db.Branches.Where(b => b.Id == ids.MainBranchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.OpeningDate, (DateOnly?)null)));
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), ids.QtmId,
            "2026-09-25 09:00", ids.Kh1Id, 300m));
        await InDbAsync(db => db.Branches.Where(b => b.Id == ids.MainBranchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.OpeningDate, (DateOnly?)ReportDataset.OpeningDate)));

        var september = await GetBalanceAsync(client, "side=Receivable&from=2026-09-01&to=2026-09-30");
        september.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PartnerId = (Guid?)ids.Kh1Id,
            OpeningDebit = 4_000m, OpeningCredit = 0m, Increase = 0m, Decrease = 300m, ClosingDebit = 3_700m, ClosingCredit = 0m,
        });

        var october = await GetBalanceAsync(client, "side=Receivable&from=2026-10-01&to=2026-10-31");
        october.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            OpeningDebit = 3_700m, OpeningCredit = 0m, Increase = 3_000m, Decrease = 5_500m, ClosingDebit = 1_200m, ClosingCredit = 0m,
        });

        // The September closing agrees with the ledger balance at the start of 10-01.
        (await BalanceAsync(ids.MainBranchId, ids.Kh1Id, DebtSide.Receivable,
                VnTime.StartOfNextDay(new DateOnly(2026, 9, 30))))
            .Should().Be(3_700m);
    }

    [Fact]
    public async Task Filters_and_permission()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var suffix = ids.Kh1Code[3..];
        var kh2 = await CreatePartnerAsync("KH2" + suffix, isCustomer: true, isSupplier: false);
        await InDbAsync(db => db.Customers.Where(c => c.Id == kh2)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, $"Cửa hàng Hoa Mai {suffix}")));
        await CreateOpeningDebtAsync(client, kh2, DebtSide.Receivable, OpeningDebtKind.Debt, "HD-K2",
            new DateOnly(2026, 9, 10), 700m);
        const string october = "side=Receivable&from=2026-10-01&to=2026-10-31";

        var all = await GetBalanceAsync(client, october);
        all.Rows.Select(r => r.PartnerId).Should().Equal(ids.Kh1Id, kh2);
        all.Totals.OpeningDebit.Should().Be(4_700m);
        all.Totals.ClosingDebit.Should().Be(2_200m);

        var byPartner = await GetBalanceAsync(client, $"{october}&partnerId={kh2}");
        byPartner.Rows.Should().ContainSingle().Which.ClosingDebit.Should().Be(700m);
        byPartner.Totals.ClosingDebit.Should().Be(700m);

        // Search matches the code or the name (case- and accent-insensitive).
        (await GetBalanceAsync(client, $"{october}&search={ids.Kh1Code.ToLowerInvariant()}"))
            .Rows.Select(r => r.PartnerId).Should().Equal(ids.Kh1Id);
        (await GetBalanceAsync(client, $"{october}&search=hoa%20mai%20{suffix}"))
            .Rows.Select(r => r.PartnerId).Should().Equal(kh2);
        (await GetBalanceAsync(client, $"{october}&search=Cua%20hang%20hoa%20mai%20{suffix}"))
            .Rows.Select(r => r.PartnerId).Should().Equal(kh2);
        var none = await GetBalanceAsync(client, $"{october}&search=zz-no-such-partner");
        none.Rows.Should().BeEmpty();
        none.Totals.ClosingDebit.Should().Be(0m);

        // Validation: from > to, missing side, missing from.
        (await client.GetAsync("/api/reports/debt-balance?side=Receivable&from=2026-10-31&to=2026-10-01"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/reports/debt-balance?from=2026-10-01&to=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/reports/debt-balance?side=Receivable&to=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Permission: WAREHOUSE has no reports.debt.
        var warehouse = await CreateClientForRoleAsync("dbr_wh_" + suffix, RoleCodes.Warehouse, ids.MainBranchId);
        (await warehouse.GetAsync($"/api/reports/debt-balance?{october}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<DebtBalanceReportDto> GetBalanceAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtBalanceReportDto>(await client.GetAsync($"/api/reports/debt-balance?{query}"));

    private Task<decimal> BalanceAsync(Guid branchId, Guid partnerId, DebtSide side, DateTimeOffset? at) =>
        InTransactionAsync((sp, ct) => sp.GetRequiredService<IDebtQueryService>()
            .BalanceAsync(branchId, partnerId, side, at, ct));

    /// Deltas of the fund's movements in P17 order (OpeningAmount first).
    private async Task<List<decimal>> MovementsAsync(Guid fundId) =>
        (await InTransactionAsync((sp, ct) => sp.GetRequiredService<IFundBalanceGuard>().MovementsAsync(fundId, ct)))
        .Select(m => m.Delta).ToList();
}
