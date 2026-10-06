using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Catalog.Products.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class ProductCrudTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;
    private Guid _epsGroupId;
    private Guid _tamUnitId;

    public ProductCrudTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();
        await ResolveReferenceIdsAsync();
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

    private async Task ResolveReferenceIdsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _epsGroupId = (await db.ProductGroups.FirstAsync(g => g.Code == "EPS")).Id;
        _tamUnitId = (await db.Units.FirstAsync(u => u.Code == "TAM")).Id;
    }

    [Fact]
    public async Task Create_then_get_returns_product_with_group_and_unit_names()
    {
        var create = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "EPS 1000x2000x50",
            ProductGroupId = _epsGroupId,
            UnitId = _tamUnitId,
            Length = 1000,
            Width = 2000,
            Thickness = 50,
            Density = 15,
            Specification = "1000x2000x50mm",
            DefaultPrice = 150_000,
            CostPrice = 100_000,
            DefaultTaxRate = 8,
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(TestJson.Options);
        created!.Data!.Code.Should().StartWith("HH-");
        created.Data.ProductGroupName.Should().Be("Tấm xốp EPS");
        created.Data.UnitName.Should().Be("Tấm");

        var get = await _client.GetFromJsonAsync<ApiResponse<ProductDto>>(
            $"/api/products/{created.Data.Id}", TestJson.Options);
        get!.Data!.Name.Should().Be("EPS 1000x2000x50");
        get.Data.DefaultPrice.Should().Be(150_000);
    }

    [Fact]
    public async Task Create_with_missing_group_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Missing group",
            ProductGroupId = Guid.Empty,
            UnitId = _tamUnitId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options);
        body!.Error!.Code.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Create_with_unknown_group_returns_404()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Unknown group",
            ProductGroupId = Guid.NewGuid(),
            UnitId = _tamUnitId,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_filters_by_group_and_search()
    {
        await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "EPS Pattern A", ProductGroupId = _epsGroupId, UnitId = _tamUnitId,
        });
        await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "EPS Pattern B", ProductGroupId = _epsGroupId, UnitId = _tamUnitId,
        });

        var byGroup = await _client.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>(
            $"/api/products?page=1&pageSize=20&productGroupId={_epsGroupId}", TestJson.Options);
        byGroup!.Data!.Items.Should().HaveCount(c => c >= 2);
        byGroup.Data.Items.Should().OnlyContain(i => i.ProductGroupName == "Tấm xốp EPS");

        var bySearch = await _client.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>(
            "/api/products?page=1&pageSize=20&search=Pattern%20A", TestJson.Options);
        bySearch!.Data!.Items.Should().Contain(i => i.Name == "EPS Pattern A");
        bySearch.Data.Items.Should().NotContain(i => i.Name == "EPS Pattern B");
    }

    [Fact]
    public async Task Search_matches_product_name_without_accents()
    {
        const string productName = "T\u1EA5m nhua op tuong";

        await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = productName,
            ProductGroupId = _epsGroupId,
            UnitId = _tamUnitId,
        });

        var search = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<ProductSuggestionDto>>>(
            "/api/products/search?q=tam&take=20", TestJson.Options);

        search!.Data.Should().Contain(i => i.Name == productName);
    }

    [Fact]
    public async Task Delete_then_list_excludes_product()
    {
        var create = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Will be deleted",
            ProductGroupId = _epsGroupId,
            UnitId = _tamUnitId,
        });
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(TestJson.Options);

        var delete = await _client.DeleteAsync($"/api/products/{created!.Data!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>(
            "/api/products?page=1&pageSize=200", TestJson.Options);
        list!.Data!.Items.Should().NotContain(i => i.Id == created.Data.Id);
    }
}
