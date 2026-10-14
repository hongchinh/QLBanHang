using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// recent: the receipts, payments and stock vouchers created today (VN), newest first. Automatic cash vouchers are left
/// out (their stock voucher is listed), and so are cancelled and deleted documents. The dataset's documents are all
/// created by the test, so all of them are "today".
[Collection(nameof(PostgresCollection))]
public class RecentDocumentsTests : ExecutiveDashboardTestBase
{
    public RecentDocumentsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Documents_created_today_company_wide()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var admin = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.FullName).SingleAsync());

        var rows = await GetRecentAsync(CloneAdminClient(), "limit=50");

        // In the order the dataset created them, newest first. PT00001 and PC00001 are automatic, PT00003 is cancelled.
        rows.Select(r => (r.Id, r.Kind)).Should().Equal(
            (ids.BranchBSaleId, RecentDocumentKind.StockOut),
            (ids.Payment10Id, RecentDocumentKind.Payment),
            (ids.Payment8Id, RecentDocumentKind.Payment),
            (ids.Purchase7Id, RecentDocumentKind.StockIn),
            (ids.Return6Id, RecentDocumentKind.StockIn),
            (ids.Receipt5Id, RecentDocumentKind.Receipt),
            (ids.SaleId, RecentDocumentKind.StockOut));
        rows.Select(r => r.CreatedAt).Should().BeInDescendingOrder();
        rows.Where(r => r.Kind is RecentDocumentKind.Receipt or RecentDocumentKind.Payment).Select(r => r.Code)
            .Should().Equal("PC00003", "PC00002", "PT00002");

        var sale = rows.Single(r => r.Id == ids.SaleId);
        sale.Amount.Should().Be(3_000m);
        sale.PartnerName.Should().Be($"Đối tượng {ids.Kh1Code}");
        sale.OwnerName.Should().Be(admin);
        sale.BranchCode.Should().Be(await BranchCodeAsync(ids.MainBranchId));
        // A payment to a person without a partner shows the person.
        var payment = rows.Single(r => r.Id == ids.Payment10Id);
        payment.PartnerName.Should().Be("Nguyễn A");
        payment.Amount.Should().Be(700m);
    }

    [Fact]
    public async Task One_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        // A receipt of branch B from KH1, handed in by a person: the partner's name is the one shown.
        var branchB = CloneAdminClient();
        UseBranch(branchB, ids.BranchBId);
        var receiptRequest = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            await CashFundIdAsync(ids.BranchBId), "2026-10-06 09:00", ids.Kh1Id, 300m);
        receiptRequest.PersonName = "Nguyễn E";
        var branchBReceipt = await CreateCashAsync(branchB, receiptRequest);

        var rows = await GetRecentAsync(CloneAdminClient(), $"limit=50&branchId={ids.MainBranchId}");
        var company = await GetRecentAsync(CloneAdminClient(), "limit=50");

        using (new AssertionScope())
        {
            rows.Should().HaveCount(6);
            rows.Select(r => r.Id).Should().NotContain(new[] { ids.BranchBSaleId, branchBReceipt.Id });
            company.Single(r => r.Id == branchBReceipt.Id).PartnerName.Should().Be($"Đối tượng {ids.Kh1Code}");
        }
    }

    [Fact]
    public async Task Documents_created_before_today_are_left_out()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var startOfToday = VnTime.ToUtc(VnTime.StartOfDay(VnTime.ToVnDate(DateTimeOffset.UtcNow)));
        var lastMicrosecondOfYesterday = startOfToday.AddTicks(-10);
        // A receipt and a return created a microsecond before midnight, a payment and a purchase at midnight itself.
        await InDbAsync(async db =>
        {
            await db.CashVouchers.Where(v => v.Id == ids.Receipt5Id)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.CreatedAt, lastMicrosecondOfYesterday));
            await db.CashVouchers.Where(v => v.Id == ids.Payment8Id)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.CreatedAt, startOfToday));
            await db.StockVouchers.Where(v => v.Id == ids.Return6Id)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.CreatedAt, lastMicrosecondOfYesterday));
            await db.StockVouchers.Where(v => v.Id == ids.Purchase7Id)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.CreatedAt, startOfToday));
        });

        var rows = await GetRecentAsync(CloneAdminClient(), $"limit=50&branchId={ids.MainBranchId}");

        // PC00002 and PN00002 were created at the same instant: the code decides.
        rows.Select(r => r.Id).Should().Equal(ids.Payment10Id, ids.SaleId, ids.Payment8Id, ids.Purchase7Id);
    }

    [Fact]
    public async Task Cancelled_stock_vouchers_are_left_out()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var product = await CreateInventoryProductAsync("CAN-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-20 09:00",
            LineRequest(product, 1, 700m));
        request.WarehouseId = ids.MainWarehouseId;
        request.PartnerId = ids.Kh1Id;
        var (status, sale, error) = await PostVoucherAsync(client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await client.PostAsJsonAsync($"/api/stock-vouchers/{sale!.Id}/cancel",
            new StockVoucherActionRequest { Version = sale.Version })).StatusCode.Should().Be(HttpStatusCode.OK);

        var rows = await GetRecentAsync(CloneAdminClient(), $"limit=50&branchId={ids.MainBranchId}");

        rows.Should().HaveCount(6);
        rows.Select(r => r.Id).Should().NotContain(sale.Id);
    }

    [Fact]
    public async Task Deleted_documents_are_left_out()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        var product = await CreateInventoryProductAsync("DEL-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-20 09:00",
            LineRequest(product, 1, 700m));
        request.WarehouseId = ids.MainWarehouseId;
        request.PartnerId = ids.Kh1Id;
        var (status, sale, error) = await PostVoucherAsync(client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        var receiptRequest = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), ids.QtmId,
            "2026-10-20 09:00", partnerId: null, 200m);
        receiptRequest.PersonName = "Nguyễn D";
        var receipt = await CreateCashAsync(client, receiptRequest);

        (await client.DeleteAsync($"/api/stock-vouchers/{sale!.Id}?version={sale.Version}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var (deleteStatus, _, deleteError) = await DeleteCashAsync(client, receipt.Id,
            new CashDocumentActionRequest { Version = receipt.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);

        var rows = await GetRecentAsync(CloneAdminClient(), $"limit=50&branchId={ids.MainBranchId}");

        rows.Should().HaveCount(6);
        rows.Select(r => r.Id).Should().NotContain(new[] { sale.Id, receipt.Id });
    }

    [Fact]
    public async Task Limit()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var admin = CloneAdminClient();

        (await GetRecentAsync(admin, "limit=2")).Select(r => r.Id).Should().Equal(ids.BranchBSaleId, ids.Payment10Id);
        await AssertBadRequestKeyAsync(admin, "recent?limit=0", "limit", "Số dòng từ 1 đến 50.");
        await AssertBadRequestKeyAsync(admin, "recent?limit=51", "limit", "Số dòng từ 1 đến 50.");
        (await GetRecentAsync(admin, "limit=1")).Select(r => r.Id).Should().Equal(ids.BranchBSaleId);
        (await GetRecentAsync(admin, "")).Should().HaveCountLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task Rows_follow_the_permissions()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var cashUser = await CreateClientWithPermissionsAsync("cash_only", ids.MainBranchId, Permissions.Dashboard.Executive,
            Permissions.Reports.Cash);
        var executiveUser = await CreateClientWithPermissionsAsync("executive_only", ids.MainBranchId,
            Permissions.Dashboard.Executive);
        var inventoryUser = await CreateClientWithPermissionsAsync("inventory_only", ids.MainBranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Inventory);

        var cashRows = await GetRecentAsync(cashUser, "limit=50");
        var executiveRows = await GetRecentAsync(executiveUser, "limit=50");
        var inventoryRows = await GetRecentAsync(inventoryUser, "limit=50");

        cashRows.Select(r => r.Id).Should().Equal(ids.Payment10Id, ids.Payment8Id, ids.Receipt5Id);
        executiveRows.Should().BeEmpty();
        inventoryRows.Select(r => r.Id).Should().Equal(ids.Purchase7Id, ids.Return6Id, ids.SaleId);
    }

    [Fact]
    public async Task Foreign_branch_is_refused()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var own = await CreateBranchAsync("RA" + suffix);
        var other = await CreateBranchAsync("RC" + suffix);
        var user = await CreateClientWithPermissionsAsync("executive_only", own, Permissions.Dashboard.Executive);

        (await GetExecRawAsync(user, $"recent?branchId={other}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Task<List<RecentDocumentDto>> GetRecentAsync(HttpClient client, string query) =>
        GetExecAsync<List<RecentDocumentDto>>(client, $"recent?{query}");
}
