using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Funds.Models;
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
}
