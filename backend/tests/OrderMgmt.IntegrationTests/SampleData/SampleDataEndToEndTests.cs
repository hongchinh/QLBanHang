using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.ExecutiveDashboard;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace OrderMgmt.IntegrationTests.SampleData;

/// The whole path with the real pieces: a branch created with sample data through the API is filled by the worker of
/// the test host (real queue, generator, runner and business services), then read through the reports and deleted.
[Collection(nameof(PostgresCollection))]
public class SampleDataEndToEndTests : ExecutiveDashboardTestBase
{
    private static readonly TimeSpan SeedLimit = TimeSpan.FromSeconds(300);

    private readonly ITestOutputHelper _output;

    public SampleDataEndToEndTests(PostgresFixture pg, ITestOutputHelper output) : base(pg) => _output = output;

    [Fact]
    public async Task Demo_branch_is_seeded_and_every_report_has_data()
    {
        // Three owners, as the generator's end state is tuned for.
        foreach (var username in new[] { "sd_e2e_a", "sd_e2e_b", "sd_e2e_c" })
            await CreateTestUserAsync(username, "Pass@123", RoleCodes.Sales);
        var owners = await TinyPlans.SalesOwnersAsync(_factory);
        owners.Should().HaveCount(3);

        var branch = await CreateDemoAndWaitAsync("E2E01");

        (branch.SampleDataStatus, branch.SampleDataProgress).Should().Be((SampleDataStatus.Ready, 100));
        UseBranch(_client, branch.Id);
        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();

        var counts = await InDbAsync(async db => new
        {
            StockVouchers = await db.StockVouchers.CountAsync(v => v.BranchId == branch.Id),
            CashVouchers = await db.CashVouchers.CountAsync(v => v.BranchId == branch.Id),
            FundTransfers = await db.FundTransfers.CountAsync(t => t.BranchId == branch.Id),
            DebtOffsets = await db.DebtOffsetVouchers.CountAsync(o => o.BranchId == branch.Id),
            OpeningDebts = await db.OpeningDebts.CountAsync(o => o.BranchId == branch.Id),
            OpeningStocks = await db.OpeningStocks.CountAsync(o => o.BranchId == branch.Id),
            CancelledCashVouchers = await db.CashVouchers
                .CountAsync(v => v.BranchId == branch.Id && v.Status == DocumentStatus.Cancelled),
        });
        counts.StockVouchers.Should().BeGreaterThanOrEqualTo(150);
        counts.CashVouchers.Should().BeGreaterThanOrEqualTo(60);
        counts.FundTransfers.Should().BeGreaterThanOrEqualTo(12);
        counts.DebtOffsets.Should().Be(1);
        counts.OpeningDebts.Should().Be(16);
        counts.OpeningStocks.Should().Be(36);
        counts.CancelledCashVouchers.Should().BeGreaterThanOrEqualTo(1);

        var opening = branch.OpeningDate!.Value;
        var yesterday = VnToday().AddDays(-1);
        var period = $"from={Iso(opening)}&to={Iso(yesterday)}";

        var onHand = await GetReportAsync<StockOnHandReportDto>("stock-on-hand", "");
        onHand.Rows.Should().NotBeEmpty();
        onHand.Rows.Sum(r => r.Quantity).Should().BePositive();

        var summary = await GetReportAsync<StockSummaryReportDto>("stock-summary", $"{period}&scope=Branch");
        summary.Rows.Sum(r => r.InQty).Should().BePositive();
        summary.Rows.Sum(r => r.OutQty).Should().BePositive();

        var sp01 = await InDbAsync(db => db.Products.Where(p => p.Code == "DM-SP01").Select(p => p.Id).SingleAsync());
        var card = await GetReportAsync<StockCardDto>("stock-card", $"productId={sp01}&{period}");
        card.Rows.Should().NotBeEmpty();

        foreach (var kind in new[] { "Cash", "Bank" })
        {
            var book = await GetReportAsync<CashBookReportDto>("cash-book", $"kind={kind}&{period}");
            book.Groups.SelectMany(g => g.Rows).Should().NotBeEmpty($"the {kind} book has movements");
        }

        var balance = await GetReportAsync<DebtBalanceReportDto>("debt-balance", $"side=Receivable&{period}");
        balance.Totals.ClosingDebit.Should().BePositive();

        // The aging report's default buckets count the age from the document date, from day 0 (the overdue report's
        // count the days overdue, from day 1).
        var aging = await GetReportAsync<DebtAgingReportDto>("debt-aging", $"side=Receivable&at={Iso(yesterday)}");
        aging.Buckets.Select(b => (b.From, b.To)).Should().Equal(new (int, int?)[] { (0, 30), (31, 60), (61, 90), (91, null) });
        aging.Totals.Amounts.Should().HaveCount(4).And.OnlyContain(amount => amount > 0);

        var overdue = await GetReportAsync<DebtOverdueReportDto>("debt-overdue", $"side=Receivable&at={Iso(yesterday)}");
        overdue.Groups.Should().NotBeEmpty();

        var bySalesperson = await GetReportAsync<DebtBySalespersonReportDto>("debt-by-salesperson",
            $"side=Receivable&{period}");
        bySalesperson.Rows.Select(r => r.OwnerUserId).Should().BeEquivalentTo(owners.Select(o => (Guid?)o));

        var customer = await InDbAsync(db => db.Customers.Where(c => c.Code == "DM-KH01").Select(c => c.Id).SingleAsync());
        var s12 = await _client.PostAsJsonAsync("/api/reports/debt-ledger/export", new Dictionary<string, object?>
        {
            ["side"] = "Receivable",
            ["from"] = Iso(opening),
            ["to"] = Iso(yesterday),
            ["partnerIds"] = new[] { customer },
            ["scope"] = "Branch",
        }, TestJson.Options);
        s12.StatusCode.Should().Be(HttpStatusCode.OK, await s12.Content.ReadAsStringAsync());

        var sales = await GetSummaryAsync(_client, $"{period}&branchId={branch.Id}");
        sales.Revenue.Should().BePositive();
    }

