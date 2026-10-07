using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Infrastructure.Persistence.Seed;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class CashDebtSeedTests : CashDebtTestBase
{
    public CashDebtSeedTests(PostgresFixture pg) : base(pg) { }

    private static readonly string[] CashModuleCodes =
    {
        Permissions.Receipts.View, Permissions.Receipts.Create, Permissions.Receipts.Edit,
        Permissions.Receipts.Delete, Permissions.Receipts.Cancel, Permissions.Receipts.EditAll,
        Permissions.Payments.View, Permissions.Payments.Create, Permissions.Payments.Edit,
        Permissions.Payments.Delete, Permissions.Payments.Cancel, Permissions.Payments.EditAll,
        Permissions.FundTransfers.View, Permissions.FundTransfers.Create, Permissions.FundTransfers.Edit,
        Permissions.FundTransfers.Delete, Permissions.FundTransfers.Cancel, Permissions.FundTransfers.EditAll,
        Permissions.DebtOffsets.View, Permissions.DebtOffsets.Create, Permissions.DebtOffsets.Cancel,
        Permissions.DebtOffsets.Delete,
        Permissions.Debt.Matching, Permissions.Debt.OpeningBalance, Permissions.Debt.PartnerSummary,
        Permissions.Cash.OpeningBalance, Permissions.Cash.ManageCatalogs, Permissions.Cash.Settings,
        Permissions.CreditLimits.Manage,
    };

    private static readonly string[] AllNewCodes = CashModuleCodes.Append(Permissions.Reports.Cash).ToArray();

    private Task<HashSet<string>> RoleCodesAsync(string roleCode) => InDbAsync(async db =>
        (await db.Roles.AsNoTracking()
            .Where(r => r.Code == roleCode)
            .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code))
            .ToListAsync()).ToHashSet());

    [Fact]
    public async Task New_permission_codes_exist_with_modules()
    {
        AllNewCodes.Should().HaveCount(30);
        var rows = await InDbAsync(db => db.Permissions.AsNoTracking()
            .Where(p => AllNewCodes.Contains(p.Code)).ToDictionaryAsync(p => p.Code, p => p.Module));

        rows.Keys.Should().BeEquivalentTo(AllNewCodes);
        rows[Permissions.Reports.Cash].Should().Be(Permissions.ReportModule);
        foreach (var code in CashModuleCodes)
            rows[code].Should().Be(Permissions.CashModule, code);
    }

    [Fact]
    public async Task Default_role_grants_follow_p5()
    {
        (await RoleCodesAsync(RoleCodes.Accountant)).Should().Contain(AllNewCodes);
        (await RoleCodesAsync(RoleCodes.Manager)).Should().Contain(AllNewCodes);
        (await RoleCodesAsync(RoleCodes.Admin)).Should().Contain(AllNewCodes);

        var sales = await RoleCodesAsync(RoleCodes.Sales);
        sales.Should().Contain(Permissions.Debt.PartnerSummary);
        sales.Should().NotContain(Permissions.Receipts.View);

        (await RoleCodesAsync(RoleCodes.Warehouse)).Should().NotIntersectWith(AllNewCodes);
    }

    [Fact]
    public async Task Removed_grant_survives_a_reseed()
    {
        var removed = await InDbAsync(async db =>
        {
            var rp = await db.RolePermissions
                .Where(x => x.Role.Code == RoleCodes.Accountant && x.Permission.Code == Permissions.Receipts.View)
                .ToListAsync();
            db.RolePermissions.RemoveRange(rp);
            await db.SaveChangesAsync();
            return rp.Count;
        });
        try
        {
            removed.Should().Be(1);
            await DbSeeder.SeedAsync(_factory.Services);
            (await RoleCodesAsync(RoleCodes.Accountant)).Should().NotContain(Permissions.Receipts.View);
        }
        finally
        {
            await InDbAsync(async db =>
            {
                var roleId = await db.Roles.Where(r => r.Code == RoleCodes.Accountant).Select(r => r.Id).SingleAsync();
                var permId = await db.Permissions.Where(p => p.Code == Permissions.Receipts.View).Select(p => p.Id).SingleAsync();
                if (!await db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permId))
                {
                    db.RolePermissions.Add(new OrderMgmt.Domain.Entities.Identity.RolePermission { RoleId = roleId, PermissionId = permId });
                    await db.SaveChangesAsync();
                }
            });
        }
    }
}
