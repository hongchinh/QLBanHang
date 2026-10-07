using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Admin.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;
using Xunit;

namespace OrderMgmt.IntegrationTests.Admin;

[Collection(nameof(PostgresCollection))]
public class AdminRolesPermissionsTests : QuotationTestBase
{
    public AdminRolesPermissionsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task ListPermissions_returns_all_seeded_permissions()
    {
        var res = await _client.GetAsync("/api/admin/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<PermissionDto>>>(TestJson.Options);
        body!.Data!.Should().Contain(p => p.Code == Permissions.Quotations.View);
        body.Data.Should().Contain(p => p.Code == Permissions.Roles.Manage);
        body.Data.Should().Contain(p => p.Code == Permissions.Reports.Revenue);

        // Module values are constrained.
        body.Data.Select(p => p.Module).Distinct()
            .Should().BeSubsetOf(new[] { "system", "catalog", "sales", "inventory", "report" });
    }

    [Fact]
    public async Task UpdatePermissions_custom_role_replaces_set()
    {
        var created = await CreateCustomRoleAsync(
            "TEST_UP_PERMS", "Update perms target",
            new[] { Permissions.Quotations.View });

        var update = new UpdateRolePermissionsRequest
        {
            PermissionCodes = new[] { Permissions.Customers.View, Permissions.Products.View },
        };
        var res = await _client.PutAsJsonAsync($"/api/admin/roles/{created.Id}/permissions", update);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(TestJson.Options);
        body!.Data!.PermissionCodes.Should().BeEquivalentTo(
            new[] { Permissions.Customers.View, Permissions.Products.View });
    }

    [Fact]
    public async Task UpdatePermissions_non_admin_system_role_succeeds()
    {
        var sales = await GetRoleByCodeAsync(RoleCodes.Sales);
        var newCodes = sales.PermissionCodes
            .Append(Permissions.Quotations.Delete)
            .Distinct()
            .ToArray();

        var update = new UpdateRolePermissionsRequest { PermissionCodes = newCodes };
        var res = await _client.PutAsJsonAsync($"/api/admin/roles/{sales.Id}/permissions", update);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(TestJson.Options);
        body!.Data!.PermissionCodes.Should().Contain(Permissions.Quotations.Delete);
    }

    [Fact]
    public async Task UpdatePermissions_admin_role_returns_403()
    {
        var admin = await GetRoleByCodeAsync(RoleCodes.Admin);

        var update = new UpdateRolePermissionsRequest { PermissionCodes = Array.Empty<string>() };
        var res = await _client.PutAsJsonAsync($"/api/admin/roles/{admin.Id}/permissions", update);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePermissions_with_invalid_code_returns_409()
    {
        var created = await CreateCustomRoleAsync("TEST_BAD_UP", "Bad update", Array.Empty<string>());

        var update = new UpdateRolePermissionsRequest
        {
            PermissionCodes = new[] { "nonexistent.permission" },
        };
        var res = await _client.PutAsJsonAsync($"/api/admin/roles/{created.Id}/permissions", update);
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task User_with_updated_role_sees_new_permissions_after_token_refresh()
    {
        // 1. Create SALES user; login → access token must NOT contain quotations.delete.
        await CreateTestUserAsync("ut_live_perm", "Pass@123", RoleCodes.Sales);

        using var userClient = _factory.CreateClient();
        var login = await userClient.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "ut_live_perm", Password = "Pass@123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);

        var initialPerms = ReadPermissionClaims(loginBody!.Data!.AccessToken);
        initialPerms.Should().NotContain(Permissions.Quotations.Delete);

        // 2. Admin grants quotations.delete to SALES.
        var sales = await GetRoleByCodeAsync(RoleCodes.Sales);
        var newCodes = sales.PermissionCodes.Append(Permissions.Quotations.Delete).Distinct().ToArray();
        var update = await _client.PutAsJsonAsync(
            $"/api/admin/roles/{sales.Id}/permissions",
            new UpdateRolePermissionsRequest { PermissionCodes = newCodes });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. User refreshes → new access token must contain quotations.delete.
        var refreshResp = await userClient.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = loginBody.Data!.RefreshToken });
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<ApiResponse<TokenPairResponse>>(TestJson.Options);
        var refreshedPerms = ReadPermissionClaims(refreshBody!.Data!.AccessToken);
        refreshedPerms.Should().Contain(Permissions.Quotations.Delete);

        // Cleanup: revert SALES permissions to original so other tests aren't affected.
        await _client.PutAsJsonAsync(
            $"/api/admin/roles/{sales.Id}/permissions",
            new UpdateRolePermissionsRequest { PermissionCodes = sales.PermissionCodes.ToArray() });
    }

    private async Task<RoleDetailDto> CreateCustomRoleAsync(string code, string name, string[] perms)
    {
        var payload = new CreateRoleRequest { Code = code, Name = name, PermissionCodes = perms };
        var res = await _client.PostAsJsonAsync("/api/admin/roles", payload);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(TestJson.Options);
        return body!.Data!;
    }

    private async Task<RoleDetailDto> GetRoleByCodeAsync(string code)
    {
        var listRes = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<RoleListItemDto>>>(
            "/api/admin/roles", TestJson.Options);
        var item = listRes!.Data!.First(r => r.Code == code);
        var detail = await _client.GetFromJsonAsync<ApiResponse<RoleDetailDto>>(
            $"/api/admin/roles/{item.Id}", TestJson.Options);
        return detail!.Data!;
    }

    private static IReadOnlyList<string> ReadPermissionClaims(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();
    }
}
