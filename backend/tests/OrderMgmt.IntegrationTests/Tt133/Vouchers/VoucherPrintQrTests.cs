using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Vouchers;

/// `includeQr` on the print routes of the 01-TT receipt and the 02-VT stock-out: a picture per voucher that gets a QR.
[Collection(nameof(PostgresCollection))]
public class VoucherPrintQrTests : CashDebtTestBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string TransferTitle = "THÔNG TIN CHUYỂN KHOẢN";
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public VoucherPrintQrTests(PostgresFixture pg) : base(pg) { }

    // ----- receipts -----

    [Fact]
    public async Task Receipt_without_include_qr_has_no_picture()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateReceiptAsync(ctx, bankFundId, 500_000m);

        var response = await ctx.Client.GetAsync($"/api/cash-vouchers/{v.Id}/print?format=xlsx");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Single().Pictures.Count.Should().Be(0);
    }

    [Fact]
    public async Task Receipt_with_include_qr_has_one_picture()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateReceiptAsync(ctx, bankFundId, 500_000m);

        var response = await ctx.Client.GetAsync($"/api/cash-vouchers/{v.Id}/print?format=xlsx&includeQr=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be(XlsxContentType);
        response.Content.Headers.ContentDisposition!.FileName.Should().Be($"{v.Code}.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Single().Pictures.Count.Should().Be(1);
    }

    [Fact]
    public async Task Receipt_without_a_ready_fund_still_prints_without_qr()
    {
        var ctx = await NewBranchAsync();
        var v = await CreateReceiptAsync(ctx, ctx.CashFundId, 500_000m);

        var response = await ctx.Client.GetAsync($"/api/cash-vouchers/{v.Id}/print?format=xlsx&includeQr=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Single().Pictures.Count.Should().Be(0);
    }

    [Fact]
    public async Task Batch_counts_pictures_per_voucher()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var noHolderFundId = await CreateBankFundAsync("NH02", ctx.BranchId, bankId: await FirstBankIdAsync(),
            accountNumber: "9876543210");
        var r1 = await CreateReceiptAsync(ctx, bankFundId, 500_000m);
        var r2 = await CreateReceiptAsync(ctx, ctx.CashFundId, 600_000m);
        var r3 = await CreateReceiptAsync(ctx, bankFundId, 300_000m);
        (await CancelCashAsync(ctx.Client, r3.Id, new CashDocumentActionRequest { Version = r3.Version }))
            .Status.Should().Be(HttpStatusCode.OK);
        var r4 = await CreateReceiptAsync(ctx, noHolderFundId, 400_000m);
        var stockOut = await CreateStockOutAsync(ctx, "XBH", "TM", paid: 400_000m);
        var r5 = await AutoReceiptOfAsync(stockOut.Id);
        r5.FundId.Should().Be(ctx.CashFundId);

        var ids = new List<Guid> { r1.Id, r2.Id, r3.Id, r4.Id, r5.Id };
        var withQr = await ctx.Client.PostAsJsonAsync("/api/cash-vouchers/print?format=xlsx",
            new CashVoucherPrintRequest { Ids = ids, IncludeQr = true });
        var without = await ctx.Client.PostAsJsonAsync("/api/cash-vouchers/print?format=xlsx",
            new CashVoucherPrintRequest { Ids = ids });

        withQr.StatusCode.Should().Be(HttpStatusCode.OK, await withQr.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(withQr))
        {
            wb.Worksheets.Select(w => w.Name).Should().Equal(
                new[] { r1.Code, r2.Code, r3.Code, r4.Code, r5.Code }.OrderBy(c => c, StringComparer.Ordinal));
            PicturesByName(wb).Should().BeEquivalentTo(new Dictionary<string, int>
            {
                [r1.Code] = 1, [r2.Code] = 1, [r3.Code] = 0, [r4.Code] = 0, [r5.Code] = 0,
            });
        }
        without.StatusCode.Should().Be(HttpStatusCode.OK, await without.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(without))
        {
            wb.Worksheets.Should().HaveCount(5);
            wb.Worksheets.Should().OnlyContain(w => w.Pictures.Count == 0);
        }
    }

    [Fact]
    public async Task Payment_vouchers_never_get_a_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateCashAsync(ctx.Client, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            ctx.CashFundId, "2026-10-05 09:00", null, 200_000m));

        var response = await ctx.Client.GetAsync($"/api/cash-vouchers/{v.Id}/print?format=xlsx&includeQr=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Single().Pictures.Count.Should().Be(0);
    }

    // ----- stock vouchers -----

    [Fact]
    public async Task Stock_out_voucher_mode_with_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", null, paid: null);

        var response = await ctx.Client.GetAsync(
            $"/api/stock-vouchers/{v.Id}/print?format=xlsx&priceMode=Voucher&includeQr=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentDisposition!.FileName.Should().Be($"{v.Code}.xlsx");
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        ws.Pictures.Count.Should().Be(1);
        Tt133Sheet.Contains(ws, TransferTitle).Should().BeTrue();
    }

    [Fact]
    public async Task Stock_out_without_include_qr_has_no_payment_block()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", null, paid: null);

        var response = await ctx.Client.GetAsync($"/api/stock-vouchers/{v.Id}/print?format=xlsx&priceMode=Voucher");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        ws.Pictures.Count.Should().Be(0);
        Tt133Sheet.Contains(ws, TransferTitle).Should().BeFalse();
    }

    [Theory]
    [InlineData("Cost")]
    [InlineData("None")]
    public async Task Cost_and_None_modes_ignore_qr(string mode)
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", null, paid: null, track: true);

        var response = await ctx.Client.GetAsync(
            $"/api/stock-vouchers/{v.Id}/print?format=xlsx&priceMode={mode}&includeQr=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        var ws = wb.Worksheets.Single();
        Tt133Sheet.Contains(ws, "Phiếu không có hàng theo dõi tồn kho.").Should().BeFalse("the form prints a line, so the early return is not what hides the QR");
        ws.Pictures.Count.Should().Be(0, mode);
        Tt133Sheet.Contains(ws, TransferTitle).Should().BeFalse(mode);
    }

    [Fact]
    public async Task Stock_in_ignores_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockInAsync(ctx);

        var response = await ctx.Client.GetAsync($"/api/stock-vouchers/{v.Id}/print?format=xlsx&includeQr=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var wb = await Tt133Sheet.OpenAsync(response);
        wb.Worksheets.Single().Pictures.Count.Should().Be(0);
    }

    [Fact]
    public async Task Stock_batch_body_flag()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var credit = await CreateStockOutAsync(ctx, "XBH", null, paid: null, track: true);
        var transfer = await CreateStockOutAsync(ctx, "XBH", "CK", paid: null, track: true);
        var stockIn = await CreateStockInAsync(ctx);
        var notSale = await CreateStockOutAsync(ctx, "XKH", null, paid: null, partner: false);
        var ids = new List<Guid> { credit.Id, transfer.Id, stockIn.Id, notSale.Id };

        var withQr = await ctx.Client.PostAsJsonAsync("/api/stock-vouchers/print?format=xlsx",
            new StockVoucherPrintRequest { Ids = ids, PriceMode = StockPrintPriceMode.Voucher, IncludeQr = true });
        var without = await ctx.Client.PostAsJsonAsync("/api/stock-vouchers/print?format=xlsx",
            new StockVoucherPrintRequest { Ids = ids, PriceMode = StockPrintPriceMode.Voucher });
        var atCost = await ctx.Client.PostAsJsonAsync("/api/stock-vouchers/print?format=xlsx",
            new StockVoucherPrintRequest { Ids = ids, PriceMode = StockPrintPriceMode.Cost, IncludeQr = true });

        withQr.StatusCode.Should().Be(HttpStatusCode.OK, await withQr.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(withQr))
        {
            wb.Worksheets.Should().HaveCount(4);
            PicturesByName(wb).Should().BeEquivalentTo(new Dictionary<string, int>
            {
                [credit.Code] = 1, [transfer.Code] = 1, [stockIn.Code] = 0, [notSale.Code] = 0,
            });
        }
        without.StatusCode.Should().Be(HttpStatusCode.OK, await without.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(without))
            wb.Worksheets.Should().OnlyContain(w => w.Pictures.Count == 0);
        atCost.StatusCode.Should().Be(HttpStatusCode.OK, await atCost.Content.ReadAsStringAsync());
        using (var wb = await Tt133Sheet.OpenAsync(atCost))
        {
            wb.Worksheets.Should().OnlyContain(w => w.Pictures.Count == 0);
            Tt133Sheet.Contains(wb.Worksheets.Single(w => w.Name == credit.Code), "Phiếu không có hàng theo dõi tồn kho.")
                .Should().BeFalse("the tracked stock-out prints a line at cost");
        }
    }

    // ----- access still wins -----

    // The user works in the voucher's own branch, so only the permission check can refuse them.
    [Fact]
    public async Task Cash_print_with_qr_still_needs_the_view_permission()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateReceiptAsync(ctx, bankFundId, 500_000m);
        var denied = await UserInBranchAsync(ctx.BranchId, Permissions.Payments.View);

        var single = await denied.GetAsync($"/api/cash-vouchers/{v.Id}/print?format=xlsx&includeQr=true");
        var batch = await denied.PostAsJsonAsync("/api/cash-vouchers/print?format=xlsx",
            new CashVoucherPrintRequest { Ids = [v.Id], IncludeQr = true });

        await AssertPermissionDeniedAsync(single);
        await AssertPermissionDeniedAsync(batch);
    }

    [Fact]
    public async Task Stock_print_with_qr_still_needs_the_view_permission()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", null, paid: null);
        var denied = await UserInBranchAsync(ctx.BranchId, Permissions.StockIn.View);

        var single = await denied.GetAsync($"/api/stock-vouchers/{v.Id}/print?format=xlsx&includeQr=true");
        var batch = await denied.PostAsJsonAsync("/api/stock-vouchers/print?format=xlsx",
            new StockVoucherPrintRequest { Ids = [v.Id], IncludeQr = true });

        await AssertPermissionDeniedAsync(single);
        await AssertPermissionDeniedAsync(batch);
    }

    [Fact]
    public async Task Print_with_qr_still_refuses_a_voucher_of_another_branch()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var cash = await CreateReceiptAsync(ctx, ctx.CashFundId, 500_000m);
        var stock = await CreateStockOutAsync(ctx, "XBH", null, paid: null);
        var otherBranchId = await CreateBranchAsync("PQ" + Guid.NewGuid().ToString("N")[..8]);
        var client = CloneAdminClient();
        UseBranch(client, otherBranchId);

        var cashResponse = await client.GetAsync($"/api/cash-vouchers/{cash.Id}/print?format=xlsx&includeQr=true");
        var stockResponse = await client.PostAsJsonAsync("/api/stock-vouchers/print?format=xlsx",
            new StockVoucherPrintRequest { Ids = [stock.Id], IncludeQr = true });

        foreach (var response in new[] { cashResponse, stockResponse })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Message
                .Should().Be("Phiếu thuộc chi nhánh khác.");
        }
    }

    // ----- helpers -----

    private sealed record BranchCtx(Guid BranchId, HttpClient Client, Guid CashFundId, Guid WarehouseId);

    /// A fresh branch opened on 10-01 with an admin client working in it, its default cash fund (10,000,000) and a
    /// warehouse, so the default-fund state of one test never leaks into another.
    private async Task<BranchCtx> NewBranchAsync()
    {
        await SetCompanyAsync();
        var code = "PQ" + Guid.NewGuid().ToString("N")[..8];
        var branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var warehouseId = await CreateWarehouseAsync(branchId, code);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var cashFundId = await FundIdAsync("QTM", branchId);
        await SetFundOpeningAmountAsync(cashFundId, 10_000_000m);
        return new BranchCtx(branchId, client, cashFundId, warehouseId);
    }

    private async Task<CashVoucherDto> CreateReceiptAsync(BranchCtx ctx, Guid fundId, decimal amount) =>
        await CreateCashAsync(ctx.Client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId,
            "2026-10-05 09:00", null, amount));

    /// One line of 1 × 1,000,000, untracked unless `track`: then the product is stocked first (10 on 10-04), so the cost
    /// and no-money forms print a line. `method` is the payment method code (TM, CK); `paid` null = the default.
    private async Task<StockVoucherDto> CreateStockOutAsync(BranchCtx ctx, string reasonCode, string? method, decimal? paid,
        bool partner = true, bool track = false)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: track);
        if (track)
        {
            var stockIn = VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-04 09:00",
                LineRequest(product, 10, 500_000m));
            stockIn.WarehouseId = ctx.WarehouseId;
            var (inStatus, _, inError) = await PostVoucherAsync(ctx.Client, stockIn);
            inStatus.Should().Be(HttpStatusCode.OK, inError?.Message);
        }
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync(reasonCode), "2026-10-05 09:00",
            LineRequest(product, 1, 1_000_000m));
        request.WarehouseId = ctx.WarehouseId;
        if (partner)
            request.PartnerId = await CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], true, false);
        if (method is not null)
            request.PaymentMethodId = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == method).Select(m => m.Id).SingleAsync());
        request.PaidAmount = paid;
        var (status, voucher, error) = await PostVoucherAsync(ctx.Client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    private async Task<StockVoucherDto> CreateStockInAsync(BranchCtx ctx)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-05 09:00",
            LineRequest(product, 1, 1_000_000m));
        request.WarehouseId = ctx.WarehouseId;
        var (status, voucher, error) = await PostVoucherAsync(ctx.Client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    private Task<OrderMgmt.Domain.Entities.CashDebt.CashVoucher> AutoReceiptOfAsync(Guid stockVoucherId) =>
        InDbAsync(db => db.CashVouchers.AsNoTracking().SingleAsync(c => c.SourceStockVoucherId == stockVoucherId));

    private Task<Guid> FirstBankIdAsync() =>
        InDbAsync(db => db.Banks.Where(b => b.IsActive).OrderBy(b => b.Code).Select(b => b.Id).FirstAsync());

    /// A user of the voucher's own branch with exactly these permissions.
    private Task<HttpClient> UserInBranchAsync(Guid branchId, params string[] permissions) =>
        CreateClientWithPermissionsAsync("vpq_" + Guid.NewGuid().ToString("N")[..8], branchId, permissions);

    private static async Task AssertPermissionDeniedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!;
        error.Code.Should().Be("FORBIDDEN");
        error.Message.Should().NotBe("Phiếu thuộc chi nhánh khác.", "the permission is checked before the branch");
    }

    private static Dictionary<string, int> PicturesByName(XLWorkbook wb) =>
        wb.Worksheets.ToDictionary(w => w.Name, w => w.Pictures.Count);

    private Task SetCompanyAsync() =>
        InDbAsync(db => db.CompanyProfiles.Where(p => p.Id == 1).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Name, "Công ty ABC")
            .SetProperty(p => p.Address, "1 Láng Hạ")
            .SetProperty(p => p.ChiefAccountantName, "Trần B")
            .SetProperty(p => p.LegalRepresentativeName, "Nguyễn A")));
}
