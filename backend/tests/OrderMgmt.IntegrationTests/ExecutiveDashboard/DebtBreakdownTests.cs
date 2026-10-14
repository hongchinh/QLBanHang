using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// debt/breakdown: the customers who owe the most, the credit limits that are exceeded, the receivable by the employee
/// who owns the documents, and the payables that fall due within thirty days.
[Collection(nameof(PostgresCollection))]
public class DebtBreakdownTests : ExecutiveDashboardTestBase
{
    public DebtBreakdownTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Top_debtors()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);

        var main = await GetDebtBreakdownAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");

        main.At.Should().Be(D(10, 31));
        var row = main.TopDebtors.Should().ContainSingle().Which;
        row.PartnerId.Should().Be(ids.Kh1Id);
        row.PartnerCode.Should().Be(ids.Kh1Code);
        row.PartnerName.Should().Be($"Đối tượng {ids.Kh1Code}");
        row.Balance.Should().Be(1_500m);
        row.Overdue.Should().Be(3_000m);

        // The same customer owes 9,999 more in branch B: one row for the whole company.
        var company = (await GetDebtBreakdownAsync(client, "at=2026-10-31")).TopDebtors.Should().ContainSingle().Which;
        company.PartnerId.Should().Be(ids.Kh1Id);
        company.Balance.Should().Be(11_499m);
        company.Overdue.Should().Be(12_999m);
    }

    [Fact]
    public async Task Top_debtors_leave_out_customers_in_credit_or_at_zero()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var suffix = ids.Kh1Code[3..];
        // KC paid ahead (a credit of 300). KZ owes 500 and has paid 500 that is not matched to it: balance 0.
        var credit = await CreatePartnerAsync("KC" + suffix, isCustomer: true, isSupplier: false);
        var zero = await CreatePartnerAsync("KZ" + suffix, isCustomer: true, isSupplier: false);
        await CreateOpeningDebtAsync(client, credit, DebtSide.Receivable, OpeningDebtKind.Prepayment, "TT-KC", D(9, 15), 300m);
        await CreateOpeningDebtAsync(client, zero, DebtSide.Receivable, OpeningDebtKind.Debt, "HD-KZ", D(9, 5), 500m,
            dueDate: D(9, 20));
        await CreateOpeningDebtAsync(client, zero, DebtSide.Receivable, OpeningDebtKind.Prepayment, "TT-KZ", D(9, 15), 500m);

        var breakdown = await GetDebtBreakdownAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");

        breakdown.TopDebtors.Select(r => r.PartnerId).Should().Equal(ids.Kh1Id);
    }

    [Fact]
    public async Task Top_debtors_are_ordered_by_balance_then_code_and_cut_at_ten()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var suffix = ids.Kh1Code[3..];
        // Eleven customers beside KH1 (1,500). The five with 9,000 are created in the opposite order of their codes:
        // only the code can put five equal balances in this order (their ids are random).
        foreach (var (code, amount) in new[]
                 {
                     ("TIEE", 9_000m), ("TIED", 9_000m), ("TIEC", 9_000m), ("TIEB", 9_000m), ("TIEA", 9_000m),
                     ("K8000", 8_000m), ("K7000", 7_000m), ("K6000", 6_000m), ("K5000", 5_000m), ("K4000", 4_000m),
                     ("K1000", 1_000m),
                 })
        {
            var partnerId = await CreatePartnerAsync(code + suffix, isCustomer: true, isSupplier: false);
            await CreateOpeningDebtAsync(client, partnerId, DebtSide.Receivable, OpeningDebtKind.Debt, "HD-" + code,
                D(9, 5), amount, dueDate: D(9, 20));
        }

        var breakdown = await GetDebtBreakdownAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");

        // KH1 (1,500) and K1000 do not fit in the ten.
        breakdown.TopDebtors.Select(r => (r.PartnerCode, r.Balance)).Should().Equal(
            ("TIEA" + suffix, 9_000m), ("TIEB" + suffix, 9_000m), ("TIEC" + suffix, 9_000m), ("TIED" + suffix, 9_000m),
            ("TIEE" + suffix, 9_000m), ("K8000" + suffix, 8_000m), ("K7000" + suffix, 7_000m), ("K6000" + suffix, 6_000m),
            ("K5000" + suffix, 5_000m), ("K4000" + suffix, 4_000m));
    }

    [Fact]
    public async Task Position_and_overdue_are_as_at_the_given_date()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        var breakdown = await GetDebtBreakdownAsync(MainClient(ids), $"at=2026-10-03&branchId={ids.MainBranchId}");

        // On the 3rd HD-OLD is still whole (the receipt of 10-05 is not in yet) and 13 days late. The sale of that day
        // has 2,000 left after its automatic receipt and falls due that day, so it is not overdue yet. TT-OLD (1,000)
        // is unmatched money: it lowers the balance and belongs to no employee.
        using (new AssertionScope())
        {
            breakdown.At.Should().Be(D(10, 3));
            breakdown.TopDebtors.Select(r => (r.PartnerId, r.Balance, r.Overdue))
                .Should().Equal((ids.Kh1Id, 6_000m, 5_000m));
            breakdown.ByEmployee.Select(r => (r.OwnerUserId, r.Open, r.Overdue))
                .Should().Equal(((Guid?)adminId, 2_000m, 0m), (null, 5_000m, 5_000m));
        }
    }

    [Fact]
    public async Task Deleted_customer_keeps_the_name()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await InDbAsync(db => db.Customers.IgnoreQueryFilters().Where(c => c.Id == ids.Kh1Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true)));

        var breakdown = await GetDebtBreakdownAsync(MainClient(ids), $"at=2026-10-31&branchId={ids.MainBranchId}");

        var row = breakdown.TopDebtors.Should().ContainSingle().Which;
        row.PartnerCode.Should().Be(ids.Kh1Code);
        row.PartnerName.Should().Be($"Đối tượng {ids.Kh1Code}");
    }

    [Fact]
    public async Task Over_limit()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var query = $"at=2026-10-31&branchId={ids.MainBranchId}";
        var branchCode = await BranchCodeAsync(ids.MainBranchId);

        await SetLimitAsync(client, ids.Kh1Id, 1_000m);

        var breach = (await GetDebtBreakdownAsync(client, query)).OverLimit.Should().ContainSingle().Which;
        breach.PartnerId.Should().Be(ids.Kh1Id);
        breach.PartnerCode.Should().Be(ids.Kh1Code);
        breach.PartnerName.Should().Be($"Đối tượng {ids.Kh1Code}");
        breach.BranchId.Should().Be(ids.MainBranchId);
        breach.BranchCode.Should().Be(branchCode);
        breach.Balance.Should().Be(1_500m);
        breach.Limit.Should().Be(1_000m);
        breach.Over.Should().Be(500m);

        await SetLimitAsync(client, ids.Kh1Id, 2_000m);

        (await GetDebtBreakdownAsync(client, query)).OverLimit.Should().BeEmpty();
    }

    [Fact]
    public async Task Over_limit_is_per_branch_strictly_above_the_limit_and_ignores_deleted_limits()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var suffix = ids.Kh1Code[3..];
        var client = MainClient(ids);
        var branchB = CloneAdminClient();
        UseBranch(branchB, ids.BranchBId);
        var mainQuery = $"at=2026-10-31&branchId={ids.MainBranchId}";

        // The customer owes 1,500 in the main branch: a limit of exactly that is not exceeded.
        await SetLimitAsync(client, ids.Kh1Id, 1_500m);
        (await GetDebtBreakdownAsync(client, mainQuery)).OverLimit.Should().BeEmpty();

        // And 9,999 in branch B. Each branch has its own limit and its own balance; the larger excess comes first.
        await SetLimitAsync(client, ids.Kh1Id, 1_000m);
        await SetLimitAsync(branchB, ids.Kh1Id, 1_000m);
        var company = (await GetDebtBreakdownAsync(client, "at=2026-10-31")).OverLimit;
        company.Select(b => (b.BranchId, b.BranchCode, b.Balance, b.Limit, b.Over)).Should().Equal(
            (ids.BranchBId, "RB" + suffix, 9_999m, 1_000m, 8_999m),
            (ids.MainBranchId, "RD" + suffix, 1_500m, 1_000m, 500m));
        (await GetDebtBreakdownAsync(client, mainQuery)).OverLimit.Select(b => b.BranchId)
            .Should().Equal(ids.MainBranchId);

        // A limit that was deleted is no limit.
        (await client.DeleteAsync($"/api/credit-limits/{ids.Kh1Id}")).IsSuccessStatusCode.Should().BeTrue();
        (await GetDebtBreakdownAsync(client, mainQuery)).OverLimit.Should().BeEmpty();
        (await GetDebtBreakdownAsync(client, "at=2026-10-31")).OverLimit.Select(b => b.BranchId)
            .Should().Equal(ids.BranchBId);
    }

    [Fact]
    public async Task Over_limit_row_of_a_customer_who_owes_nothing_company_wide_keeps_code_and_name()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var branchB = CloneAdminClient();
        UseBranch(branchB, ids.BranchBId);
        var code = "KX" + ids.Kh1Code[3..];
        var customer = await CreatePartnerAsync(code, isCustomer: true, isSupplier: false);
        // The customer owes 1,000 in the main branch, over its limit of 400 there, and has paid 1,000 ahead in branch B:
        // over the whole company it owes nothing, so it is no debtor and only the breach names it.
        await CreateOpeningDebtAsync(client, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD-KX", D(9, 5), 1_000m,
            dueDate: D(9, 20));
        await SetLimitAsync(client, customer, 400m);
        await CreateCashAsync(branchB, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            await CashFundIdAsync(ids.BranchBId), "2026-10-06 09:00", customer, 1_000m));

        var breakdown = await GetDebtBreakdownAsync(client, "at=2026-10-31");

        breakdown.TopDebtors.Select(r => r.PartnerId).Should().NotContain(customer);
        var breach = breakdown.OverLimit.Should().ContainSingle(b => b.PartnerId == customer).Which;
        breach.BranchId.Should().Be(ids.MainBranchId);
        breach.PartnerCode.Should().Be(code);
        breach.PartnerName.Should().Be($"Đối tượng {code}");
        (breach.Balance, breach.Limit, breach.Over).Should().Be((1_000m, 400m, 600m));
    }

    [Fact]
    public async Task By_employee_equals_the_report()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var admin = await InDbAsync(db => db.Users.Where(u => u.Username == "admin")
            .Select(u => new { u.Id, u.FullName }).SingleAsync());

        var breakdown = await GetDebtBreakdownAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");
        var report = await ReadDataAsync<DebtBySalespersonReportDto>(await client.GetAsync(
            "/api/reports/debt-by-salesperson?side=Receivable&from=2026-10-01&to=2026-10-31"));

        // The sale (2,000 left) is the admin's; HD-OLD (1,000 left) has no owner. The return and TT-OLD are unmatched
        // money, not documents owed, so they belong to nobody's total.
        breakdown.ByEmployee.Select(r => (r.OwnerUserId, r.OwnerName, r.Open, r.Overdue)).Should().Equal(
            ((Guid?)admin.Id, (string?)admin.FullName, 2_000m, 2_000m), (null, null, 1_000m, 1_000m));
        AssertByEmployeeEqualsTheReport(breakdown, report);
    }

    [Fact]
    public async Task By_employee_has_the_row_without_an_owner_last_and_counts_what_is_overdue_per_owner()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var suffix = ids.Kh1Code[3..];
        var seller = await CreateClientWithPermissionsAsync("sp2_" + suffix, ids.MainBranchId,
            Permissions.StockOut.View, Permissions.StockOut.Create);
        UseBranch(seller, ids.MainBranchId);
        // A sale of 500 by a second user, not due before 11-15: less than HD-OLD without an owner (1,000), but listed
        // before it, and none of it is overdue.
        var product = await CreateInventoryProductAsync("SP2-" + suffix, track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-20 09:00",
            LineRequest(product, 1, 500m));
        request.WarehouseId = ids.MainWarehouseId;
        request.PartnerId = ids.Kh1Id;
        request.DueDate = D(11, 15);
        var (status, _, error) = await PostVoucherAsync(seller, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        var users = await InDbAsync(db => db.Users
            .Where(u => u.Username == "admin" || u.Username == "sp2_" + suffix)
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToListAsync());
        var admin = users.Single(u => u.Username == "admin");
        var sp2 = users.Single(u => u.Username != "admin");
        // The seller leaves the company: the user is soft-deleted, the documents stay.
        await InDbAsync(db => db.Users.IgnoreQueryFilters().Where(u => u.Id == sp2.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDeleted, true)));

        var breakdown = await GetDebtBreakdownAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");
        var report = await ReadDataAsync<DebtBySalespersonReportDto>(await client.GetAsync(
            "/api/reports/debt-by-salesperson?side=Receivable&from=2026-10-01&to=2026-10-31"));

        breakdown.ByEmployee.Select(r => (r.OwnerUserId, r.OwnerName, r.Open, r.Overdue)).Should().Equal(
            ((Guid?)admin.Id, (string?)admin.FullName, 2_000m, 2_000m),
            ((Guid?)sp2.Id, (string?)sp2.FullName, 500m, 0m),
            (null, null, 1_000m, 1_000m));
        AssertByEmployeeEqualsTheReport(breakdown, report);
    }

    [Fact]
    public async Task Payables_due_within_thirty_days()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var branchCode = await BranchCodeAsync(ids.MainBranchId);
        var purchase = await PostPurchaseAsync(ids, ids.Ncc1Id, 800m, D(11, 15));

        var breakdown = await GetDebtBreakdownAsync(MainClient(ids), $"at=2026-10-21&branchId={ids.MainBranchId}");

        // The purchase of 10-10 is already overdue, and the payment of 3,000 is not a document that falls due.
        var row = breakdown.PayablesDue.Should().ContainSingle().Which;
        row.DocCode.Should().Be(purchase.Code);
        row.DueDate.Should().Be(D(11, 15));
        row.Open.Should().Be(800m);
        row.SourceType.Should().Be(DebtSourceType.StockIn);
        row.SourceId.Should().Be(purchase.Id);
        row.PartnerId.Should().Be(ids.Ncc1Id);
        row.PartnerCode.Should().Be(ids.Ncc1Code);
        row.PartnerName.Should().Be($"Đối tượng {ids.Ncc1Code}");
        row.BranchId.Should().Be(ids.MainBranchId);
        row.BranchCode.Should().Be(branchCode);
    }

    [Fact]
    public async Task Payables_due_after_thirty_days_are_not_listed()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await PostPurchaseAsync(ids, ids.Ncc1Id, 800m, D(11, 25));

        var breakdown = await GetDebtBreakdownAsync(MainClient(ids), $"at=2026-10-21&branchId={ids.MainBranchId}");

        breakdown.PayablesDue.Should().BeEmpty();
    }

    [Fact]
    public async Task Payables_due_window_runs_from_the_given_day_to_thirty_days_after_it()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var yesterday = await PostPurchaseAsync(ids, ids.Ncc1Id, 100m, D(10, 20));
        var today = await PostPurchaseAsync(ids, ids.Ncc1Id, 200m, D(10, 21));
        var lastDay = await PostPurchaseAsync(ids, ids.Ncc1Id, 300m, D(11, 20));
        var afterTheWindow = await PostPurchaseAsync(ids, ids.Ncc1Id, 400m, D(11, 21));

        var breakdown = await GetDebtBreakdownAsync(MainClient(ids), $"at=2026-10-21&branchId={ids.MainBranchId}");

        breakdown.PayablesDue.Select(r => (r.DocCode, r.DueDate, r.Open)).Should().Equal(
            (today.Code, D(10, 21), 200m), (lastDay.Code, D(11, 20), 300m));
        breakdown.PayablesDue.Select(r => r.DocCode).Should().NotContain(new[] { yesterday.Code, afterTheWindow.Code });
    }

    [Fact]
    public async Task Payables_due_list_only_what_the_company_owes()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        // A customer's debt that falls due on 11-10, inside the thirty days: money owed to the company, not by it.
        await CreateOpeningDebtAsync(client, ids.Kh1Id, DebtSide.Receivable, OpeningDebtKind.Debt, "HD-DUE", D(9, 5), 600m,
            dueDate: D(11, 10));
        var purchase = await PostPurchaseAsync(ids, ids.Ncc1Id, 800m, D(11, 15));

        var breakdown = await GetDebtBreakdownAsync(client, $"at=2026-10-21&branchId={ids.MainBranchId}");

        breakdown.PayablesDue.Select(r => (r.DocCode, r.Open)).Should().Equal((purchase.Code, 800m));
    }

    [Fact]
    public async Task Payables_due_are_ordered_by_due_date_then_code_and_cut_at_fifty()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        // 52 payables of NCC1 created from the highest code down: odd numbers fall due on 10-31, even ones on 10-26.
        var created = new List<(string Code, DateOnly Due)>();
        foreach (var number in Enumerable.Range(1, 52).Reverse())
        {
            var code = $"OD-{number:00}";
            var due = number % 2 == 0 ? D(10, 26) : D(10, 31);
            await CreateOpeningDebtAsync(client, ids.Ncc1Id, DebtSide.Payable, OpeningDebtKind.Debt, code, D(9, 5), 10m,
                dueDate: due);
            created.Add((code, due));
        }

        var breakdown = await GetDebtBreakdownAsync(client, $"at=2026-10-21&branchId={ids.MainBranchId}");

        var expected = created.OrderBy(c => c.Due).ThenBy(c => c.Code, StringComparer.Ordinal).Take(50).ToList();
        breakdown.PayablesDue.Select(r => (r.DocCode, r.DueDate)).Should().Equal(expected);
        // The last two odd numbers are the ones that do not fit.
        breakdown.PayablesDue.Select(r => r.DocCode).Should().NotContain(new[] { "OD-49", "OD-51" });
    }

    [Fact]
    public async Task Balance_date_defaults_to_today_in_vn_time()
    {
        var branch = await CreateFreshBranchAsync();
        var before = VnTime.ToVnDate(DateTimeOffset.UtcNow);

        var breakdown = await GetDebtBreakdownAsync(branch.Client, $"branchId={branch.BranchId}");

        // The request may straddle midnight in Vietnam; either day is "today" then.
        breakdown.At.Should().BeOneOf(before, VnTime.ToVnDate(DateTimeOffset.UtcNow));
    }

    /// Every owner has the same open and overdue amounts in the report and in the breakdown. An owner whose documents
    /// are all settled is in the report with zeros and has no row in the breakdown.
    private static void AssertByEmployeeEqualsTheReport(DebtBreakdownDto breakdown, DebtBySalespersonReportDto report)
    {
        breakdown.ByEmployee.Select(r => r.OwnerUserId).Should().BeSubsetOf(report.Rows.Select(r => r.OwnerUserId));
        foreach (var row in report.Rows)
        {
            var mine = breakdown.ByEmployee.SingleOrDefault(r => r.OwnerUserId == row.OwnerUserId);
            (mine?.Open ?? 0m).Should().Be(row.ClosingOpen, $"open of owner {row.OwnerUserId}");
            (mine?.Overdue ?? 0m).Should().Be(row.ClosingOverdue, $"overdue of owner {row.OwnerUserId}");
        }
    }

    private static Task<DebtBreakdownDto> GetDebtBreakdownAsync(HttpClient client, string query) =>
        GetExecAsync<DebtBreakdownDto>(client, $"debt/breakdown?{query}");
}
