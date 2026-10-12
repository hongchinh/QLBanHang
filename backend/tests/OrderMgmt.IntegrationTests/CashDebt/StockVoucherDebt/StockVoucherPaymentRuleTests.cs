using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.StockVoucherDebt;

[Collection(nameof(PostgresCollection))]
public class StockVoucherPaymentRuleTests : CashDebtTestBase
{
    private Guid _branchId;
    private Guid _warehouseId;
    private HttpClient _branchClient = default!;

    public StockVoucherPaymentRuleTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch: its funds and codes are not shared with other tests of the class.
        var code = "SP" + Guid.NewGuid().ToString("N")[..8];
        _branchId = await CreateBranchAsync(code);
        _warehouseId = await CreateWarehouseAsync(_branchId, code);
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
    }

    [Fact]
    public async Task Due_date_and_fund_roundtrip()
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var customer = await CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);
        var tm = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "TM").Select(m => m.Id).SingleAsync());
        var qtm = await CashFundIdAsync(_branchId);
        var qtm2 = (await ReadDataAsync<FundDto>(await _branchClient.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = "QTM2", Name = "Quỹ tiền mặt 2", Kind = FundKind.Cash, IsActive = true,
        }))).Id;

        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-05 09:00",
            LineRequest(product, 2, 100_000));
        request.WarehouseId = _warehouseId;
        request.PartnerId = customer;
        request.PaymentMethodId = tm;
        request.FundId = qtm;
        request.DueDate = new DateOnly(2026, 10, 30);
        var (status, created, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        var read = await GetVoucherAsync(_branchClient, created!.Id);
        read.Should().BeEquivalentTo(new
        {
            DueDate = (DateOnly?)new DateOnly(2026, 10, 30),
            FundId = (Guid?)qtm,
            FundCode = "QTM",
            ReasonDebtEffect = DebtEffect.ReceivableIncrease,
        });

        // A voucher on the non-default cash fund QTM2 keeps it from being deleted.
        var onQtm2 = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-06 09:00",
            LineRequest(product, 1, 50_000));
        onQtm2.WarehouseId = _warehouseId;
        onQtm2.PartnerId = customer;
        onQtm2.PaymentMethodId = tm;
        onQtm2.FundId = qtm2;
        var second = await PostVoucherAsync(_branchClient, onQtm2);
        second.Status.Should().Be(HttpStatusCode.OK, second.Error?.Message);
        (await GetVoucherAsync(_branchClient, second.Voucher!.Id)).FundCode.Should().Be("QTM2");

        (await _branchClient.DeleteAsync($"/api/funds/{qtm2}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Paid_amount_defaults_by_fund_kind()
    {
        var customer = await NewCustomerAsync();
        var tm = await PaymentMethodIdAsync("TM");

        // No payment method → nothing paid, no fund.
        var noMethod = await PostOkAsync(await SaleRequestAsync(customer, 2, 100_000));
        noMethod.Should().BeEquivalentTo(new { Total = 200_000m, PaidAmount = 0m, FundId = (Guid?)null });

        // TM (FundKind = Cash) → the whole total, through the branch's default cash fund QTM.
        var cash = await SaleRequestAsync(customer, 2, 100_000);
        cash.PaymentMethodId = tm;
        (await PostOkAsync(cash)).Should().BeEquivalentTo(new
        {
            Total = 200_000m, PaidAmount = 200_000m, FundId = (Guid?)await CashFundIdAsync(_branchId), FundCode = "QTM",
        });

        // An explicit amount is kept.
        var partial = await SaleRequestAsync(customer, 2, 100_000);
        partial.PaymentMethodId = tm;
        partial.PaidAmount = 500;
        (await PostOkAsync(partial)).PaidAmount.Should().Be(500m);
    }

    [Fact]
    public async Task Paid_amount_needs_a_method_with_fund()
    {
        var customer = await NewCustomerAsync();

        var noMethod = await SaleRequestAsync(customer, 1, 100_000);
        noMethod.PaidAmount = 500;
        await ExpectInvalidAsync(noMethod, "paymentMethodId");

        var debtOnly = new PaymentMethod { Code = "PM" + Guid.NewGuid().ToString("N")[..6], Name = "Ghi nợ", FundKind = FundKind.None };
        await InDbAsync(async db => { db.PaymentMethods.Add(debtOnly); await db.SaveChangesAsync(); });
        var withoutFund = await SaleRequestAsync(customer, 1, 100_000);
        withoutFund.PaymentMethodId = debtOnly.Id;
        withoutFund.PaidAmount = 500;
        await ExpectInvalidAsync(withoutFund, "paymentMethodId");

        // The same method with the default amount pays nothing, so it is accepted without a fund.
        withoutFund.PaidAmount = null;
        (await PostOkAsync(withoutFund)).Should().BeEquivalentTo(new { PaidAmount = 0m, FundId = (Guid?)null });
    }

    [Fact]
    public async Task Bank_method_uses_default_bank_fund_or_fails()
    {
        var customer = await NewCustomerAsync();
        var ck = await PaymentMethodIdAsync("CK");

        var request = await SaleRequestAsync(customer, 1, 100_000);
        request.PaymentMethodId = ck;
        var error = await ExpectInvalidAsync(request, "fundId");
        error.Details!["fundId"].Should().Equal("Chi nhánh chưa có quỹ mặc định loại ngân hàng.");

        var nh01 = await CreateBankFundAsync("NH01", _branchId);
        (await PostOkAsync(request)).Should().BeEquivalentTo(new
        {
            PaidAmount = 100_000m, FundId = (Guid?)nh01, FundCode = "NH01",
        });

        // A cash fund does not match the bank method.
        request.FundId = await CashFundIdAsync(_branchId);
        await ExpectInvalidAsync(request, "fundId");
    }

    [Fact]
    public async Task Due_date_defaults_from_credit_days()
    {
        var customer = await NewCustomerAsync();
        await InDbAsync(db => db.Customers.Where(c => c.Id == customer)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreditDays, 30)));

        // VN date 10-05 + 30 days.
        (await PostOkAsync(await SaleRequestAsync(customer, 1, 100_000))).DueDate.Should().Be(new DateOnly(2026, 11, 4));

        var explicitDue = await SaleRequestAsync(customer, 1, 100_000);
        explicitDue.DueDate = new DateOnly(2026, 10, 20);
        (await PostOkAsync(explicitDue)).DueDate.Should().Be(new DateOnly(2026, 10, 20));

        // A due date before the document's VN date would be overdue on the day it is posted; the same day is fine.
        var early = await SaleRequestAsync(customer, 1, 100_000);
        early.DueDate = new DateOnly(2026, 10, 4);
        await ExpectInvalidAsync(early, "dueDate");
        early.DueDate = new DateOnly(2026, 10, 5);
        (await PostOkAsync(early)).DueDate.Should().Be(new DateOnly(2026, 10, 5));

        // XKH has no debt effect: no default.
        var other = await SaleRequestAsync(customer, 1, 100_000);
        other.ReasonId = await ReasonIdAsync("XKH");
        (await PostOkAsync(other)).DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Debt_reason_requires_partner_with_side_role()
    {
        var reason = new StockReason
        {
            Code = "LD" + Guid.NewGuid().ToString("N")[..6], Name = "Xuất ghi nợ khách", Direction = StockDirection.Out,
            PartnerType = PartnerType.Any, DebtEffect = DebtEffect.ReceivableIncrease,
        };
        await InDbAsync(async db => { db.StockReasons.Add(reason); await db.SaveChangesAsync(); });
        var supplier = await CreatePartnerAsync("NCC-" + Guid.NewGuid().ToString("N")[..8], isCustomer: false, isSupplier: true);

        var request = await SaleRequestAsync(supplier, 1, 100_000);
        request.ReasonId = reason.Id;
        await ExpectInvalidAsync(request, "partnerId");

        request.PartnerId = null;
        await ExpectInvalidAsync(request, "partnerId");

        request.PartnerId = await NewCustomerAsync();
        await PostOkAsync(request);
    }

    [Fact]
    public async Task Fund_deactivated_while_the_save_waits_for_its_key_is_rejected()
    {
        var customer = await NewCustomerAsync();
        var qtm2 = (await ReadDataAsync<FundDto>(await _branchClient.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = "QTM2", Name = "Quỹ tiền mặt 2", Kind = FundKind.Cash, IsActive = true,
        }))).Id;
        var request = await SaleRequestAsync(customer, 1, 100_000);
        request.PaymentMethodId = await PaymentMethodIdAsync("TM");
        request.FundId = qtm2;

        // The test holds the fund key: the fund edit waits on it first, then the sale (validated while QTM2 was still
        // active) queues behind the edit and re-checks the fund once it has the key.
        var (deactivate, sale) = await RaceAsync(
            sp => sp.GetRequiredService<ICashDebtLock>().AcquireAsync(_branchId, Array.Empty<Guid>(), new[] { qtm2 }),
            () => _branchClient.PutAsJsonAsync($"/api/funds/{qtm2}", new UpdateFundRequest
            {
                Name = "Quỹ tiền mặt 2", Kind = FundKind.Cash, IsDefault = false, IsActive = false,
            }),
            () => PostVoucherAsync(_branchClient, request));

        deactivate.StatusCode.Should().Be(HttpStatusCode.OK, await deactivate.Content.ReadAsStringAsync());
        sale.Status.Should().Be(HttpStatusCode.BadRequest, sale.Error?.Message);
        sale.Error!.Details!["fundId"].Should().Equal("Quỹ đã ngừng sử dụng.");
        (await InDbAsync(db => db.StockVouchers.AnyAsync(v => v.BranchId == _branchId))).Should().BeFalse();
    }

    private Task<Guid> NewCustomerAsync() =>
        CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);

    private Task<Guid> PaymentMethodIdAsync(string code) =>
        InDbAsync(db => db.PaymentMethods.Where(m => m.Code == code).Select(m => m.Id).SingleAsync());

    /// An XBH stock-out at 10-05 09:00 in the test branch with one untracked line.
    private async Task<UpsertStockVoucherRequest> SaleRequestAsync(Guid partnerId, decimal quantity, decimal unitPrice)
    {
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), "2026-10-05 09:00",
            LineRequest(product, quantity, unitPrice));
        request.WarehouseId = _warehouseId;
        request.PartnerId = partnerId;
        return request;
    }

    /// Creates through the API, asserts 200 and returns the voucher as GET reads it.
    private async Task<StockVoucherDto> PostOkAsync(UpsertStockVoucherRequest request)
    {
        var (status, created, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return await GetVoucherAsync(_branchClient, created!.Id);
    }

    private async Task<ApiError> ExpectInvalidAsync(UpsertStockVoucherRequest request, string key)
    {
        var (status, _, error) = await PostVoucherAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.BadRequest, error?.Message);
        error!.Details.Should().ContainKey(key);
        return error;
    }
}
