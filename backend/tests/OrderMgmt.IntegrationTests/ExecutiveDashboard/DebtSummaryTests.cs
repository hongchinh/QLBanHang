using FluentAssertions;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// debt/summary: what customers owe and what is owed to suppliers at a date, split into not due, overdue by age and
/// due within seven days.
[Collection(nameof(PostgresCollection))]
public class DebtSummaryTests : ExecutiveDashboardTestBase
{
    public DebtSummaryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Receivable_and_payable_of_main_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetDebtSummaryAsync(MainClient(ids), $"at=2026-10-31&branchId={ids.MainBranchId}");

        summary.At.Should().Be(D(10, 31));
        summary.OverdueBuckets.Select(b => (b.From, b.To)).Should().Equal((1, 30), (31, 60), (61, 90), (91, (int?)null));

        var receivable = summary.Receivable;
        receivable.TotalOpen.Should().Be(3_000m);
        receivable.Unallocated.Should().Be(1_500m);
        receivable.Balance.Should().Be(1_500m);
        receivable.Overdue.Should().Be(3_000m);
        receivable.OverdueAmounts.Should().Equal(2_000m, 1_000m, 0m, 0m);
        receivable.NotDue.Should().Be(0m);
        receivable.OverduePartnerCount.Should().Be(1);
        (receivable.DueSoon, receivable.DueSoonCount).Should().Be((0m, 0));

