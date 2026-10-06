using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Admin.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;
using Xunit;

namespace OrderMgmt.IntegrationTests.Admin;

[Collection(nameof(PostgresCollection))]
public class AdminUserCrudTests : QuotationTestBase
{
    public AdminUserCrudTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Create_then_get_returns_detail_dto()
    {
        var payload = new CreateUserRequest
        {
            Username = "ut_create_ok",
            Email = "ut_create_ok@test.local",
            FullName = "Create OK",
            PhoneNumber = "0900000001",
            RoleCode = RoleCodes.Sales,
            Password = "Pass@123",
            Status = UserStatus.Active,
        };

        var create = await _client.PostAsJsonAsync("/api/admin/users", payload);
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await create.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>(TestJson.Options);
        created!.Data!.Username.Should().Be("ut_create_ok");
        created.Data.Email.Should().Be("ut_create_ok@test.local");
        created.Data.RoleCode.Should().Be(RoleCodes.Sales);
        created.Data.Status.Should().Be(UserStatus.Active);

        var get = await _client.GetAsync($"/api/admin/users/{created.Data.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await get.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>(TestJson.Options);
        dto!.Data!.Id.Should().Be(created.Data.Id);
        dto.Data.Username.Should().Be("ut_create_ok");
    }

    [Fact]
    public async Task Create_with_duplicate_username_returns_conflict()
    {
        await CreateTestUserAsync("dup_user", "Sales@123", RoleCodes.Sales);

        var payload = new CreateUserRequest
        {
            Username = "dup_user",
            Email = "dup_user_2@test.local",
            FullName = "Dup",
            RoleCode = RoleCodes.Sales,
            Password = "Pass@123",
            Status = UserStatus.Active,
        };
        var res = await _client.PostAsJsonAsync("/api/admin/users", payload);
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options);
        body!.Error!.Code.Should().Be("CONFLICT");
        body.Error.Message.Should().Contain("tồn tại");
    }

