using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Identity.Admin.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.Infrastructure.Persistence.Seed;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;
using Xunit;

namespace OrderMgmt.IntegrationTests.Admin;

[Collection(nameof(PostgresCollection))]
public class DbSeederUpgradeTests : QuotationTestBase
{
    public DbSeederUpgradeTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Reseeding_does_not_overwrite_modified_system_role_permissions()
    {
        // Establish SALES default state (has customers.view by seed).
        var sales = await GetRoleByCodeAsync(RoleCodes.Sales);
        sales.PermissionCodes.Should().Contain(Permissions.Customers.View);

        // Remove customers.view via admin API (simulating an admin's customisation).
        var trimmed = sales.PermissionCodes
            .Where(c => c != Permissions.Customers.View)
            .ToArray();
        var put = await _client.PutAsJsonAsync(
            $"/api/admin/roles/{sales.Id}/permissions",
            new UpdateRolePermissionsRequest { PermissionCodes = trimmed });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-run the seeder. The non-Admin system role branch must skip re-seeding.
        await DbSeeder.SeedAsync(_factory.Services);

        // Assert: SALES still does NOT have customers.view.
        var after = await GetRoleByCodeAsync(RoleCodes.Sales);
        after.PermissionCodes.Should().NotContain(Permissions.Customers.View);

        // Restore for next test (defensive, although each test uses a fresh DB).
        await _client.PutAsJsonAsync(
            $"/api/admin/roles/{sales.Id}/permissions",
            new UpdateRolePermissionsRequest { PermissionCodes = sales.PermissionCodes.ToArray() });
    }

    [Fact]
    public async Task Reseeding_keeps_admin_full_permissions()
    {
        // Snapshot all permission codes from the DB.
        IReadOnlyList<string> allCodes;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            allCodes = await db.Permissions.Select(p => p.Code).ToListAsync();
        }

        // Re-run the seeder.
        await DbSeeder.SeedAsync(_factory.Services);

