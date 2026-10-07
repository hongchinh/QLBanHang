using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class CreditLimitCrudTests : CashDebtTestBase
{
    public CreditLimitCrudTests(PostgresFixture pg) : base(pg) { }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Credit_days_roundtrip_and_search_item()
    {
        var code = NewCode("CD");
        var created = await ReadDataAsync<CustomerDto>(await _client.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest { Code = code, Name = "Khách hạn nợ", CreditDays = 30 }));
        created.CreditDays.Should().Be(30);

        var got = await ReadDataAsync<CustomerDto>(await _client.GetAsync($"/api/customers/{created.Id}"));
        got.CreditDays.Should().Be(30);

        var search = await ReadDataAsync<List<CustomerSearchItemDto>>(
            await _client.GetAsync($"/api/customers/search?keyword={code}"));
        search.Should().ContainSingle(c => c.Id == created.Id).Which.CreditDays.Should().Be(30);

        var list = await ReadDataAsync<OrderMgmt.Application.Common.Models.PagedResult<CustomerListItemDto>>(
            await _client.GetAsync($"/api/customers?search={code}"));
        list.Items.Should().ContainSingle().Which.CreditDays.Should().Be(30);

        var bad = await _client.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest { Code = NewCode("CD"), Name = "Sai hạn", CreditDays = -1 });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Update replaces the stored value; null clears it.
        var cleared = await ReadDataAsync<CustomerDto>(await _client.PutAsJsonAsync($"/api/customers/{created.Id}",
            new UpdateCustomerRequest { Name = got.Name, Group = got.Group, Status = got.Status, CreditDays = null }));
        cleared.CreditDays.Should().BeNull();
    }

    [Fact]
    public async Task Upsert_list_delete_per_branch()
    {
        var code = NewCode("CL");
        var customerId = await CreatePartnerAsync(code, isCustomer: true, isSupplier: false);
        var supplierCode = NewCode("CS");
        var supplierId = await CreatePartnerAsync(supplierCode, isCustomer: false, isSupplier: true);
        var branchB = await CreateBranchAsync("L" + Guid.NewGuid().ToString("N")[..8]);
        var clientB = CloneAdminClient();
        UseBranch(clientB, branchB);

        var put = await ReadDataAsync<CreditLimitDto>(await _client.PutAsJsonAsync(
            $"/api/credit-limits/{customerId}", new UpsertCreditLimitRequest { Amount = 50_000_000 }));
        put.CustomerId.Should().Be(customerId);
        put.CustomerCode.Should().Be(code);
        put.Amount.Should().Be(50_000_000);

        var list = await ReadDataAsync<CreditLimitListResult>(await _client.GetAsync($"/api/credit-limits?search={code}"));
        list.Items.Should().ContainSingle().Which.Amount.Should().Be(50_000_000);
        list.TotalItems.Should().Be(1);

        var listB = await ReadDataAsync<CreditLimitListResult>(await clientB.GetAsync($"/api/credit-limits?search={code}"));
        listB.Items.Should().BeEmpty();

        await ReadDataAsync<CreditLimitDto>(await _client.PutAsJsonAsync(
            $"/api/credit-limits/{customerId}", new UpsertCreditLimitRequest { Amount = 60_000_000 }));
        list = await ReadDataAsync<CreditLimitListResult>(await _client.GetAsync($"/api/credit-limits?search={code}"));
        list.Items.Should().ContainSingle().Which.Amount.Should().Be(60_000_000);

        (await clientB.DeleteAsync($"/api/credit-limits/{customerId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/credit-limits/{customerId}")).IsSuccessStatusCode.Should().BeTrue();
        list = await ReadDataAsync<CreditLimitListResult>(await _client.GetAsync($"/api/credit-limits?search={code}"));
        list.Items.Should().BeEmpty();
        (await _client.DeleteAsync($"/api/credit-limits/{customerId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A new row can be created after the soft delete.
        await ReadDataAsync<CreditLimitDto>(await _client.PutAsJsonAsync(
            $"/api/credit-limits/{customerId}", new UpsertCreditLimitRequest { Amount = 10 }));

        (await _client.PutAsJsonAsync($"/api/credit-limits/{supplierId}", new UpsertCreditLimitRequest { Amount = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.PutAsJsonAsync($"/api/credit-limits/{customerId}", new UpsertCreditLimitRequest { Amount = -1 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sales = await CreateClientForRoleAsync("cl_sales", RoleCodes.Sales);
        (await sales.GetAsync("/api/credit-limits")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sales.PutAsJsonAsync($"/api/credit-limits/{customerId}", new UpsertCreditLimitRequest { Amount = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
