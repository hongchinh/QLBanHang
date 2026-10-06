using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;

namespace OrderMgmt.IntegrationTests.Inventory;

public abstract class InventoryTestBase : QuotationTestBase
{
    protected static readonly Guid MainBranchId = BranchDefaults.MainBranchId;

    protected InventoryTestBase(PostgresFixture pg) : base(pg) { }

    protected async Task InDbAsync(Func<AppDbContext, Task> work)
    {
        using var scope = _factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// Inserts a custom role T_{username} with exactly these permissions and a user (password Pass@123),
    /// then returns a client logged in as that user.
    protected async Task<HttpClient> CreateClientWithPermissionsAsync(
        string username, Guid? defaultBranchId, params string[] permissionCodes)
    {
        await InDbAsync(async db =>
        {
            var hasher = _factory.Services.GetRequiredService<IPasswordHasher>();
            var permissions = await db.Permissions.Where(p => permissionCodes.Contains(p.Code)).ToListAsync();
            permissions.Should().HaveCount(permissionCodes.Distinct().Count(), "every requested permission code must exist");

            var role = new Role { Code = $"T_{username}".ToUpperInvariant(), Name = $"Test {username}", IsSystem = false };
            foreach (var p in permissions)
                role.RolePermissions.Add(new RolePermission { Role = role, PermissionId = p.Id });
            db.Roles.Add(role);

            db.Users.Add(new User
            {
                Username = username,
                Email = $"{username}@test.local",
                FullName = $"Test {username}",
                PasswordHash = hasher.Hash("Pass@123"),
                Status = UserStatus.Active,
                DefaultBranchId = defaultBranchId ?? MainBranchId,
                UserRoles = new List<UserRole> { new() { Role = role } },
            });
            await db.SaveChangesAsync();
        });

        return await LoginAsync(username, "Pass@123");
    }

    protected async Task<HttpClient> CreateClientForRoleAsync(string username, string roleCode, Guid? defaultBranchId = null)
    {
        await CreateTestUserAsync(username, "Pass@123", roleCode, defaultBranchId);
        return await LoginAsync(username, "Pass@123");
    }

    /// A new client carrying the admin token (no extra login: the login endpoint is rate-limited).
    protected HttpClient CloneAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        return client;
    }

    protected static void UseBranch(HttpClient client, Guid branchId)
    {
        client.DefaultRequestHeaders.Remove("X-Branch-Id");
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
    }

    protected async Task<Guid> CreateBranchAsync(string code)
    {
        var response = await _client.PostAsJsonAsync("/api/branches", new CreateBranchRequest { Code = code, Name = $"Chi nhánh {code}" });
        return (await ReadDataAsync<BranchDto>(response)).Id;
    }

    protected async Task<Guid> CreateWarehouseAsync(Guid branchId, string code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouses")
        {
            Content = JsonContent.Create(new CreateWarehouseRequest { Code = code, Name = $"Kho {code}", IsActive = true }),
        };
        request.Headers.Add("X-Branch-Id", branchId.ToString());
        var response = await _client.SendAsync(request);
        return (await ReadDataAsync<WarehouseDto>(response)).Id;
    }

    /// Direct DB update of the singleton (bypasses the API and its recalculation).
    protected Task UpdateInventorySettingsAsync(Action<OrderMgmt.Domain.Entities.Inventory.InventorySettings> mutate) =>
        InDbAsync(async db =>
        {
            var settings = await db.InventorySettings.SingleAsync(s => s.Id == 1);
            mutate(settings);
            await db.SaveChangesAsync();
        });

    protected Task<Guid> CreatePartnerAsync(string code, bool isCustomer, bool isSupplier) =>
        InDbAsync(async db =>
        {
            var partner = new OrderMgmt.Domain.Entities.Catalog.Customer
            {
                Code = code,
                Name = $"Đối tượng {code}",
                TaxCode = "0100000000",
                CompanyAddress = "Hà Nội",
                IsCustomer = isCustomer,
                IsSupplier = isSupplier,
            };
            db.Customers.Add(partner);
            await db.SaveChangesAsync();
            return partner.Id;
        });

    protected static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"expected success but got {(int)response.StatusCode}: {body}");
        var parsed = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<T>>(body, TestJson.Options);
        return parsed!.Data!;
    }

    protected async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }
}
