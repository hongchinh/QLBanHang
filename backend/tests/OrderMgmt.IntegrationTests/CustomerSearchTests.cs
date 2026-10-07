using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class CustomerSearchTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;

    public CustomerSearchTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();
        await SeedAsync();
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

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Customers.AddRange(
            new Customer
            {
                Code = "KH-S-001",
                Name = "Công ty TNHH Sản xuất ABC",
                TaxCode = "0100000001",
                CompanyAddress = "12 Đường Số 1, Quận 1, TPHCM",
                PhoneNumber = "0901111111",
                Status = CustomerStatus.Active,
            },
            new Customer
            {
                Code = "KH-S-002",
                Name = "Cong Ty XYZ",
                TaxCode = "0100000002",
                CompanyAddress = "34 Le Duan, Ha Noi",
                PhoneNumber = "0902222222",
                Status = CustomerStatus.Active,
            },
            new Customer
            {
                Code = "KH-S-003",
                Name = "Công ty Đã Ngừng",
                TaxCode = "0100000003",
                CompanyAddress = "5 Đại lộ Hồ Chí Minh",
                PhoneNumber = "0903333333",
                Status = CustomerStatus.Inactive,
            });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Empty_keyword_returns_empty_list()
    {
        var resp = await _client.GetFromJsonAsync<ApiResponse<List<CustomerSearchItemDto>>>(
            "/api/customers/search?keyword=&limit=10", TestJson.Options);
        resp!.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_by_exact_code_returns_customer()
    {
        var resp = await _client.GetFromJsonAsync<ApiResponse<List<CustomerSearchItemDto>>>(
            "/api/customers/search?keyword=KH-S-001&limit=10", TestJson.Options);
        resp!.Data!.Should().ContainSingle(c => c.Code == "KH-S-001");
    }

    [Fact]
    public async Task Search_with_diacritics_returns_both_diacritic_and_plain_variants()
    {
        var resp = await _client.GetFromJsonAsync<ApiResponse<List<CustomerSearchItemDto>>>(
            "/api/customers/search?keyword=C%C3%B4ng&limit=10", TestJson.Options);
        // "Công" should match both "Công ty TNHH Sản xuất ABC" and "Cong Ty XYZ" (active ones).
        resp!.Data!.Select(c => c.Code).Should().Contain(new[] { "KH-S-001", "KH-S-002" });
    }

    [Fact]
    public async Task Search_without_diacritics_matches_diacritic_names()
    {
        var resp = await _client.GetFromJsonAsync<ApiResponse<List<CustomerSearchItemDto>>>(
            "/api/customers/search?keyword=cong&limit=10", TestJson.Options);
        resp!.Data!.Select(c => c.Code).Should().Contain(new[] { "KH-S-001", "KH-S-002" });
    }

    [Fact]
    public async Task ActiveOnly_default_hides_inactive_customers()
    {
        var resp = await _client.GetFromJsonAsync<ApiResponse<List<CustomerSearchItemDto>>>(
            "/api/customers/search?keyword=KH-S&limit=10", TestJson.Options);
        resp!.Data!.Should().NotContain(c => c.Code == "KH-S-003");
        resp.Data.Should().Contain(c => c.Code == "KH-S-001");
    }

    [Fact]
    public async Task ActiveOnly_false_includes_inactive_customers()
    {
        var resp = await _client.GetFromJsonAsync<ApiResponse<List<CustomerSearchItemDto>>>(
            "/api/customers/search?keyword=KH-S&activeOnly=false&limit=10", TestJson.Options);
        resp!.Data!.Should().Contain(c => c.Code == "KH-S-003");
    }

    [Fact]
    public async Task Limit_clamps_result_count()
    {
        var resp = await _client.GetFromJsonAsync<ApiResponse<List<CustomerSearchItemDto>>>(
            "/api/customers/search?keyword=KH-S&limit=2", TestJson.Options);
        resp!.Data!.Count.Should().BeLessThanOrEqualTo(2);
    }
}
