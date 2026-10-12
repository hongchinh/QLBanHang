using FluentAssertions;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// The report windows are Vietnam calendar days (D31 / P25), not UTC days. ReportDataset puts every voucher at 09:00 VN,
/// which is the same date in UTC, so this class puts receipts either side of the VN midnights.
[Collection(nameof(PostgresCollection))]
public class ReportDayBoundaryTests : CashDebtTestBase
{
    private const string Window = "from=2026-10-10&to=2026-10-20";

    public ReportDayBoundaryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Windows_are_vn_calendar_days_in_the_debt_balance_and_the_cash_book()
    {
        var branchId = await CreateBranchAsync("RN" + Guid.NewGuid().ToString("N")[..8]);
        await ReportDataset.SetOpeningDateAsync(this, branchId, new DateOnly(2026, 9, 1));
        var fundId = await CashFundIdAsync(branchId);
        var partner = await CreatePartnerAsync("KHN" + Guid.NewGuid().ToString("N")[..6], isCustomer: true,
            isSupplier: false);
        var reasonId = await CashReasonIdAsync("TKH");
        var client = CloneAdminClient();
        UseBranch(client, branchId);

        // 00:30 VN on 10-10 is 17:30 UTC on 10-09; 23:59 VN on 10-20 is 16:59 UTC the same day.
        // 23:59 VN on 10-09 and 00:30 VN on 10-21 are outside the window.
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-09 23:59", partner, 1m));
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-10 00:30", partner, 10m));
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-20 23:59", partner, 100m));
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, reasonId, fundId, "2026-10-21 00:30", partner, 1_000m));

        var balance = await ReadDataAsync<DebtBalanceReportDto>(
            await client.GetAsync($"/api/reports/debt-balance?side=Receivable&{Window}"));
        // A customer's receipts with no debt are prepayments (credit): 1 before the window, 10 + 100 inside it.
        var row = balance.Rows.Should().ContainSingle().Subject;
        (row.OpeningDebit, row.OpeningCredit).Should().Be((0m, 1m));
        row.Increase.Should().Be(0m);
        row.Decrease.Should().Be(110m);
        (row.ClosingDebit, row.ClosingCredit).Should().Be((0m, 111m));

        var book = await ReadDataAsync<CashBookReportDto>(
            await client.GetAsync($"/api/reports/cash-book?kind=Cash&{Window}"));
        var group = book.Groups.Should().ContainSingle().Subject;
        group.Opening.Should().Be(1m);
        group.Rows.Select(r => r.In).Should().Equal(10m, 100m);
        group.TotalIn.Should().Be(110m);
        group.Closing.Should().Be(111m);
    }
}