    [Fact]
    public async Task Update_changing_role_keeps_single_user_role_row()
    {
        var created = await CreateUserViaApiAsync("ut_role_swap", RoleCodes.Sales);

        var update = new UpdateUserRequest
        {
            FullName = "Role Swap",
            Email = "ut_role_swap@test.local",
            RoleCode = RoleCodes.Manager,
            Status = UserStatus.Active,
        };
        var res = await _client.PutAsJsonAsync($"/api/admin/users/{created.Id}", update);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = await db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == created.Id)
            .ToListAsync();
        roles.Should().HaveCount(1);
        roles[0].Role.Code.Should().Be(RoleCodes.Manager);
    }

    [Fact]
    public async Task ResetPassword_allows_login_with_new_password_and_revokes_old_refresh_tokens()
    {
        var created = await CreateUserViaApiAsync("ut_reset", RoleCodes.Sales, password: "Pass@123");

        // Login as the new user to issue a refresh token (saved as a fresh HttpClient — won't affect _client).
        using (var userClient = _factory.CreateClient())
        {
            var login = await userClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "ut_reset", Password = "Pass@123" });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.RefreshTokens.CountAsync(rt => rt.UserId == created.Id && rt.RevokedAt == null))
                .Should().Be(1);
        }

        // Re-authenticate _client as admin (it should still be authed, but be safe).
        await AuthenticateAsync("admin", "Admin@123");

        var reset = await _client.PostAsJsonAsync($"/api/admin/users/{created.Id}/reset-password",
            new ResetPasswordRequest { NewPassword = "NewPass@123" });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var userClient = _factory.CreateClient())
        {
            var loginNew = await userClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "ut_reset", Password = "NewPass@123" });
            loginNew.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginOld = await userClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "ut_reset", Password = "Pass@123" });
            loginOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var revoked = await verifyDb.RefreshTokens
            .Where(rt => rt.UserId == created.Id && rt.RevokedReason == "PASSWORD_RESET")
            .CountAsync();
        revoked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Delete_user_with_active_quotations_returns_conflict()
    {
        var created = await CreateUserViaApiAsync("ut_owner", RoleCodes.Sales, password: "Pass@123");

        using (var userClient = _factory.CreateClient())
        {
            var login = await userClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "ut_owner", Password = "Pass@123" });
            var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
            userClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);

            var quote = await userClient.PostAsJsonAsync("/api/quotations", BuildRequest());
            quote.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await AuthenticateAsync("admin", "Admin@123");

        var del = await _client.DeleteAsync($"/api/admin/users/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body2 = await del.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options);
        body2!.Error!.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task Delete_self_returns_forbidden()
    {
        var adminId = await GetUserIdAsync("admin");
        var del = await _client.DeleteAsync($"/api/admin/users/{adminId}");
        del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetStatus_disable_self_returns_forbidden()
    {
        var adminId = await GetUserIdAsync("admin");
        var res = await _client.PostAsJsonAsync(
            $"/api/admin/users/{adminId}/status",
            new SetUserStatusRequest { Status = UserStatus.Disabled });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetStatus_disable_other_user_blocks_login_and_revokes_tokens()
    {
        var created = await CreateUserViaApiAsync("ut_disable", RoleCodes.Sales, password: "Pass@123");

        using (var userClient = _factory.CreateClient())
        {
            var login = await userClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "ut_disable", Password = "Pass@123" });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var disable = await _client.PostAsJsonAsync(
            $"/api/admin/users/{created.Id}/status",
            new SetUserStatusRequest { Status = UserStatus.Disabled });
        disable.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var userClient = _factory.CreateClient())
        {
            var loginAgain = await userClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "ut_disable", Password = "Pass@123" });
            loginAgain.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RefreshTokens
            .Where(rt => rt.UserId == created.Id && rt.RevokedReason == "USER_DISABLED")
            .CountAsync())
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_without_status_field_defaults_to_active()
    {
        // Send a JSON body that omits the `status` key entirely → must default to Active
        // (regression guard for the UserStatus.Disabled = 0 default-enum bug).
        var json = JsonSerializer.Serialize(new
        {
            username = "ut_no_status",
            email = "ut_no_status@test.local",
            fullName = "No Status",
            roleCode = RoleCodes.Sales,
            password = "Pass@123",
        });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var res = await _client.PostAsync("/api/admin/users", content);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>(TestJson.Options);
        body!.Data!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task SoftDelete_cascades_to_user_quotation_settings()
    {
        var created = await CreateUserViaApiAsync("ut_uqs", RoleCodes.Sales);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserQuotationSettings.Add(new Domain.Entities.Identity.UserQuotationSettings
            {
                UserId = created.Id,
                LockAtStatus = QuotationStatus.Sent,
            });
            await db.SaveChangesAsync();
        }

        var del = await _client.DeleteAsync($"/api/admin/users/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var uqs = await verifyDb.UserQuotationSettings
            .IgnoreQueryFilters()
            .FirstAsync(s => s.UserId == created.Id);
        uqs.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDelete_revokes_active_refresh_tokens_with_user_deleted_reason()
    {
        var created = await CreateUserViaApiAsync("ut_del_revoke", RoleCodes.Sales, password: "Pass@123");

        using (var userClient = _factory.CreateClient())
        {
            var login = await userClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Username = "ut_del_revoke", Password = "Pass@123" });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var del = await _client.DeleteAsync($"/api/admin/users/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = await db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstAsync(rt => rt.UserId == created.Id);
        token.RevokedReason.Should().Be("USER_DELETED");
        token.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_assigns_main_branch_by_default_or_the_given_branch()
    {
        var withDefault = await CreateUserViaApiAsync("ut_branch_default", RoleCodes.Sales);
        withDefault.DefaultBranchId.Should().Be(BranchDefaults.MainBranchId);
        withDefault.DefaultBranchName.Should().Be("Chi nhánh chính");

        var b = await CreateBranchAsync("CN05");
        var withBranch = await _client.PostAsJsonAsync("/api/admin/users", NewUser("ut_branch_b", b));
        withBranch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await withBranch.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>(TestJson.Options))!
            .Data!.DefaultBranchId.Should().Be(b);

        var unknown = await _client.PostAsJsonAsync("/api/admin/users", NewUser("ut_branch_unknown", Guid.NewGuid()));
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknown.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!
            .Error!.Code.Should().Be("BRANCH_NOT_FOUND");
    }

    [Fact]
    public async Task Update_changes_default_branch_only_when_given()
    {
        var created = await CreateUserViaApiAsync("ut_branch_upd", RoleCodes.Sales);
        var b = await CreateBranchAsync("CN06");

        var withBranch = await _client.PutAsJsonAsync($"/api/admin/users/{created.Id}", new UpdateUserRequest
        {
            FullName = "Branch Update", Email = "ut_branch_upd@test.local", RoleCode = RoleCodes.Sales,
            Status = UserStatus.Active, DefaultBranchId = b,
        });
        (await withBranch.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>(TestJson.Options))!
            .Data!.DefaultBranchId.Should().Be(b);

        var withoutBranch = await _client.PutAsJsonAsync($"/api/admin/users/{created.Id}", new UpdateUserRequest
        {
            FullName = "Branch Update 2", Email = "ut_branch_upd@test.local", RoleCode = RoleCodes.Sales,
            Status = UserStatus.Active,
        });
        (await withoutBranch.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>(TestJson.Options))!
            .Data!.DefaultBranchId.Should().Be(b);
    }

    private static CreateUserRequest NewUser(string username, Guid? defaultBranchId) => new()
    {
        Username = username,
        Email = $"{username}@test.local",
        FullName = username,
        RoleCode = RoleCodes.Sales,
        Password = "Pass@123",
        Status = UserStatus.Active,
        DefaultBranchId = defaultBranchId,
    };

    private async Task<Guid> CreateBranchAsync(string code)
    {
        var res = await _client.PostAsJsonAsync("/api/branches",
            new OrderMgmt.Application.Organization.Branches.Models.CreateBranchRequest { Code = code, Name = code });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<OrderMgmt.Application.Organization.Branches.Models.BranchDto>>(TestJson.Options);
        return body!.Data!.Id;
    }

    private async Task<AdminUserDetailDto> CreateUserViaApiAsync(
        string username, string roleCode, string password = "Pass@123")
    {
        var res = await _client.PostAsJsonAsync("/api/admin/users", new CreateUserRequest
        {
            Username = username,
            Email = $"{username}@test.local",
            FullName = username,
            RoleCode = roleCode,
            Password = password,
            Status = UserStatus.Active,
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AdminUserDetailDto>>(TestJson.Options);
        return body!.Data!;
    }

    private async Task<Guid> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Username == username);
        return user.Id;
    }
}
