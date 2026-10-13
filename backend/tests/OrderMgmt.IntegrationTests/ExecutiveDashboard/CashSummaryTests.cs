using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// cash/summary: receipts, payments and fund balances of the report dataset (Phase 02, Task 2.1).
[Collection(nameof(PostgresCollection))]
public class CashSummaryTests : ExecutiveDashboardTestBase
{
    public CashSummaryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Summary_of_main_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var branchCode = await InDbAsync(db => db.Branches.Where(b => b.Id == ids.MainBranchId)
            .Select(b => b.Code).SingleAsync());

        var summary = await GetCashSummaryAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        summary.From.Should().Be(new DateOnly(2026, 10, 1));
        summary.To.Should().Be(new DateOnly(2026, 10, 31));
        summary.PrevFrom.Should().Be(new DateOnly(2026, 8, 31));
        summary.PrevTo.Should().Be(new DateOnly(2026, 9, 30));
        summary.At.Should().Be(new DateOnly(2026, 10, 31));
        summary.Receipts.Should().Be(5_000m);
        summary.Payments.Should().Be(5_700m);
        summary.Net.Should().Be(-700m);
        summary.PrevNet.Should().Be(0m);
        summary.NetDeltaPct.Should().BeNull();
        summary.Balance.Should().Be(29_300m);
        summary.CashBalance.Should().Be(19_300m);
        summary.BankBalance.Should().Be(10_000m);