        // Assert: ADMIN has every permission code.
        var admin = await GetRoleByCodeAsync(RoleCodes.Admin);
        admin.PermissionCodes.Should().BeEquivalentTo(allCodes);
    }

    [Fact]
    public async Task Reseeding_fallback_restores_default_when_system_role_has_zero_permissions()
    {
        // Wipe all RolePermissions for SALES directly via DbContext, simulating an out-of-band
        // DB edit (the API would refuse to leave a 0-permission state in some flows but the
        // service does allow an empty list — either way this models the fallback's purpose).
        var sales = await GetRoleByCodeAsync(RoleCodes.Sales);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == sales.Id)
                .ToListAsync();
            db.RolePermissions.RemoveRange(rows);
            await db.SaveChangesAsync();
        }

        // Pre-assertion: SALES has 0 permissions.
        var emptied = await GetRoleByCodeAsync(RoleCodes.Sales);
        emptied.PermissionCodes.Should().BeEmpty();

        // Re-run seeder → fallback branch should restore defaults.
        await DbSeeder.SeedAsync(_factory.Services);

        var restored = await GetRoleByCodeAsync(RoleCodes.Sales);
        restored.PermissionCodes.Should().Contain(Permissions.Quotations.View);
        restored.PermissionCodes.Should().Contain(Permissions.Customers.View);
    }

    private static readonly string[] Round1Codes =
    {
        Permissions.Branches.Manage, Permissions.Branches.AccessAll, Permissions.PeriodLock.Manage,
        Permissions.Suppliers.View, Permissions.Suppliers.Create, Permissions.Suppliers.Update, Permissions.Suppliers.Delete,
        Permissions.StockIn.View, Permissions.StockIn.Create, Permissions.StockIn.Edit, Permissions.StockIn.Delete,
        Permissions.StockIn.Cancel, Permissions.StockIn.EditAll,
        Permissions.StockOut.View, Permissions.StockOut.Create, Permissions.StockOut.Edit, Permissions.StockOut.Delete,
        Permissions.StockOut.Cancel, Permissions.StockOut.EditAll,
        Permissions.Inventory.OpeningStock, Permissions.Inventory.ViewCost, Permissions.Inventory.ManageCatalogs,
        Permissions.Inventory.Settings, Permissions.Inventory.RecalcCost,
        Permissions.Reports.Inventory,
    };

    [Fact]
    public async Task Fresh_seed_has_round1_permissions_and_warehouse_defaults()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var codes = await db.Permissions.Select(p => p.Code).ToListAsync();
            codes.Should().Contain(Round1Codes);
        }

        var warehouse = await GetRoleByCodeAsync(RoleCodes.Warehouse);
        warehouse.PermissionCodes.Should().Contain(new[]
        {
            Permissions.StockIn.View, Permissions.StockIn.Create, Permissions.StockIn.Edit,
            Permissions.StockIn.Delete, Permissions.StockIn.Cancel,
            Permissions.StockOut.View, Permissions.StockOut.Create, Permissions.StockOut.Edit,
            Permissions.StockOut.Delete, Permissions.StockOut.Cancel,
            Permissions.Inventory.OpeningStock, Permissions.Reports.Inventory, Permissions.Suppliers.View,
        });
        warehouse.PermissionCodes.Should().NotContain(new[]
        {
            Permissions.Inventory.ViewCost, Permissions.StockIn.EditAll, Permissions.Inventory.Settings,
        });
    }

    [Fact]
    public async Task Reseeding_grants_newly_introduced_permission_to_roles_whose_defaults_include_it()
    {
        // Simulate an upgrade from a release that did not know stock_out.view yet.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var permission = await db.Permissions.SingleAsync(p => p.Code == Permissions.StockOut.View);
            var assignments = await db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => rp.PermissionId == permission.Id)
                .ToListAsync();
            db.RolePermissions.RemoveRange(assignments);
            db.Permissions.Remove(permission);
            await db.SaveChangesAsync();
        }

        await DbSeeder.SeedAsync(_factory.Services);

        (await GetRoleByCodeAsync(RoleCodes.Warehouse)).PermissionCodes.Should().Contain(Permissions.StockOut.View);
        (await GetRoleByCodeAsync(RoleCodes.Manager)).PermissionCodes.Should().Contain(Permissions.StockOut.View);
        (await GetRoleByCodeAsync(RoleCodes.Sales)).PermissionCodes.Should().NotContain(Permissions.StockOut.View);
    }

    [Fact]
    public async Task Reseeding_does_not_regrant_inventory_permission_removed_by_admin()
    {
        var warehouse = await GetRoleByCodeAsync(RoleCodes.Warehouse);
        var trimmed = warehouse.PermissionCodes.Where(c => c != Permissions.StockIn.View).ToArray();
        var put = await _client.PutAsJsonAsync(
            $"/api/admin/roles/{warehouse.Id}/permissions",
            new UpdateRolePermissionsRequest { PermissionCodes = trimmed });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        await DbSeeder.SeedAsync(_factory.Services);

        (await GetRoleByCodeAsync(RoleCodes.Warehouse)).PermissionCodes.Should().NotContain(Permissions.StockIn.View);
    }

    private async Task<RoleDetailDto> GetRoleByCodeAsync(string code)
    {
        var list = await _client.GetFromJsonAsync<OrderMgmt.Application.Common.Models.ApiResponse<IReadOnlyList<RoleListItemDto>>>(
            "/api/admin/roles", TestJson.Options);
        var item = list!.Data!.First(r => r.Code == code);
        var detail = await _client.GetFromJsonAsync<OrderMgmt.Application.Common.Models.ApiResponse<RoleDetailDto>>(
            $"/api/admin/roles/{item.Id}", TestJson.Options);
        return detail!.Data!;
    }
}
