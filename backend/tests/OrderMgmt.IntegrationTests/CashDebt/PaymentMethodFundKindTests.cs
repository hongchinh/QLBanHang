using System.Net.Http.Json;
using FluentAssertions;
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
}