    [Fact]
    public async Task Seeding_succeeds_under_block_policies()
    {
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        await UpdateCashDebtSettingsAsync(s =>
        {
            s.CreditLimitPolicy = LimitPolicy.Block;
            s.NegativeFundPolicy = LimitPolicy.Block;
        });

        var branch = await CreateDemoAndWaitAsync("E2E02");

        (branch.SampleDataStatus, branch.SampleDataProgress).Should().Be((SampleDataStatus.Ready, 100));
        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    /// With VAT left out of the net amount, the vouchers move less cash than with it: the plan's cash estimates must
    /// follow, or a fixed deposit drains the cash fund, which a Block fund policy refuses.
    [Fact]
    public async Task Seeding_succeeds_with_net_excludes_vat_under_block_fund_policy()
    {
        await UpdateInventorySettingsAsync(s => s.NetExcludesVat = true);
        await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Block);

        var branch = await CreateDemoAndWaitAsync("E2E05");

        (branch.SampleDataStatus, branch.SampleDataProgress).Should().Be((SampleDataStatus.Ready, 100));
        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Two_demo_branches_share_the_catalog_and_go_away_cleanly()
    {
        await ReportDataset.SeedAsync(this);
        // A window that holds both the report dataset (October 2026) and the demo branches' documents.
        var today = VnToday();
        var demoOpening = new DateOnly(today.Year, today.Month, 1).AddMonths(-3);
        var from = Min(demoOpening, ReportDataset.OpeningDate);
        var to = Max(today.AddDays(-1), new DateOnly(2026, 10, 31));
        var companyQuery = $"side=Receivable&from={Iso(from)}&to={Iso(to)}&scope=Company";
        var c0 = (await GetReportAsync<DebtBalanceReportDto>("debt-balance", companyQuery)).Totals;

        var e2e03 = await CreateDemoAndWaitAsync("E2E03");
        var e2e04 = await CreateDemoAndWaitAsync("E2E04");

        e2e03.SampleDataStatus.Should().Be(SampleDataStatus.Ready);
        e2e04.SampleDataStatus.Should().Be(SampleDataStatus.Ready);
        (await InDbAsync(db => db.Products.IgnoreQueryFilters().CountAsync(p => p.Code == "DM-SP01"))).Should().Be(1);
        var demoClient = CloneAdminClient();
        UseBranch(demoClient, e2e03.Id);
        var demoTotals = (await ReadDataAsync<DebtBalanceReportDto>(await demoClient.GetAsync(
            $"/api/reports/debt-balance?side=Receivable&from={Iso(from)}&to={Iso(to)}"))).Totals;
        demoTotals.ClosingDebit.Should().BePositive("the demo branch has receivables the company total must not count");
        (await GetReportAsync<DebtBalanceReportDto>("debt-balance", companyQuery)).Totals.Should().BeEquivalentTo(c0);

        await DeleteDemoAsync(e2e03);
        (await SampleCatalogRowCountAsync()).Should().BePositive("E2E04 still uses the sample catalog");
        (await InDbAsync(db => db.Products.IgnoreQueryFilters().CountAsync(p => p.Code == "DM-SP01"))).Should().Be(1);

        await DeleteDemoAsync(e2e04);
        (await SampleCatalogRowCountAsync()).Should().Be(0, "the last demo delete removes the sample catalog");
        (await InDbAsync(db => db.ProductGroups.IgnoreQueryFilters().CountAsync(g => g.Code == "DM"))).Should().Be(0);

        (await GetReportAsync<DebtBalanceReportDto>("debt-balance", companyQuery)).Totals.Should().BeEquivalentTo(c0);
        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    /// POST /api/branches with sample data, then GET /api/branches every second until the branch leaves Running (at
    /// most SeedLimit). Fails with the branch's error when it ends Failed.
    private async Task<BranchDto> CreateDemoAndWaitAsync(string code)
    {
        var stopwatch = Stopwatch.StartNew();
        var created = await ReadDataAsync<BranchDto>(await _client.PostAsJsonAsync("/api/branches",
            new CreateBranchRequest { Code = code, Name = $"Chi nhánh demo {code}", WithSampleData = true }));
        created.SampleDataStatus.Should().Be(SampleDataStatus.Running);

        var branch = created;
        while (branch.SampleDataStatus == SampleDataStatus.Running && stopwatch.Elapsed < SeedLimit)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            var branches = await ReadDataAsync<List<BranchDto>>(await _client.GetAsync("/api/branches"));
            branch = branches.Single(b => b.Id == created.Id);
        }
        stopwatch.Stop();
        _output.WriteLine($"{code}: {branch.SampleDataStatus} ({branch.SampleDataProgress}%) after "
            + $"{stopwatch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s");

        if (branch.SampleDataStatus == SampleDataStatus.Failed)
            Assert.Fail($"{code} failed: {branch.SampleDataError}");
        branch.SampleDataStatus.Should().Be(SampleDataStatus.Ready,
            $"{code} is still {branch.SampleDataStatus} at {branch.SampleDataProgress}% after {SeedLimit.TotalSeconds} s");
        await WriteDatasetAsync(code, branch.Id);
        return branch;
    }

    /// The time split (catalog, the rest of the setup, the document steps) read from the rows' CreatedAt, and the
    /// documents per type.
    private async Task WriteDatasetAsync(string code, Guid branchId)
    {
        var times = await InDbAsync(async db => new
        {
            Created = await db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.CreatedAt).SingleAsync(),
            FirstWarehouse = await db.Warehouses.IgnoreQueryFilters().Where(w => w.BranchId == branchId)
                .MinAsync(w => (DateTimeOffset?)w.CreatedAt),
            FirstStep = await db.StockVouchers.IgnoreQueryFilters().Where(v => v.BranchId == branchId)
                .MinAsync(v => (DateTimeOffset?)v.CreatedAt),
            LastStock = await db.StockVouchers.IgnoreQueryFilters().Where(v => v.BranchId == branchId)
                .MaxAsync(v => (DateTimeOffset?)(v.UpdatedAt ?? v.CreatedAt)),
            LastCash = await db.CashVouchers.IgnoreQueryFilters().Where(v => v.BranchId == branchId)
                .MaxAsync(v => (DateTimeOffset?)(v.UpdatedAt ?? v.CreatedAt)),
            LastTransfer = await db.FundTransfers.IgnoreQueryFilters().Where(t => t.BranchId == branchId)
                .MaxAsync(t => (DateTimeOffset?)(t.UpdatedAt ?? t.CreatedAt)),
        });
        var lastStep = new[] { times.LastStock, times.LastCash, times.LastTransfer }.Max();
        _output.WriteLine($"{code}: catalog {Seconds(times.Created, times.FirstWarehouse)} s, "
            + $"rest of the setup {Seconds(times.FirstWarehouse, times.FirstStep)} s, "
            + $"steps {Seconds(times.FirstStep, lastStep)} s");

        var stock = await InDbAsync(db => db.StockVouchers.Where(v => v.BranchId == branchId)
            .GroupBy(v => v.Reason!.Code).Select(g => new { g.Key, Count = g.Count() }).ToListAsync());
        var lines = await InDbAsync(db => db.StockVoucherLines.CountAsync(l => l.StockVoucher!.BranchId == branchId));
        var cash = await InDbAsync(db => db.CashVouchers.Where(v => v.BranchId == branchId)
            .GroupBy(v => new { Auto = v.SourceStockVoucherId != null, v.Reason!.Code })
            .Select(g => new { g.Key.Auto, g.Key.Code, Count = g.Count() }).ToListAsync());
        var others = await InDbAsync(async db => new
        {
            Transfers = await db.FundTransfers.CountAsync(t => t.BranchId == branchId),
            Offsets = await db.DebtOffsetVouchers.CountAsync(o => o.BranchId == branchId),
            OpeningDebts = await db.OpeningDebts.CountAsync(o => o.BranchId == branchId),
            OpeningStocks = await db.OpeningStocks.CountAsync(o => o.BranchId == branchId),
        });
        _output.WriteLine($"{code}: stock vouchers {stock.Sum(s => s.Count)} ("
            + string.Join(", ", stock.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key} {s.Count}"))
            + $"), {lines} lines");
        _output.WriteLine($"{code}: cash vouchers {cash.Sum(c => c.Count)} ("
            + string.Join(", ", cash.OrderBy(c => c.Auto).ThenBy(c => c.Code, StringComparer.Ordinal)
                .Select(c => $"{(c.Auto ? "auto " : "")}{c.Code} {c.Count}"))
            + $"), transfers {others.Transfers}, offsets {others.Offsets}, opening debts {others.OpeningDebts}, "
            + $"opening stock rows {others.OpeningStocks}");
    }

