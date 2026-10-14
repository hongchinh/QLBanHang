using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Dashboards.Executive.Services;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// ExecutiveLookups: the names the metrics services print, by id. A row that was soft-deleted keeps its name, and an id
/// that is not found is simply absent from the answer.
[Collection(nameof(PostgresCollection))]
public class ExecutiveLookupsTests : ExecutiveDashboardTestBase
{
    public ExecutiveLookupsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Names_come_back_by_id_and_unknown_ids_are_absent()
    {
        var rows = await ArrangeAsync();

        var names = await LookUpAsync(rows);

        names.BranchCodes.Should().BeEquivalentTo(new Dictionary<Guid, string> { [rows.BranchId] = rows.BranchCode });
        names.Branches.Should().BeEquivalentTo(
            new Dictionary<Guid, (string Code, string Name)> { [rows.BranchId] = (rows.BranchCode, rows.BranchName) });
        names.Partners.Should().BeEquivalentTo(
            new Dictionary<Guid, (string Code, string Name)> { [rows.PartnerId] = (rows.PartnerCode, rows.PartnerName) });
        names.Products.Should().BeEquivalentTo(
            new Dictionary<Guid, (string Code, string Name)> { [rows.ProductId] = (rows.ProductCode, rows.ProductName) });
        names.Users.Should().BeEquivalentTo(new Dictionary<Guid, string> { [rows.UserId] = rows.UserName });
    }

    [Fact]
    public async Task Soft_deleted_rows_keep_their_names()
    {
        var rows = await ArrangeAsync();
        await InDbAsync(async db =>
        {
            await db.Branches.IgnoreQueryFilters().Where(b => b.Id == rows.BranchId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsDeleted, true));
            await db.Customers.IgnoreQueryFilters().Where(c => c.Id == rows.PartnerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true));
            await db.Products.IgnoreQueryFilters().Where(p => p.Id == rows.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true));
            await db.Users.IgnoreQueryFilters().Where(u => u.Id == rows.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDeleted, true));
        });

        var names = await LookUpAsync(rows);

        names.BranchCodes.Should().BeEquivalentTo(new Dictionary<Guid, string> { [rows.BranchId] = rows.BranchCode });
        names.Branches.Should().BeEquivalentTo(
            new Dictionary<Guid, (string Code, string Name)> { [rows.BranchId] = (rows.BranchCode, rows.BranchName) });
        names.Partners.Should().BeEquivalentTo(
            new Dictionary<Guid, (string Code, string Name)> { [rows.PartnerId] = (rows.PartnerCode, rows.PartnerName) });
        names.Products.Should().BeEquivalentTo(
            new Dictionary<Guid, (string Code, string Name)> { [rows.ProductId] = (rows.ProductCode, rows.ProductName) });
        names.Users.Should().BeEquivalentTo(new Dictionary<Guid, string> { [rows.UserId] = rows.UserName });
    }

    private sealed record Rows(Guid BranchId, string BranchCode, string BranchName, Guid PartnerId, string PartnerCode,
        string PartnerName, Guid ProductId, string ProductCode, string ProductName, Guid UserId, string UserName);

    private sealed record Names(Dictionary<Guid, string> BranchCodes, Dictionary<Guid, (string Code, string Name)> Branches,
        Dictionary<Guid, (string Code, string Name)> Partners, Dictionary<Guid, (string Code, string Name)> Products,
        Dictionary<Guid, string> Users);

    /// One branch, partner, product and user (not the admin, who is never deleted) of this test.
    private async Task<Rows> ArrangeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var branchCode = "LK" + suffix;
        var partnerCode = "LKP" + suffix;
        var productCode = "LKS" + suffix;
        var username = "lk_" + suffix;
        var branchId = await CreateBranchAsync(branchCode);
        var partnerId = await CreatePartnerAsync(partnerCode, isCustomer: true, isSupplier: false);
        var productId = await CreateInventoryProductAsync(productCode);
        await CreateClientWithPermissionsAsync(username, MainBranchId, Permissions.Dashboard.Executive);
        var user = await InDbAsync(db => db.Users.Where(u => u.Username == username)
            .Select(u => new { u.Id, u.FullName }).SingleAsync());
        return new Rows(branchId, branchCode, $"Chi nhánh {branchCode}", partnerId, partnerCode,
            $"Đối tượng {partnerCode}", productId, productCode, $"Hàng {productCode}", user.Id, user.FullName);
    }

    /// Asks each lookup for the four ids of the rows and an id that exists nowhere.
    private async Task<Names> LookUpAsync(Rows rows)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var lookups = scope.ServiceProvider.GetRequiredService<ExecutiveLookups>();
        var unknown = Guid.NewGuid();
        var ids = new[] { rows.BranchId, rows.PartnerId, rows.ProductId, rows.UserId, unknown };
        return new Names(
            await lookups.BranchCodesAsync(ids, CancellationToken.None),
            await lookups.BranchesAsync(ids, CancellationToken.None),
            await lookups.PartnersAsync(ids, CancellationToken.None),
            await lookups.ProductsAsync(ids, CancellationToken.None),
            await lookups.UserNamesAsync(ids, CancellationToken.None));
    }
}
