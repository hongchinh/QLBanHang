using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Inventory.PaymentMethods.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class PaymentMethodCrudTests : InventoryTestBase
{
    public PaymentMethodCrudTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Crud_happy_path_duplicate_code_and_guard()
    {
        var created = await ReadDataAsync<PaymentMethodDto>(await _client.PostAsJsonAsync("/api/payment-methods",
            new CreatePaymentMethodRequest { Code = "VDT", Name = "Ví điện tử", IsCash = false }));
        created.IsCash.Should().BeFalse();

        (await _client.PostAsJsonAsync("/api/payment-methods",
                new CreatePaymentMethodRequest { Code = "VDT", Name = "Trùng", IsCash = false }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updated = await ReadDataAsync<PaymentMethodDto>(await _client.PutAsJsonAsync($"/api/payment-methods/{created.Id}",
            new UpdatePaymentMethodRequest { Name = "Tiền mặt tại quầy", IsCash = true }));
        updated.IsCash.Should().BeTrue();

        var list = await ReadDataAsync<List<PaymentMethodDto>>(await _client.GetAsync("/api/payment-methods"));
        list.Select(m => m.Code).Should().Contain("VDT");

        (await _client.DeleteAsync($"/api/payment-methods/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/payment-methods/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var sales = await CreateClientForRoleAsync("pm_sales", RoleCodes.Sales);
        (await sales.PostAsJsonAsync("/api/payment-methods",
                new CreatePaymentMethodRequest { Code = "X1", Name = "X1", IsCash = false }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
