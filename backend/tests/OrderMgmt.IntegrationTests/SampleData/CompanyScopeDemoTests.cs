using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Tt133;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Plan S15: company-scope reports ignore demo branches; a demo branch named explicitly still reports.
[Collection(nameof(PostgresCollection))]
public class CompanyScopeDemoTests : CashDebtTestBase
{
    private const string October = "from=2026-10-01&to=2026-10-31";

    public CompanyScopeDemoTests(PostgresFixture pg) : base(pg) { }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private Task MarkDemoAsync(Guid branchId, bool alsoDeleted = false) =>
        InDbAsync(db => db.Branches.Where(b => b.Id == branchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsDemo, true).SetProperty(b => b.IsDeleted, alsoDeleted)));

    private static async Task<T> GetAsync<T>(HttpClient client, string route, string query) =>
        await ReadDataAsync<T>(await client.GetAsync($"/api/reports/{route}?{query}"));

    [Fact]
    public async Task Debt_balance_company_skips_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var query = $"side=Receivable&partnerId={ids.Kh1Id}&{October}";

        var before = await GetAsync<DebtBalanceReportDto>(client, "debt-balance", query + "&scope=Company");
        var main = await GetAsync<DebtBalanceReportDto>(client, "debt-balance", query);
        before.Rows.Should().ContainSingle().Which.Increase.Should().NotBe(main.Rows.Single().Increase);

        await MarkDemoAsync(ids.BranchBId);

        var after = await GetAsync<DebtBalanceReportDto>(client, "debt-balance", query + "&scope=Company");
        var row = after.Rows.Should().ContainSingle().Subject;
        var mainRow = main.Rows.Should().ContainSingle().Subject;
        row.Increase.Should().Be(mainRow.Increase);
        row.Decrease.Should().Be(mainRow.Decrease);
        row.OpeningDebit.Should().Be(mainRow.OpeningDebit);
        row.ClosingDebit.Should().Be(mainRow.ClosingDebit);
    }

    [Fact]
    public async Task Debt_ledger_company_skips_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var query = $"side=Receivable&partnerId={ids.Kh1Id}&{October}";
        var codeB = await InDbAsync(db => db.Branches.IgnoreQueryFilters().Where(b => b.Id == ids.BranchBId)
            .Select(b => b.Code).SingleAsync());

        var before = await GetAsync<DebtLedgerReportDto>(client, "debt-ledger", query + "&scope=Company");
        before.Rows.Should().Contain(r => r.BranchCode == codeB);
        var main = await GetAsync<DebtLedgerReportDto>(client, "debt-ledger", query);

        await MarkDemoAsync(ids.BranchBId);

        var after = await GetAsync<DebtLedgerReportDto>(client, "debt-ledger", query + "&scope=Company");
        after.Rows.Should().NotContain(r => r.BranchCode == codeB);
        after.OpeningBalance.Should().Be(main.OpeningBalance);
        after.TotalIncrease.Should().Be(main.TotalIncrease);
        after.ClosingBalance.Should().Be(main.ClosingBalance);
    }

    [Fact]
    public async Task Debt_reconciliation_company_skips_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var query = $"partnerId={ids.Kh1Id}&{October}";

        var before = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query + "&scope=Company");
        var main = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query);
        before.Sides.Should().ContainSingle().Which.ClosingBalance
            .Should().NotBe(main.Sides.Should().ContainSingle().Subject.ClosingBalance);

        await MarkDemoAsync(ids.BranchBId);

        var after = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query + "&scope=Company");
        var side = after.Sides.Should().ContainSingle().Subject;
        var mainSide = main.Sides.Single();
        side.ClosingBalance.Should().Be(mainSide.ClosingBalance);
        side.TotalIncrease.Should().Be(mainSide.TotalIncrease);
        side.OpeningBalance.Should().Be(mainSide.OpeningBalance);
    }

    [Fact]
    public Task Stock_summary_company_skips_demo_branch() => Stock_summary_Async(softDeleted: false);

    [Fact]
    public Task Stock_summary_company_skips_soft_deleted_demo_branch() => Stock_summary_Async(softDeleted: true);

    private async Task Stock_summary_Async(bool softDeleted)
    {
        var ids = await ReportDataset.SeedAsync(this);
        var warehouseB = await InDbAsync(db => db.Warehouses.IgnoreQueryFilters()
            .Where(w => w.BranchId == ids.BranchBId).Select(w => w.Id).FirstAsync());
        var code = "SD" + Guid.NewGuid().ToString("N")[..8];
        var product = await CreateInventoryProductAsync(code);
        var mainClient = MainBranchClient(ids);
        var clientB = CloneAdminClient();
        UseBranch(clientB, ids.BranchBId);
        await PostPurchaseAsync(mainClient, ids.MainWarehouseId, ids.Ncc1Id, product, 10m);
        await PostPurchaseAsync(clientB, warehouseB, ids.Ncc1Id, product, 7m);
        var query = $"{October}&search={code}";

        var before = await GetAsync<StockSummaryReportDto>(mainClient, "stock-summary", query + "&scope=Company");
        var main = await GetAsync<StockSummaryReportDto>(mainClient, "stock-summary", query);
        before.Rows.Should().ContainSingle().Which.InQty.Should().Be(17m);

        await MarkDemoAsync(ids.BranchBId, softDeleted);

        var after = await GetAsync<StockSummaryReportDto>(mainClient, "stock-summary", query + "&scope=Company");
        var row = after.Rows.Should().ContainSingle().Subject;
        var mainRow = main.Rows.Should().ContainSingle().Subject;
        row.InQty.Should().Be(10m).And.Be(mainRow.InQty);
        row.ClosingQty.Should().Be(mainRow.ClosingQty);
        after.TotalInValue.Should().Be(main.TotalInValue);
        after.TotalClosingValue.Should().Be(main.TotalClosingValue);
    }

    [Fact]
    public async Task Tt133_company_opening_date_skips_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var mainDate = ReportDataset.OpeningDate;
        var dateB = mainDate.AddMonths(-1);
        await InDbAsync(db => db.Branches.Where(b => b.Id == ids.BranchBId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.OpeningDate, dateB)));
        var client = MainBranchClient(ids);

        (await OpeningLineAsync(client, ids)).Should().Be($"- Ngày mở sổ: {dateB:dd/MM/yyyy}");

        await MarkDemoAsync(ids.BranchBId);

        (await OpeningLineAsync(client, ids)).Should().Be($"- Ngày mở sổ: {mainDate:dd/MM/yyyy}");
    }

    [Fact]
    public async Task Soft_deleted_demo_branch_stays_excluded_from_company_scope()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var query = $"partnerId={ids.Kh1Id}&{October}";
        var main = await GetAsync<DebtBalanceReportDto>(client, "debt-balance", $"side=Receivable&{query}");
        var mainLedger = await GetAsync<DebtLedgerReportDto>(client, "debt-ledger", $"side=Receivable&{query}");
        var mainReconciliation = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query);

        await InDbAsync(db => db.Branches.Where(b => b.Id == ids.BranchBId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsDemo, true).SetProperty(b => b.IsDeleted, true)));

        var balance = await GetAsync<DebtBalanceReportDto>(client, "debt-balance", $"side=Receivable&{query}&scope=Company");
        balance.Rows.Should().ContainSingle().Which.Increase.Should().Be(main.Rows.Single().Increase);
        var ledger = await GetAsync<DebtLedgerReportDto>(client, "debt-ledger", $"side=Receivable&{query}&scope=Company");
        ledger.ClosingBalance.Should().Be(mainLedger.ClosingBalance);
        var reconciliation = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query + "&scope=Company");
        reconciliation.Sides.Should().ContainSingle().Which.ClosingBalance
            .Should().Be(mainReconciliation.Sides.Single().ClosingBalance);
    }

    [Fact]
    public Task Reconciliation_company_side_list_skips_demo_branch() => Reconciliation_side_list_Async(softDeleted: false);

    [Fact]
    public Task Reconciliation_company_side_list_skips_soft_deleted_demo_branch() => Reconciliation_side_list_Async(softDeleted: true);

    private async Task Reconciliation_side_list_Async(bool softDeleted)
    {
        var ids = await ReportDataset.SeedAsync(this);
        var warehouseB = await InDbAsync(db => db.Warehouses.IgnoreQueryFilters()
            .Where(w => w.BranchId == ids.BranchBId).Select(w => w.Id).FirstAsync());
        // Branch B sells to the supplier NCC1 (made a customer for the sale only), so only B gives it a Receivable side.
        await InDbAsync(db => db.Customers.Where(c => c.Id == ids.Ncc1Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsCustomer, true)));
        var clientB = CloneAdminClient();
        UseBranch(clientB, ids.BranchBId);
        await ReportDataset.PostStockAsync(this, clientB, StockDirection.Out, "XBH", "2026-10-06 09:00", warehouseB,
            ids.Ncc1Id, 100m, paymentMethodId: null, paid: 0m);
        await InDbAsync(db => db.Customers.Where(c => c.Id == ids.Ncc1Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsCustomer, false)));
        var client = MainBranchClient(ids);
        var query = $"partnerId={ids.Ncc1Id}&{October}&scope=Company";

        var before = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query);
        before.Sides.Select(s => s.Side).Should().BeEquivalentTo(new[] { DebtSide.Receivable, DebtSide.Payable });

        await MarkDemoAsync(ids.BranchBId, softDeleted);

        var after = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query);
        after.Sides.Select(s => s.Side).Should().Equal(DebtSide.Payable);
    }

    [Fact]
    public async Task Demo_branch_report_still_works_when_named()
    {
        var ids = await ReportDataset.SeedAsync(this);
        await MarkDemoAsync(ids.BranchBId);
        var clientB = CloneAdminClient();
        UseBranch(clientB, ids.BranchBId);

        var report = await GetAsync<DebtBalanceReportDto>(clientB, "debt-balance",
            $"side=Receivable&partnerId={ids.Kh1Id}&{October}&scope=Branch");

        var row = report.Rows.Should().ContainSingle().Subject;
        row.Increase.Should().Be(9_999m);
        row.ClosingDebit.Should().Be(9_999m);
    }

    private async Task PostPurchaseAsync(HttpClient client, Guid warehouseId, Guid supplier, Guid product, decimal qty)
    {
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-10-05 09:00",
            LineRequest(product, qty, 100m));
        request.WarehouseId = warehouseId;
        request.PartnerId = supplier;
        request.PaidAmount = 0m;
        var (status, _, error) = await PostVoucherAsync(client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
    }

    /// The "- Ngày mở sổ: …" line of the company-scope S12 export.
    private static async Task<string?> OpeningLineAsync(HttpClient client, ReportIds ids)
    {
        var response = await client.PostAsJsonAsync("/api/reports/debt-ledger/export", new Dictionary<string, object?>
        {
            ["side"] = "Receivable",
            ["from"] = "2026-10-01",
            ["to"] = "2026-10-31",
            ["partnerIds"] = new[] { ids.Kh1Id },
            ["scope"] = "Company",
        }, TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Should().ContainSingle().Subject;
        return ws.CellsUsed().Select(c => c.GetString()).FirstOrDefault(s => s.StartsWith("- Ngày mở sổ:"));
    }
}
