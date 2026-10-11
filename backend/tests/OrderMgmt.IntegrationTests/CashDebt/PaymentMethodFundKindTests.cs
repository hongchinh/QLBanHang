using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.PaymentMethods.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class PaymentMethodFundKindTests : CashDebtTestBase
{
    public PaymentMethodFundKindTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Seeded_methods_have_fund_kind_and_crud_roundtrips()
    {
        var list = await ReadDataAsync<List<PaymentMethodDto>>(await _client.GetAsync("/api/payment-methods"));
        list.Single(m => m.Code == "TM").FundKind.Should().Be(FundKind.Cash);
        list.Single(m => m.Code == "CK").FundKind.Should().Be(FundKind.Bank);

        var code = "QR" + Guid.NewGuid().ToString("N")[..6];
        var response = await _client.PostAsJsonAsync("/api/payment-methods",
            new { code, name = "QR", fundKind = "Bank" });
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"fundKind\":\"Bank\"");
        var created = await ReadDataAsync<PaymentMethodDto>(response);

        var fetched = await ReadDataAsync<PaymentMethodDto>(await _client.GetAsync($"/api/payment-methods/{created.Id}"));
        fetched.FundKind.Should().Be(FundKind.Bank);

        var updated = await ReadDataAsync<PaymentMethodDto>(await _client.PutAsJsonAsync(
            $"/api/payment-methods/{created.Id}", new { name = "QR", fundKind = "None" }));
        updated.FundKind.Should().Be(FundKind.None);
    }

    [Fact]
    public async Task Fund_kind_is_locked_while_a_paid_stock_voucher_uses_the_method()
    {
        var code = "FK" + Guid.NewGuid().ToString("N")[..8];
        var branchId = await CreateBranchAsync(code);
        await SetBranchOpeningDateAsync(branchId, new DateOnly(2026, 9, 1));
        var warehouseId = await CreateWarehouseAsync(branchId, code);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var customer = await CreatePartnerAsync("KH-" + code, isCustomer: true, isSupplier: false);

        async Task<PaymentMethodDto> MethodAsync(string suffix) => await ReadDataAsync<PaymentMethodDto>(
            await _client.PostAsJsonAsync("/api/payment-methods", new { code = code + suffix, name = "Ví", fundKind = "Cash" }));
        async Task SaleAsync(Guid methodId, decimal paid)
        {
            var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
            await CreateVoucherAsync(client, StockDirection.Out, "XBH", "2026-10-05 09:00", r =>
            {
                r.WarehouseId = warehouseId;
                r.PartnerId = customer;
                r.PaymentMethodId = methodId;
                r.PaidAmount = paid;
            }, LineRequest(product, 1, 1_000));
        }

        var paidWith = await MethodAsync("P");
        await SaleAsync(paidWith.Id, 500m);
        var change = await _client.PutAsJsonAsync($"/api/payment-methods/{paidWith.Id}", new { name = "Ví", fundKind = "Bank" });
        change.StatusCode.Should().Be(HttpStatusCode.BadRequest, await change.Content.ReadAsStringAsync());
        (await change.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("fundKind");
        (await InDbAsync(db => db.PaymentMethods.Where(m => m.Id == paidWith.Id).Select(m => m.FundKind).SingleAsync()))
            .Should().Be(FundKind.Cash);

        // The name still changes, and a method used only on unpaid vouchers may change its kind.
        var rename = await ReadDataAsync<PaymentMethodDto>(await _client.PutAsJsonAsync(
            $"/api/payment-methods/{paidWith.Id}", new { name = "Ví điện tử", fundKind = "Cash" }));
        rename.Name.Should().Be("Ví điện tử");
        var unpaidWith = await MethodAsync("U");
        await SaleAsync(unpaidWith.Id, 0m);
        var unpaidChange = await ReadDataAsync<PaymentMethodDto>(await _client.PutAsJsonAsync(
            $"/api/payment-methods/{unpaidWith.Id}", new { name = "Ví", fundKind = "None" }));
        unpaidChange.FundKind.Should().Be(FundKind.None);
        await AssertCashDebtInvariantsAsync();
    }
}
