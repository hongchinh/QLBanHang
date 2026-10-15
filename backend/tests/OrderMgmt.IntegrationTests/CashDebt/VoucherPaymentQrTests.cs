using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.PaymentQr;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Payments.Services;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Payments;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// `GET api/cash-vouchers/{id}/payment-qr`, `GET api/stock-vouchers/{id}/payment-qr` and the print-side resolver.
[Collection(nameof(PostgresCollection))]
public class VoucherPaymentQrTests : CashDebtTestBase
{
    private const string Code = "PAYMENT_QR_UNAVAILABLE";
    private const string NotApplicable = "Phiếu này không có QR chuyển khoản.";
    private const string Cancelled = "Phiếu đã hủy.";
    private const string NoAmount = "Phiếu không còn số cần chuyển khoản.";
    private const string NotSale = "Lý do xuất không phải bán hàng.";
    private const string AutoInCash = "Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản.";
    private const string NoDefault = "Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR.";
    private static string Incomplete(string fundCode) => $"Quỹ {fundCode} chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản.";

    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public VoucherPaymentQrTests(PostgresFixture pg) : base(pg) { }

    // ----- cash vouchers: who gets a QR -----

    [Fact]
    public async Task Receipt_on_its_bank_fund()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var bank = await BankOfFundAsync(bankFundId);
        var v = await CreateReceiptAsync(ctx, bankFundId, 1_500_000m);