    private async Task<DemoBranchDeletionResultDto> DeleteDemoAsync(BranchDto branch)
    {
        var stopwatch = Stopwatch.StartNew();
        var (status, result, error) = await ReadDocumentResponseAsync<DemoBranchDeletionResultDto>(
            await _client.DeleteAsync($"/api/branches/{branch.Id}/demo"));
        stopwatch.Stop();
        _output.WriteLine($"DELETE {branch.Code}: {stopwatch.ElapsedMilliseconds} ms");
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return result!;
    }

    /// Products and partners with a sample code (DM-SP*, DM-KH*, DM-NCC*, DM-DT*), soft-deleted ones included.
    private Task<int> SampleCatalogRowCountAsync() => InDbAsync(async db =>
        await db.Products.IgnoreQueryFilters().CountAsync(p => p.Code.StartsWith("DM-SP") || p.Code.StartsWith("DM-KH")
            || p.Code.StartsWith("DM-NCC") || p.Code.StartsWith("DM-DT"))
        + await db.Customers.IgnoreQueryFilters().CountAsync(c => c.Code.StartsWith("DM-SP") || c.Code.StartsWith("DM-KH")
            || c.Code.StartsWith("DM-NCC") || c.Code.StartsWith("DM-DT")));

    private async Task<T> GetReportAsync<T>(string route, string query) =>
        await ReadDataAsync<T>(await _client.GetAsync($"/api/reports/{route}?{query}"));

    private static DateOnly VnToday() => VnTime.ToVnDate(DateTimeOffset.UtcNow);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Seconds(DateTimeOffset? from, DateTimeOffset? to) =>
        from is { } f && to is { } t ? (t - f).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) : "?";

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
