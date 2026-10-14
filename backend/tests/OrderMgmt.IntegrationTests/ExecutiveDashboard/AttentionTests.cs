using System.Net;
using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// attention: what needs action at the balance date (overdue receivables, credit limits exceeded, payables due within
/// seven days, stock below zero, funds below zero). Each item is shown only to a user who may read its report.
[Collection(nameof(PostgresCollection))]
public class AttentionTests : ExecutiveDashboardTestBase
{
    public AttentionTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Debt_items_of_the_dataset()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var attention = await GetAttentionAsync(MainClient(ids), $"at=2026-10-31&branchId={ids.MainBranchId}");

        attention.At.Should().Be(D(10, 31));
        AssertItem(attention.OverdueReceivables, 1, 3_000m);
        AssertItem(attention.OverCreditLimit, 0, 0m);
        AssertItem(attention.PayablesDueSoon, 0, 0m);
        AssertItem(attention.NegativeStock, 0, null);
        AssertItem(attention.NegativeFunds, 0, 0m);
    }

    [Fact]
    public async Task Over_credit_limit()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        await SetLimitAsync(client, ids.Kh1Id, 1_000m);

        var attention = await GetAttentionAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");

        AssertItem(attention.OverCreditLimit, 1, 500m);
    }

    [Fact]
    public async Task Payables_due_soon()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await PostPurchaseAsync(ids, ids.Ncc1Id, 800m, D(10, 25));

        var attention = await GetAttentionAsync(MainClient(ids), $"at=2026-10-21&branchId={ids.MainBranchId}");
        var dayAfterDue = await GetAttentionAsync(MainClient(ids), $"at=2026-10-26&branchId={ids.MainBranchId}");

        AssertItem(attention.PayablesDueSoon, 1, 800m);
        // On the 26th the purchase is a day late, no longer due soon.
        AssertItem(dayAfterDue.PayablesDueSoon, 0, 0m);
    }

    [Fact]
    public async Task Negative_fund()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var payment = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"), ids.QtmId, "2026-10-20 09:00",
            partnerId: null, 50_000m);
        payment.PersonName = "Nguyễn C";
        payment.AcknowledgeNegativeFund = true;
        await CreateCashAsync(client, payment);

        var attention = await GetAttentionAsync(client, $"at=2026-10-31&branchId={ids.MainBranchId}");
        var dayBefore = await GetAttentionAsync(client, $"at=2026-10-19&branchId={ids.MainBranchId}");
        var company = await GetAttentionAsync(client, "at=2026-10-31");

        // QTM holds 19,300 at the 31st before the payment: 50,000 takes it to -30,700.
        AssertItem(attention.NegativeFunds, 1, 30_700m);
        // On the 19th the payment is not made yet.
        AssertItem(dayBefore.NegativeFunds, 0, 0m);
        // The cash funds of the other branches are at zero, which is not below zero.
        AssertItem(company.NegativeFunds, 1, 30_700m);
    }

    [Fact]
    public async Task Negative_stock()
    {
        var ids = await ReportDataset.SeedAsync(this);
        // A product in stock beside the one sold without stock: only the second is below zero.
        var inStock = await CreateInventoryProductAsync("POS-" + Guid.NewGuid().ToString("N")[..8]);
        var purchase = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-10-19 09:00",
            LineRequest(inStock, 1, 10m));
        purchase.WarehouseId = ids.MainWarehouseId;
        purchase.PartnerId = ids.Ncc1Id;
        var (purchaseStatus, _, purchaseError) = await PostVoucherAsync(MainClient(ids), purchase);
        purchaseStatus.Should().Be(HttpStatusCode.OK, purchaseError?.Message);
        var product = await CreateInventoryProductAsync("NEG-" + Guid.NewGuid().ToString("N")[..8]);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-20 09:00",
            LineRequest(product, 3, 100m));
        request.WarehouseId = ids.MainWarehouseId;
        request.PartnerId = ids.Kh1Id;
        request.AcknowledgeNegativeStock = true;
        var (status, _, error) = await PostVoucherAsync(MainClient(ids), request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        var attention = await GetAttentionAsync(MainClient(ids), $"at=2026-10-31&branchId={ids.MainBranchId}");

        AssertItem(attention.NegativeStock, 1, null);
    }

    [Fact]
    public async Task Parts_follow_the_permissions()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await CreateClientWithPermissionsAsync("cash_only", ids.MainBranchId, Permissions.Dashboard.Executive,
            Permissions.Reports.Cash);

        var attention = await GetAttentionAsync(user, "at=2026-10-31");

        attention.OverdueReceivables.Should().BeNull();
        attention.OverCreditLimit.Should().BeNull();
        attention.PayablesDueSoon.Should().BeNull();
        attention.NegativeStock.Should().BeNull();
        AssertItem(attention.NegativeFunds, 0, 0m);
    }

    [Fact]
    public async Task Needs_only_dashboard_executive()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await CreateClientWithPermissionsAsync("executive_only", ids.MainBranchId,
            Permissions.Dashboard.Executive);

        var attention = await GetAttentionAsync(user, "at=2026-10-31");

        attention.At.Should().Be(D(10, 31));
        attention.OverdueReceivables.Should().BeNull();
        attention.OverCreditLimit.Should().BeNull();
        attention.PayablesDueSoon.Should().BeNull();
        attention.NegativeStock.Should().BeNull();
        attention.NegativeFunds.Should().BeNull();
    }

    [Fact]
    public async Task Foreign_branch_is_refused()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var own = await CreateBranchAsync("AA" + suffix);
        var other = await CreateBranchAsync("AB" + suffix);
        var user = await CreateClientWithPermissionsAsync("executive_only", own, Permissions.Dashboard.Executive);

        (await GetExecRawAsync(user, $"attention?at=2026-10-31&branchId={other}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Own_branch_by_default()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await CreateClientWithPermissionsAsync("debt_only", ids.MainBranchId, Permissions.Dashboard.Executive,
            Permissions.Reports.Debt);

        var mine = await GetAttentionAsync(user, "at=2026-10-31");
        var company = await GetAttentionAsync(CloneAdminClient(), "at=2026-10-31");

        // Branch B's unpaid sale of 9,999 is overdue too, but only the admin reads branch B.
        mine.OverdueReceivables!.Amount.Should().Be(3_000m);
        company.OverdueReceivables!.Amount.Should().Be(12_999m);
        // reports.debt shows the debt items only.
        mine.NegativeStock.Should().BeNull();
        mine.NegativeFunds.Should().BeNull();
    }

    [Fact]
    public async Task Debt_items_are_as_at_the_given_date()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var attention = await GetAttentionAsync(MainClient(ids), $"at=2026-10-03&branchId={ids.MainBranchId}");

        // On the 3rd HD-OLD is still whole and late; the sale of that day is not overdue yet.
        attention.At.Should().Be(D(10, 3));
        AssertItem(attention.OverdueReceivables, 1, 5_000m);
    }

    [Fact]
    public async Task Stock_part_needs_only_reports_inventory()
    {
        var branch = await CreateFreshBranchAsync();
        var user = await CreateClientWithPermissionsAsync("inventory_only", branch.BranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Inventory);

        var attention = await GetAttentionAsync(user, "at=2026-10-31");

        AssertItem(attention.NegativeStock, 0, null);
        attention.OverdueReceivables.Should().BeNull();
        attention.OverCreditLimit.Should().BeNull();
        attention.PayablesDueSoon.Should().BeNull();
        attention.NegativeFunds.Should().BeNull();
    }

    private static void AssertItem(AttentionItemDto? item, int count, decimal? amount)
    {
        item.Should().NotBeNull();
        (item!.Count, item.Amount).Should().Be((count, amount));
    }

    private static Task<AttentionDto> GetAttentionAsync(HttpClient client, string query) =>
        GetExecAsync<AttentionDto>(client, $"attention?{query}");
}
