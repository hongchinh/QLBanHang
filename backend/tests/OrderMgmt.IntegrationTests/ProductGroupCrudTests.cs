using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Catalog.ProductGroups.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class ProductGroupCrudTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;
    private Guid _existingGroupId;

    public ProductGroupCrudTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();
        await ResolveExistingGroupIdAsync();
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

    private async Task ResolveExistingGroupIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _existingGroupId = (await db.ProductGroups.FirstAsync(g => g.Code == "EPS")).Id;
    }

    // --- Happy path CRUD ---

    [Fact]
    public async Task Create_returns_dto_with_generated_code()
    {
        var response = await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Name = "Nhóm Test " + Guid.NewGuid().ToString("N")[..6],
            SortOrder = 10,
            IsActive = true,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProductGroupDto>>(TestJson.Options);
        body!.Data!.Code.Should().StartWith("NG-");
        body.Data.IsActive.Should().BeTrue();
        body.Data.SortOrder.Should().Be(10);
    }

    [Fact]
    public async Task Create_with_explicit_code_stores_that_code()
    {
        var uniqueCode = "TG-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var response = await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Code = uniqueCode,
            Name = "Nhóm mã tường minh",
            IsActive = true,
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProductGroupDto>>(TestJson.Options);
        body!.Data!.Code.Should().Be(uniqueCode);
    }

    [Fact]
    public async Task Create_duplicate_code_returns_409()
    {
        var uniqueCode = "DUP-" + Guid.NewGuid().ToString("N")[..4].ToUpper();

        await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Code = uniqueCode, Name = "First", IsActive = true,
        });

        var second = await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Code = uniqueCode, Name = "Second", IsActive = true,
        });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_returns_correct_group()
    {
        var get = await _client.GetFromJsonAsync<ApiResponse<ProductGroupDto>>(
            $"/api/product-groups/{_existingGroupId}", TestJson.Options);
        get!.Data!.Id.Should().Be(_existingGroupId);
        get.Data.Code.Should().Be("EPS");
    }

    [Fact]
    public async Task List_returns_paginated_results()
    {
        var list = await _client.GetFromJsonAsync<ApiResponse<PagedResult<ProductGroupListItemDto>>>(
            "/api/product-groups?page=1&pageSize=20", TestJson.Options);
        list!.Data!.Items.Should().NotBeEmpty();
        list.Data.Page.Should().Be(1);
    }

    [Fact]
    public async Task List_search_filters_by_name_or_code()
    {
        var uniqueName = "SearchTarget-" + Guid.NewGuid().ToString("N")[..6];
        await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Name = uniqueName, IsActive = true,
        });

        var list = await _client.GetFromJsonAsync<ApiResponse<PagedResult<ProductGroupListItemDto>>>(
            $"/api/product-groups?page=1&pageSize=20&search={Uri.EscapeDataString(uniqueName[..10])}",
            TestJson.Options);
        list!.Data!.Items.Should().Contain(i => i.Name == uniqueName);
    }

    [Fact]
    public async Task List_filter_by_isActive_false_excludes_active_groups()
    {
        var uniqueName = "Inactive-" + Guid.NewGuid().ToString("N")[..6];
        await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Name = uniqueName, IsActive = false,
        });

        var list = await _client.GetFromJsonAsync<ApiResponse<PagedResult<ProductGroupListItemDto>>>(
            "/api/product-groups?page=1&pageSize=200&isActive=false", TestJson.Options);
        list!.Data!.Items.Should().Contain(i => i.Name == uniqueName);
        list.Data.Items.Should().OnlyContain(i => !i.IsActive);
    }

    [Fact]
    public async Task Update_changes_name_and_sortOrder()
    {
        var create = await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Name = "Before Update", IsActive = true, SortOrder = 0,
        });
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<ProductGroupDto>>(TestJson.Options);

        var update = await _client.PutAsJsonAsync($"/api/product-groups/{created!.Data!.Id}",
            new UpdateProductGroupRequest
            {
                Name = "After Update",
                SortOrder = 5,
                IsActive = true,
            });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await _client.GetFromJsonAsync<ApiResponse<ProductGroupDto>>(
            $"/api/product-groups/{created.Data.Id}", TestJson.Options);
        get!.Data!.Name.Should().Be("After Update");
        get.Data.SortOrder.Should().Be(5);
        get.Data.Code.Should().Be(created.Data.Code);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_excludes_from_list()
    {
        var create = await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Name = "Will Be Deleted", IsActive = true,
        });
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<ProductGroupDto>>(TestJson.Options);

        var delete = await _client.DeleteAsync($"/api/product-groups/{created!.Data!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<PagedResult<ProductGroupListItemDto>>>(
            "/api/product-groups?page=1&pageSize=200", TestJson.Options);
        list!.Data!.Items.Should().NotContain(i => i.Id == created.Data.Id);
    }

    [Fact]
    public async Task Delete_group_with_products_nullifies_product_group_reference()
    {
        var grpResp = await _client.PostAsJsonAsync("/api/product-groups", new CreateProductGroupRequest
        {
            Name = "Group With Products", IsActive = true,
        });
        var grp = await grpResp.Content.ReadFromJsonAsync<ApiResponse<ProductGroupDto>>(TestJson.Options);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var anyUnitId = (await db.Units.FirstAsync()).Id;

        await _client.PostAsJsonAsync("/api/products", new
        {
            name = "Test Product for Group Delete",
            productGroupId = grp!.Data!.Id,
            unitId = anyUnitId,
        });

        var delete = await _client.DeleteAsync($"/api/product-groups/{grp.Data.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.ProductGroupId == grp.Data.Id);
        product.Should().BeNull("ProductGroupId should be nullified by cascade");
    }

    // --- Authorization ---

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync("/api/product-groups");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