        // The purchase of 10-10 is due on its own date, 21 days before the 31st.
        var payable = summary.Payable;
        payable.TotalOpen.Should().Be(4_000m);
        payable.Unallocated.Should().Be(3_000m);
        payable.Balance.Should().Be(1_000m);
        payable.Overdue.Should().Be(4_000m);
        payable.OverdueAmounts.Should().Equal(4_000m, 0m, 0m, 0m);
        payable.NotDue.Should().Be(0m);
        payable.OverduePartnerCount.Should().Be(1);
        (payable.DueSoon, payable.DueSoonCount).Should().Be((0m, 0));
    }

    [Fact]
    public async Task Summary_equals_the_reports()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);

        var summary = await GetDebtSummaryAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");

        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            var mine = side == DebtSide.Receivable ? summary.Receivable : summary.Payable;

            var overdue = await ReadDataAsync<DebtOverdueReportDto>(
                await client.GetAsync($"/api/reports/debt-overdue?side={side}&at=2026-10-31"));
            overdue.TotalOverdue.Should().Be(mine.Overdue, $"{side}: overdue");
            overdue.TotalAmounts.Should().Equal(mine.OverdueAmounts, $"{side}: overdue by bucket");

            var aging = await ReadDataAsync<DebtAgingReportDto>(
                await client.GetAsync($"/api/reports/debt-aging?side={side}&at=2026-10-31"));
            aging.Totals.TotalOpen.Should().Be(mine.TotalOpen, $"{side}: open");
            aging.Totals.Unallocated.Should().Be(mine.Unallocated, $"{side}: unallocated");
            aging.Totals.Balance.Should().Be(mine.Balance, $"{side}: balance");

            var balance = await ReadDataAsync<DebtBalanceReportDto>(
                await client.GetAsync($"/api/reports/debt-balance?side={side}&from=2026-10-01&to=2026-10-31"));
            var closing = side == DebtSide.Receivable
                ? balance.Totals.ClosingDebit - balance.Totals.ClosingCredit
                : balance.Totals.ClosingCredit - balance.Totals.ClosingDebit;
            closing.Should().Be(mine.Balance, $"{side}: closing balance");
        }
    }

    [Fact]
    public async Task Position_is_as_at_the_given_date()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetDebtSummaryAsync(MainClient(ids), $"at=2026-10-04&branchId={ids.MainBranchId}");

        // The receipt of 10-05 and the return of 10-08 are not counted yet.
        summary.At.Should().Be(D(10, 4));
        summary.Receivable.TotalOpen.Should().Be(7_000m);
        summary.Receivable.Unallocated.Should().Be(1_000m);
        summary.Receivable.Balance.Should().Be(6_000m);
    }

    [Fact]
    public async Task Position_includes_documents_of_the_given_day()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetDebtSummaryAsync(MainClient(ids), $"at=2026-10-03&branchId={ids.MainBranchId}");

        // The sale of 10-03 09:00 and its automatic receipt are in: 5,000 of HD-OLD and 2,000 of the sale are open.
        summary.Receivable.TotalOpen.Should().Be(7_000m);
        summary.Receivable.Unallocated.Should().Be(1_000m);
        // HD-OLD (due 09-20) is 13 days late; the sale falls due on the 3rd itself, so it is not overdue yet.
        summary.Receivable.Overdue.Should().Be(5_000m);
        summary.Receivable.NotDue.Should().Be(2_000m);
        summary.Receivable.OverdueAmounts.Should().Equal(5_000m, 0m, 0m, 0m);
    }

    [Fact]
    public async Task Company_wide_adds_branches()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetDebtSummaryAsync(MainClient(ids), "at=2026-10-31");

        // Branch B's unpaid sale of 9,999 (due 10-03, 28 days late) is the same customer: still one overdue partner.
        summary.Receivable.Balance.Should().Be(11_499m);
        summary.Receivable.TotalOpen.Should().Be(12_999m);
        summary.Receivable.Overdue.Should().Be(12_999m);
        summary.Receivable.OverdueAmounts.Should().Equal(11_999m, 1_000m, 0m, 0m);
        summary.Receivable.OverduePartnerCount.Should().Be(1);
        summary.Payable.Balance.Should().Be(1_000m);
    }

    [Fact]
    public async Task Due_soon_counts_the_next_seven_days()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        await PostPurchaseAsync(ids, ids.Ncc1Id, 800m, D(10, 25));

        var twentyFirst = await GetDebtSummaryAsync(client, $"at=2026-10-21&branchId={ids.MainBranchId}");
        var twentySixth = await GetDebtSummaryAsync(client, $"at=2026-10-26&branchId={ids.MainBranchId}");

        (twentyFirst.Payable.DueSoon, twentyFirst.Payable.DueSoonCount).Should().Be((800m, 1));
        // The purchase of 10-10 (4,000 left) is late on both days; on the 21st only the new one is not due yet.
        twentyFirst.Payable.NotDue.Should().Be(800m);
        // On the 26th the purchase is a day late.
        (twentySixth.Payable.DueSoon, twentySixth.Payable.DueSoonCount).Should().Be((0m, 0));
        twentySixth.Payable.Overdue.Should().Be(twentyFirst.Payable.Overdue + 800m);
        twentySixth.Payable.NotDue.Should().Be(0m);
    }

    [Fact]
    public async Task Not_due_counts_what_falls_due_after_the_seven_days_too()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await PostPurchaseAsync(ids, ids.Ncc1Id, 300m, D(11, 15));

        var summary = await GetDebtSummaryAsync(MainClient(ids), $"at=2026-10-21&branchId={ids.MainBranchId}");

        // Due in 25 days: not overdue and not due soon either.
        summary.Payable.NotDue.Should().Be(300m);
        (summary.Payable.DueSoon, summary.Payable.DueSoonCount).Should().Be((0m, 0));
        summary.Payable.Overdue.Should().Be(4_000m);
    }

    [Fact]
    public async Task Fully_paid_documents_are_not_open()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var suffix = ids.Kh1Code[3..];
        var ncc2 = await CreatePartnerAsync("NCC2" + suffix, isCustomer: false, isSupplier: true);
        var bankMethod = await ReportDataset.PaymentMethodIdAsync(this, "CK");
        // A purchase of 500 from another supplier, due on the 25th and paid in full by bank when it is posted.
        await PostPurchaseAsync(ids, ncc2, 500m, D(10, 25), request =>
        {
            request.PaymentMethodId = bankMethod;
            request.PaidAmount = 500m;
        });

        var twentyFirst = await GetDebtSummaryAsync(client, $"at=2026-10-21&branchId={ids.MainBranchId}");
        var thirtyFirst = await GetDebtSummaryAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");

        // Nothing of it is owed, so it is neither due soon on the 21st nor an overdue partner on the 31st.
        (twentyFirst.Payable.DueSoon, twentyFirst.Payable.DueSoonCount).Should().Be((0m, 0));
        thirtyFirst.Payable.Overdue.Should().Be(4_000m);
        thirtyFirst.Payable.OverduePartnerCount.Should().Be(1);
    }

    [Fact]
    public async Task Balance_date_defaults_to_today_in_vn_time()
    {
        var branch = await CreateFreshBranchAsync();
        var before = VnTime.ToVnDate(DateTimeOffset.UtcNow);

        var summary = await GetDebtSummaryAsync(branch.Client, $"branchId={branch.BranchId}");

        // The request may straddle midnight in Vietnam; either day is "today" then.
        summary.At.Should().BeOneOf(before, VnTime.ToVnDate(DateTimeOffset.UtcNow));
    }

    private static Task<DebtSummaryDto> GetDebtSummaryAsync(HttpClient client, string query) =>
        GetExecAsync<DebtSummaryDto>(client, $"debt/summary?{query}");
}
