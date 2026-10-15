using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// Quick add of partners from the voucher forms (brainstorm 261015-0644, D12): which seed roles may create them.
[Collection(nameof(PostgresCollection))]
public class PartnerQuickAddSeedTests : CashDebtTestBase
{
    public PartnerQuickAddSeedTests(PostgresFixture pg) : base(pg) { }

    private Task<HashSet<string>> RoleCodesAsync(string roleCode) => InDbAsync(async db =>
        (await db.Roles.AsNoTracking()
            .Where(r => r.Code == roleCode)
            .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code))
            .ToListAsync()).ToHashSet());

    [Fact]
    public async Task Accountant_can_quick_add_customers_and_suppliers()
    {
        var codes = await RoleCodesAsync(RoleCodes.Accountant);
        codes.Should().Contain(new[] { Permissions.Customers.Create, Permissions.Suppliers.View, Permissions.Suppliers.Create });
    }

    [Fact]
    public async Task Warehouse_can_quick_add_customers_and_suppliers()
    {
        var codes = await RoleCodesAsync(RoleCodes.Warehouse);
        codes.Should().Contain(new[]
        {
            Permissions.Customers.View, Permissions.Customers.Create,
            Permissions.Suppliers.View, Permissions.Suppliers.Create,
        });
    }

    [Fact]
    public async Task Sales_keeps_customer_rights_only()
    {
        var codes = await RoleCodesAsync(RoleCodes.Sales);
        codes.Should().Contain(Permissions.Customers.Create);
        codes.Should().NotContain(Permissions.Suppliers.Create);
    }
}