        // Cash before bank, whatever the codes say: QTM sorts after NH01 by code alone.
        summary.Funds.Select(f => (f.FundId, f.Balance)).Should().Equal((ids.QtmId, 19_300m), (ids.Nh01Id, 10_000m));
        summary.Funds.Select(f => (f.Code, f.Name, f.Kind, f.IsActive, f.BranchId, f.BranchCode)).Should().Equal(
            ("QTM", "Quỹ tiền mặt", FundKind.Cash, true, ids.MainBranchId, branchCode),
            ("NH01", "Ngân hàng NH01", FundKind.Bank, true, ids.MainBranchId, branchCode));
    }

    [Fact]
    public async Task Balance_is_as_at_the_given_date()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetCashSummaryAsync(MainClient(ids), $"{OctoberRange}&at=2026-10-14&branchId={ids.MainBranchId}");

        // The transfer of 10-15 and the payment of 10-16 are not counted yet; the period figures do not move.
        summary.At.Should().Be(new DateOnly(2026, 10, 14));
        summary.Funds.Select(f => (f.FundId, f.Balance)).Should().Equal((ids.QtmId, 15_000m), (ids.Nh01Id, 15_000m));
        summary.Balance.Should().Be(30_000m);
        summary.Receipts.Should().Be(5_000m);
        summary.Payments.Should().Be(5_700m);
    }

    [Fact]
    public async Task Summary_equals_the_cash_book()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);

        var summary = await GetCashSummaryAsync(client, $"{October}&branchId={ids.MainBranchId}");
        var book = await ReadDataAsync<CashBookReportDto>(
            await client.GetAsync("/api/reports/cash-book?kind=All&from=2026-10-01&to=2026-10-31"));

        book.GrandIn.Should().Be(summary.Receipts);
        book.GrandOut.Should().Be(summary.Payments);
        book.GrandClosing.Should().Be(summary.Balance);
        book.Groups.Should().HaveCount(summary.Funds.Count);
        foreach (var group in book.Groups)
            summary.Funds.Single(f => f.FundId == group.FundId).Balance.Should().Be(group.Closing);
    }

    [Fact]
    public async Task Company_wide_includes_every_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var suffix = ids.Kh1Code[3..];

        var summary = await GetCashSummaryAsync(MainClient(ids), October);

        summary.Balance.Should().Be(29_300m);
        summary.Funds.Should().Contain(f => f.BranchId == ids.BranchBId);
        // Branch code first (RB before RD), then kind, then code.
        summary.Funds.Where(f => f.BranchId == ids.MainBranchId || f.BranchId == ids.BranchBId)
            .Select(f => (f.BranchCode, f.Code))
            .Should().Equal(("RB" + suffix, "QTM"), ("RD" + suffix, "QTM"), ("RD" + suffix, "NH01"));
    }

    [Fact]
    public async Task Net_is_compared_with_the_previous_period()
    {
        var ids = await ReportDataset.SeedAsync(this);

        // 10-05..10-08 against 10-01..10-04: the receipt of 10-05 against the automatic receipt of 10-03.
        var summary = await GetCashSummaryAsync(MainClient(ids),
            $"from=2026-10-05&to=2026-10-08&at=2026-10-31&branchId={ids.MainBranchId}");

        summary.PrevFrom.Should().Be(new DateOnly(2026, 10, 1));
        summary.PrevTo.Should().Be(new DateOnly(2026, 10, 4));
        summary.Receipts.Should().Be(4_000m);
        summary.Payments.Should().Be(0m);
        summary.Net.Should().Be(4_000m);
        summary.PrevNet.Should().Be(1_000m);
        summary.NetDeltaPct.Should().Be(300m);
    }

    [Fact]
    public async Task Balance_includes_the_given_day()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetCashSummaryAsync(MainClient(ids), $"{OctoberRange}&at=2026-10-15&branchId={ids.MainBranchId}");

        // The transfer NH01 -> QTM of 10-15 09:00 is already in.
        summary.Funds.Select(f => (f.FundId, f.Balance)).Should().Equal((ids.QtmId, 20_000m), (ids.Nh01Id, 10_000m));
        summary.Balance.Should().Be(30_000m);
    }

    [Fact]
    public async Task Periods_and_balance_run_from_midnight_to_midnight_in_vn_time()
    {
        var branch = await CreateFreshBranchAsync();
        var fundId = await CashFundIdAsync(branch.BranchId);
        // Current period 10-10..10-19, previous 09-30..10-09; each amount is a distinct size so a stray one shows.
        await AddAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 1_000m, "2026-09-29 23:59");   // before both
        await AddAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 2_000m, "2026-09-30 00:00");   // previous, first minute
        await AddAsync(branch.BranchId, fundId, CashVoucherType.Payment, 400m, "2026-10-09 23:59");      // previous, last minute
        await AddAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 8_000m, "2026-10-10 00:00");   // current, first minute
        await AddAsync(branch.BranchId, fundId, CashVoucherType.Payment, 160m, "2026-10-19 23:59");      // current, last minute
        await AddAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 32_000m, "2026-10-20 00:00");  // after the period
        await AddAsync(branch.BranchId, fundId, CashVoucherType.Payment, 6_400m, "2026-10-20 00:00");   // after the period

        var summary = await GetCashSummaryAsync(branch.Client, "from=2026-10-10&to=2026-10-19&at=2026-10-19");

        summary.Receipts.Should().Be(8_000m);
        summary.Payments.Should().Be(160m);
        summary.Net.Should().Be(7_840m);
        summary.PrevNet.Should().Be(1_600m);
        summary.NetDeltaPct.Should().Be(390m);
        // The balance at the end of 10-19 leaves out what was posted at 10-20 00:00; at 10-20 it is in.
        summary.Balance.Should().Be(10_440m);
        (await GetCashSummaryAsync(branch.Client, "from=2026-10-10&to=2026-10-19&at=2026-10-20")).Balance
            .Should().Be(36_040m);
    }

    [Fact]
    public async Task Inactive_fund_keeps_its_balance_in_the_list()
    {
        var ids = await ReportDataset.SeedAsync(this);
        // Created after NH01 but sorting before it: the list is in code order, not in creation order.
        var nh00 = await CreateBankFundAsync("NH00", ids.MainBranchId, opening: 7_000m);
        await InDbAsync(db => db.Funds.Where(f => f.Id == nh00)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsActive, false)));

        var summary = await GetCashSummaryAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        summary.Funds.Select(f => (f.Code, f.IsActive, f.Balance)).Should().Equal(
            ("QTM", true, 19_300m), ("NH00", false, 7_000m), ("NH01", true, 10_000m));
        summary.BankBalance.Should().Be(17_000m);
        summary.Balance.Should().Be(36_300m);
    }

    [Fact]
    public async Task Balance_date_defaults_to_today_in_vn_time()
    {
        var branch = await CreateFreshBranchAsync();
        var before = VnTime.ToVnDate(DateTimeOffset.UtcNow);

        var summary = await GetCashSummaryAsync(branch.Client, $"branchId={branch.BranchId}");

        // The request may straddle midnight in Vietnam; either day is "today" then.
        summary.At.Should().BeOneOf(before, VnTime.ToVnDate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Funds_of_one_kind_are_listed_in_ordinal_code_order()
    {
        var branch = await CreateFreshBranchAsync();
        // Ordinal order puts upper case first: B3, a1, b2. A culture-aware or database order would put a1 first.
        foreach (var code in new[] { "b2", "a1", "B3" })
            await CreateBankFundAsync(code, branch.BranchId);

        var summary = await GetCashSummaryAsync(branch.Client, $"{October}&branchId={branch.BranchId}");

        summary.Funds.Select(f => f.Code).Should().Equal("QTM", "B3", "a1", "b2");
    }

    [Fact]
    public async Task Branch_without_funds_has_zero_figures()
    {
        var branch = await CreateFreshBranchAsync();
        await InDbAsync(db => db.Funds.Where(f => f.BranchId == branch.BranchId).ExecuteDeleteAsync());

        var summary = await GetCashSummaryAsync(branch.Client, $"{October}&branchId={branch.BranchId}");

        summary.Funds.Should().BeEmpty();
        (summary.Receipts, summary.Payments, summary.Net, summary.PrevNet).Should().Be((0m, 0m, 0m, 0m));
        (summary.Balance, summary.CashBalance, summary.BankBalance).Should().Be((0m, 0m, 0m));
        summary.NetDeltaPct.Should().BeNull();
    }

    /// A receipt or payment row straight in the database (reasons without a debt effect, no partner).
    private Task AddAsync(Guid branchId, Guid fundId, CashVoucherType type, decimal total, string at) =>
        InsertCashVoucherAsync(branchId, fundId, type, total, Vn(at));

    private static Task<CashSummaryDto> GetCashSummaryAsync(HttpClient client, string query) =>
        GetExecAsync<CashSummaryDto>(client, $"cash/summary?{query}");
}
