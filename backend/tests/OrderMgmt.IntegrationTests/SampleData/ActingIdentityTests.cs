using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Domain.Constants;
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
}