        var (status, dto, error) = await GetCashQrAsync(ctx.Client, v.Id);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        dto!.Payload.Should().Be(Expected(bank, 1_500_000m, v.Code));
        dto.Amount.Should().Be(1_500_000m);
        dto.Total.Should().Be(1_500_000m);
        dto.CashPaid.Should().Be(0m);
        dto.Content.Should().Be(v.Code);
        dto.FundCode.Should().Be("NH01");
        dto.BankName.Should().Be(bank.Name);
        dto.BankShortName.Should().Be(bank.ShortName ?? bank.Code);
        dto.AccountNumber.Should().Be("0123456789");
        dto.AccountHolderName.Should().Be("CONG TY ABC");
        dto.SourceStockVoucherCode.Should().BeNull();
    }

    [Fact]
    public async Task Receipt_on_a_cash_fund_uses_the_default_bank_fund()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var bank = await BankOfFundAsync(bankFundId);
        var v = await CreateReceiptAsync(ctx, ctx.CashFundId, 700_000m);

        var (status, dto, error) = await GetCashQrAsync(ctx.Client, v.Id);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        dto!.FundCode.Should().Be("NH01");
        dto.Payload.Should().Be(Expected(bank, 700_000m, v.Code));
    }

    [Fact]
    public async Task Receipt_on_an_incomplete_bank_fund_does_not_fall_back_to_the_default()
    {
        var ctx = await NewBranchAsync();
        var bankId = await FirstBankIdAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var noHolder = await CreateBankFundAsync("NH02", ctx.BranchId, bankId: bankId, accountNumber: "9876543210");
        var v = await CreateReceiptAsync(ctx, noHolder, 500_000m);

        var (status, _, error) = await GetCashQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, Incomplete("NH02"));
    }

    [Fact]
    public async Task Receipt_on_a_bank_fund_with_an_inactive_bank_is_incomplete()
    {
        var ctx = await NewBranchAsync();
        var bankId = await InDbAsync(async db =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var bank = new Bank { Code = "OFF" + suffix, Name = "Ngân hàng ngừng " + suffix, Bin = "9" + suffix[..5], IsActive = false };
            db.Banks.Add(bank);
            await db.SaveChangesAsync();
            return bank.Id;
        });
        var fundId = await CreateBankFundAsync("NH01", ctx.BranchId, bankId: bankId, accountNumber: "0123456789",
            accountHolderName: "CONG TY ABC");
        var v = await CreateReceiptAsync(ctx, fundId, 500_000m);

        var (status, _, error) = await GetCashQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, Incomplete("NH01"));
    }

    [Fact]
    public async Task Cash_fund_receipt_in_a_branch_without_a_bank_fund()
    {
        var ctx = await NewBranchAsync();
        var v = await CreateReceiptAsync(ctx, ctx.CashFundId, 500_000m);

        var (status, _, error) = await GetCashQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, NoDefault);
    }

    [Fact]
    public async Task Cancelled_receipt_has_no_qr()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateReceiptAsync(ctx, bankFundId, 500_000m);
        var (cancelStatus, _, cancelError) = await CancelCashAsync(ctx.Client, v.Id, new CashDocumentActionRequest { Version = v.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

        var (status, _, error) = await GetCashQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, Cancelled);
    }

    [Fact]
    public async Task Payment_voucher_has_no_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateCashAsync(ctx.Client, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            ctx.CashFundId, "2026-10-05 09:00", null, 200_000m));

        var (status, _, error) = await GetCashQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, NotApplicable);
    }

    // ----- automatic receipts -----

    [Fact]
    public async Task Automatic_receipt_in_a_cash_fund_has_no_qr_while_the_stock_out_keeps_its_own()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", "TM", paid: 400_000m);
        var auto = await AutoReceiptOfAsync(v.Id);
        auto.FundId.Should().Be(ctx.CashFundId);

        var (status, _, error) = await GetCashQrAsync(ctx.Client, auto.Id);

        AssertUnavailable(status, error, AutoInCash);
        var (stockStatus, stockDto, stockError) = await GetStockQrAsync(ctx.Client, v.Id);
        stockStatus.Should().Be(HttpStatusCode.OK, stockError?.Message);
        stockDto!.Amount.Should().Be(600_000m);
    }

    [Fact]
    public async Task Automatic_receipt_in_a_bank_fund_uses_the_stock_out_code()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var bank = await BankOfFundAsync(bankFundId);
        var v = await CreateStockOutAsync(ctx, "XBH", "CK", paid: null);
        var auto = await AutoReceiptOfAsync(v.Id);

        var (status, dto, error) = await GetCashQrAsync(ctx.Client, auto.Id);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        dto!.Content.Should().Be(v.Code);
        dto.Content.Should().StartWith("PX");
        dto.SourceStockVoucherCode.Should().Be(v.Code);
        dto.Amount.Should().Be(auto.Total);
        dto.Payload.Should().Be(Expected(bank, auto.Total, v.Code));
    }

    // ----- stock vouchers: who gets a QR -----

    [Fact]
    public async Task Transfer_stock_out_asks_for_the_total()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var bank = await BankOfFundAsync(bankFundId);
        var v = await CreateStockOutAsync(ctx, "XBH", "CK", paid: 1_000_000m);

        var (status, dto, error) = await GetStockQrAsync(ctx.Client, v.Id);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        dto!.Amount.Should().Be(dto.Total);
        dto.Total.Should().Be(1_000_000m);
        dto.CashPaid.Should().Be(0m);
        dto.FundCode.Should().Be("NH01");
        dto.Content.Should().Be(v.Code);
        dto.SourceStockVoucherCode.Should().BeNull();
        dto.Payload.Should().Be(Expected(bank, 1_000_000m, v.Code));
    }

    [Fact]
    public async Task Partly_paid_in_cash()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var bank = await BankOfFundAsync(bankFundId);
        var v = await CreateStockOutAsync(ctx, "XBH", "TM", paid: 400_000m);

        var (status, dto, error) = await GetStockQrAsync(ctx.Client, v.Id);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        dto!.Amount.Should().Be(dto.Total - 400_000m);
        dto.CashPaid.Should().Be(400_000m);
        dto.FundCode.Should().Be("NH01");
        dto.Payload.Should().Be(Expected(bank, 600_000m, v.Code));
    }

    [Fact]
    public async Task Credit_sale()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var bank = await BankOfFundAsync(bankFundId);
        var v = await CreateStockOutAsync(ctx, "XBH", null, paid: null);

        var (status, dto, error) = await GetStockQrAsync(ctx.Client, v.Id);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        dto!.Amount.Should().Be(dto.Total);
        dto.CashPaid.Should().Be(0m);
        dto.FundCode.Should().Be("NH01");
        dto.Payload.Should().Be(Expected(bank, 1_000_000m, v.Code));
    }

    [Fact]
    public async Task Stock_out_fully_paid_in_cash_has_nothing_left_to_transfer()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", "TM", paid: 1_000_000m);

        var (status, _, error) = await GetStockQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, NoAmount);
    }

    [Fact]
    public async Task Stock_out_that_is_not_a_sale_has_no_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XKH", null, paid: null, partner: false);

        var (status, _, error) = await GetStockQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, NotSale);
    }

    [Fact]
    public async Task Stock_in_has_no_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockInAsync(ctx);

        var (status, _, error) = await GetStockQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, NotApplicable);
    }

    [Fact]
    public async Task Cancelled_stock_out_has_no_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", null, paid: null);
        await CancelStockAsync(ctx.Client, v);

        var (status, _, error) = await GetStockQrAsync(ctx.Client, v.Id);

        AssertUnavailable(status, error, Cancelled);
    }

    // ----- access -----

    [Fact]
    public async Task Cash_route_needs_the_view_permission_of_the_type()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateReceiptAsync(ctx, ctx.CashFundId, 500_000m);
        var client = await CreateClientWithPermissionsAsync("pqr_" + Guid.NewGuid().ToString("N")[..8], null,
            Permissions.Payments.View);

        var response = await client.GetAsync($"/api/cash-vouchers/{v.Id}/payment-qr");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Stock_route_needs_the_view_permission_of_the_type()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var v = await CreateStockOutAsync(ctx, "XBH", null, paid: null);
        var client = await CreateClientWithPermissionsAsync("pqr_" + Guid.NewGuid().ToString("N")[..8], null,
            Permissions.StockIn.View);

        var response = await client.GetAsync($"/api/stock-vouchers/{v.Id}/payment-qr");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Both_routes_refuse_a_voucher_of_another_branch()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var cash = await CreateReceiptAsync(ctx, ctx.CashFundId, 500_000m);
        var stock = await CreateStockOutAsync(ctx, "XBH", null, paid: null);
        var otherBranchId = await CreateBranchAsync("PQ" + Guid.NewGuid().ToString("N")[..8]);
        var client = CloneAdminClient();
        UseBranch(client, otherBranchId);

        var (cashStatus, _, cashError) = await GetCashQrAsync(client, cash.Id);
        var (stockStatus, _, stockError) = await GetStockQrAsync(client, stock.Id);

        cashStatus.Should().Be(HttpStatusCode.Forbidden);
        cashError!.Message.Should().Be("Phiếu thuộc chi nhánh khác.");
        stockStatus.Should().Be(HttpStatusCode.Forbidden);
        stockError!.Message.Should().Be("Phiếu thuộc chi nhánh khác.");
    }

    [Fact]
    public async Task Both_routes_answer_404_for_an_unknown_voucher()
    {
        var ctx = await NewBranchAsync();

        (await ctx.Client.GetAsync($"/api/cash-vouchers/{Guid.NewGuid()}/payment-qr")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await ctx.Client.GetAsync($"/api/stock-vouchers/{Guid.NewGuid()}/payment-qr")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Both_routes_answer_404_for_a_deleted_voucher()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var cash = await CreateReceiptAsync(ctx, ctx.CashFundId, 500_000m);
        var stock = await CreateStockOutAsync(ctx, "XBH", null, paid: null);
        var (deleteStatus, _, deleteError) = await DeleteCashAsync(ctx.Client, cash.Id, new CashDocumentActionRequest { Version = cash.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        (await ctx.Client.DeleteAsync($"/api/stock-vouchers/{stock.Id}?version={stock.Version}")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await ctx.Client.GetAsync($"/api/cash-vouchers/{cash.Id}/payment-qr")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await ctx.Client.GetAsync($"/api/stock-vouchers/{stock.Id}/payment-qr")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    // ----- ResolveMany -----

    [Fact]
    public async Task ResolveMany_keeps_only_the_cash_vouchers_that_get_a_qr()
    {
        var ctx = await NewBranchAsync();
        var bankFundId = await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var ready = await CreateReceiptAsync(ctx, bankFundId, 500_000m);
        var cancelled = await CreateReceiptAsync(ctx, bankFundId, 300_000m);
        (await CancelCashAsync(ctx.Client, cancelled.Id, new CashDocumentActionRequest { Version = cancelled.Version }))
            .Status.Should().Be(HttpStatusCode.OK);
        var payment = await CreateCashAsync(ctx.Client, CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"),
            ctx.CashFundId, "2026-10-05 09:00", null, 200_000m));

        var result = await ResolveManyAsync(PaymentQrKind.Cash, ready.Id, cancelled.Id, payment.Id, Guid.NewGuid());

        result.Keys.Should().BeEquivalentTo(new[] { ready.Id });
        result[ready.Id].Amount.Should().Be(500_000m);
    }

    [Fact]
    public async Task ResolveMany_keeps_only_the_stock_vouchers_that_get_a_qr()
    {
        var ctx = await NewBranchAsync();
        await CreateQrBankFundAsync("NH01", ctx.BranchId);
        var ready = await CreateStockOutAsync(ctx, "XBH", null, paid: null);
        var stockIn = await CreateStockInAsync(ctx);
        var notSale = await CreateStockOutAsync(ctx, "XKH", null, paid: null, partner: false);

        var result = await ResolveManyAsync(PaymentQrKind.Stock, ready.Id, stockIn.Id, notSale.Id, Guid.NewGuid());

        result.Keys.Should().BeEquivalentTo(new[] { ready.Id });
    }

    [Fact]
    public async Task ResolveMany_of_nothing_is_empty()
    {
        (await ResolveManyAsync(PaymentQrKind.Cash)).Should().BeEmpty();
        (await ResolveManyAsync(PaymentQrKind.Stock)).Should().BeEmpty();
    }

    // ----- helpers -----

    private sealed record BranchCtx(Guid BranchId, HttpClient Client, Guid CashFundId, Guid WarehouseId);

    /// A fresh branch opened on 10-01 with an admin client working in it, its default cash fund (10,000,000) and a
    /// warehouse, so the default-fund state of one test never leaks into another.
    private async Task<BranchCtx> NewBranchAsync()
    {
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

    /// One untracked line of 1 × 1,000,000. `method` is the payment method code (TM, CK); `paid` null = the default.
    private async Task<StockVoucherDto> CreateStockOutAsync(BranchCtx ctx, string reasonCode, string? method, decimal? paid,
        bool partner = true)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
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

    private static async Task CancelStockAsync(HttpClient client, StockVoucherDto v)
    {
        var response = await client.PostAsJsonAsync($"/api/stock-vouchers/{v.Id}/cancel",
            new StockVoucherActionRequest { Version = v.Version });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private Task<OrderMgmt.Domain.Entities.CashDebt.CashVoucher> AutoReceiptOfAsync(Guid stockVoucherId) =>
        InDbAsync(db => db.CashVouchers.AsNoTracking().SingleAsync(c => c.SourceStockVoucherId == stockVoucherId));

    private Task<Guid> FirstBankIdAsync() =>
        InDbAsync(db => db.Banks.Where(b => b.IsActive).OrderBy(b => b.Code).Select(b => b.Id).FirstAsync());

    private Task<Bank> BankOfFundAsync(Guid fundId) =>
        InDbAsync(db => db.Funds.Where(f => f.Id == fundId).Select(f => f.Bank!).SingleAsync());

    private static string Expected(Bank bank, decimal amount, string content) =>
        VietQrPayloadBuilder.Build(bank.Bin, "0123456789", "CONG TY ABC", amount, content);

    private static Task<(HttpStatusCode Status, VoucherPaymentQrDto? Data, ApiError? Error)> GetCashQrAsync(HttpClient c, Guid id) =>
        GetQrAsync(c, $"/api/cash-vouchers/{id}/payment-qr");

    private static Task<(HttpStatusCode Status, VoucherPaymentQrDto? Data, ApiError? Error)> GetStockQrAsync(HttpClient c, Guid id) =>
        GetQrAsync(c, $"/api/stock-vouchers/{id}/payment-qr");

    private static async Task<(HttpStatusCode Status, VoucherPaymentQrDto? Data, ApiError? Error)> GetQrAsync(HttpClient c, string url) =>
        await ReadDocumentResponseAsync<VoucherPaymentQrDto>(await c.GetAsync(url));

    private static void AssertUnavailable(HttpStatusCode status, ApiError? error, string message)
    {
        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error.Should().NotBeNull();
        error!.Code.Should().Be(Code);
        error.Message.Should().Be(message);
    }

    private async Task<IReadOnlyDictionary<Guid, VoucherPaymentQrDto>> ResolveManyAsync(PaymentQrKind kind, params Guid[] ids)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IVoucherPaymentQrService>();
        return await service.ResolveManyAsync(kind, ids);
    }
}
