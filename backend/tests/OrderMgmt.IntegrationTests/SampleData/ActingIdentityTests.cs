using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Infrastructure.Identity;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class ActingIdentityTests : CashDebtTestBase
{
    public ActingIdentityTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Background_scope_without_acting_identity_has_no_user()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        user.UserId.Should().BeNull();
        user.HasPermission(Permissions.Branches.Manage).Should().BeFalse();
    }

    [Fact]
    public async Task Acting_identity_drives_current_user_and_current_branch()
    {
        var branchId = await CreateBranchAsync("AI01");
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IActingIdentity>().Set(
            new ActingIdentity(adminId, "admin", "Quản trị", branchId, new[] { Permissions.Branches.Manage }));

        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        user.UserId.Should().Be(adminId);
        user.Username.Should().Be("admin");
        user.FullName.Should().Be("Quản trị");
        user.IsAuthenticated.Should().BeTrue();
        user.HasPermission(Permissions.Branches.Manage).Should().BeTrue();
        user.HasPermission(Permissions.Branches.AccessAll).Should().BeFalse();
        user.Roles.Should().BeEmpty();
        user.IsInRole(RoleCodes.Admin).Should().BeFalse();

        // Not the admin's default branch, and the scope has no X-Branch-Id header.
        (await scope.ServiceProvider.GetRequiredService<ICurrentBranch>().GetIdAsync()).Should().Be(branchId);
    }

    [Fact]
    public async Task Service_called_under_acting_identity_writes_in_that_branch()
    {
        var branchId = await CreateBranchAsync("AI02");
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var allPermissions = await db.Permissions.Select(p => p.Code).ToListAsync();
        scope.ServiceProvider.GetRequiredService<IActingIdentity>().Set(
            new ActingIdentity(adminId, "admin", "Quản trị", branchId, allPermissions));

        var warehouse = await scope.ServiceProvider.GetRequiredService<IWarehouseService>()
            .CreateAsync(new CreateWarehouseRequest { Code = "AI02-K1", Name = "Kho AI" });

        warehouse.BranchId.Should().Be(branchId);
        var createdBy = await InDbAsync(d => d.Warehouses.Where(w => w.Id == warehouse.Id).Select(w => w.CreatedBy).SingleAsync());
        createdBy.Should().Be(adminId);
    }

    [Fact]
    public async Task Acting_identity_wins_over_an_http_principal()
    {
        var branchId = await CreateBranchAsync("AI03");
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());

        await using var scope = _factory.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        try
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "other"),
                new Claim(ClaimTypes.Email, "x@y.z"),
                new Claim("full_name", "Other"),
                new Claim(ClaimTypes.Role, RoleCodes.Admin),
                new Claim(JwtTokenGenerator.PermissionClaimType, Permissions.Branches.AccessAll),
            }, "test");
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            http.Request.Headers["X-Branch-Id"] = BranchDefaults.MainBranchId.ToString();
            accessor.HttpContext = http;

            scope.ServiceProvider.GetRequiredService<IActingIdentity>().Set(
                new ActingIdentity(adminId, "admin", "Quản trị", branchId, new[] { Permissions.Branches.Manage }));

            var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
            user.UserId.Should().Be(adminId);
            user.Username.Should().Be("admin");
            user.FullName.Should().Be("Quản trị");
            user.Email.Should().BeNull();
            user.IsAuthenticated.Should().BeTrue();
            user.Roles.Should().BeEmpty();
            user.IsInRole(RoleCodes.Admin).Should().BeFalse();
            user.Permissions.Should().Equal(Permissions.Branches.Manage);
            user.HasPermission(Permissions.Branches.AccessAll).Should().BeFalse();
            (await scope.ServiceProvider.GetRequiredService<ICurrentBranch>().GetIdAsync()).Should().Be(branchId);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    [Fact]
    public async Task Setting_the_acting_identity_twice_in_one_scope_throws()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<IActingIdentity>();
        var identity = new ActingIdentity(Guid.NewGuid(), "u", "U", Guid.NewGuid(), Array.Empty<string>());
        holder.Set(identity);

        var second = () => holder.Set(identity);

        second.Should().Throw<InvalidOperationException>();
    }
}
