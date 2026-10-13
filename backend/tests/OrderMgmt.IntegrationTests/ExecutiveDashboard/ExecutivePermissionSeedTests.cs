using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

[Collection(nameof(PostgresCollection))]
public class ExecutivePermissionSeedTests : CashDebtTestBase
{
    public ExecutivePermissionSeedTests(PostgresFixture pg) : base(pg) { }

    private Task<HashSet<string>> RoleCodesAsync(string roleCode) => InDbAsync(async db =>
        (await db.Roles.AsNoTracking()
            .Where(r => r.Code == roleCode)
            .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code))
            .ToListAsync()).ToHashSet());

    [Fact]
    public async Task Permission_exists_in_report_module()
    {
        var module = await InDbAsync(db => db.Permissions.AsNoTracking()
            .Where(p => p.Code == Permissions.Dashboard.Executive)
            .Select(p => p.Module)
            .SingleAsync());

        module.Should().Be(Permissions.ReportModule);
    }

    [Fact]
    public async Task Admin_and_manager_hold_it()
    {
        (await RoleCodesAsync(RoleCodes.Admin)).Should().Contain(Permissions.Dashboard.Executive);
        (await RoleCodesAsync(RoleCodes.Manager)).Should().Contain(Permissions.Dashboard.Executive);
    }

    [Fact]
    public async Task Other_roles_do_not()
    {
        foreach (var role in new[] { RoleCodes.Accountant, RoleCodes.Sales, RoleCodes.Warehouse })
            (await RoleCodesAsync(role)).Should().NotContain(Permissions.Dashboard.Executive, role);
    }
}
