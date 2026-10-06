using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class CustomerCrudTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;

    public CustomerCrudTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    [Fact]
    public async Task Create_then_list_returns_the_customer()
    {
        var create = await _client.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest
            {
                Name = "Test Company",
                TaxCode = "0123456789",
                PhoneNumber = "0901000000",
            });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(TestJson.Options);
        created!.Data!.Code.Should().StartWith("KH-");

        var list = await _client.GetFromJsonAsync<ApiResponse<PagedResult<CustomerListItemDto>>>(
            "/api/customers?page=1&pageSize=20", TestJson.Options);
        list!.Data!.Items.Should().Contain(i => i.Code == created.Data.Code);
    }

    [Fact]
    public async Task List_with_invalid_page_returns_400()
    {
        var response = await _client.GetAsync("/api/customers?page=0&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options);
        body!.Error!.Code.Should().Be("VALIDATION");
    }
}
