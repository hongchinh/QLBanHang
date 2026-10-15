using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Catalog;

/// Product quick add from the product code cell (brainstorm 261015-2033, D2): which seed roles may create products.
[Collection(nameof(PostgresCollection))]
public class ProductQuickAddSeedTests : CashDebtTestBase
{
    public ProductQuickAddSeedTests(PostgresFixture pg) : base(pg) { }

    private Task<HashSet<string>> RoleCodesAsync(string roleCode) => InDbAsync(async db =>
        (await db.Roles.AsNoTracking()
            .Where(r => r.Code == roleCode)
            .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code))
            .ToListAsync()).ToHashSet());

    [Theory]
    [InlineData(RoleCodes.Warehouse)]
    [InlineData(RoleCodes.Accountant)]
    [InlineData(RoleCodes.Sales)]
    public async Task Role_can_quick_add_products(string roleCode)
    {
        var codes = await RoleCodesAsync(roleCode);
        codes.Should().Contain(new[] { Permissions.Products.View, Permissions.Products.Create });
    }

    [Theory]
    [InlineData(RoleCodes.Warehouse)]
    [InlineData(RoleCodes.Accountant)]
    [InlineData(RoleCodes.Sales)]
    public async Task Role_does_not_get_product_update_or_delete(string roleCode)
    {
        var codes = await RoleCodesAsync(roleCode);
        codes.Should().NotContain(Permissions.Products.Update);
        codes.Should().NotContain(Permissions.Products.Delete);
    }
}
