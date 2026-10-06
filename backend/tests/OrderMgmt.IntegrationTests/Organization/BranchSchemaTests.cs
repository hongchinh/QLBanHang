using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;
using Xunit;

namespace OrderMgmt.IntegrationTests.Organization;

[Collection(nameof(PostgresCollection))]
public class BranchSchemaTests : QuotationTestBase
{
    public BranchSchemaTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Main_branch_is_seeded_and_is_every_users_default()
    {
        await CreateTestUserAsync("branch_sales", "Pass@123", RoleCodes.Sales);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var main = await db.Branches.SingleAsync(b => b.Id == BranchDefaults.MainBranchId);
        main.Code.Should().Be("CN01");
        main.Name.Should().Be("Chi nhánh chính");

        (await db.Users.SingleAsync(u => u.Username == "admin")).DefaultBranchId
            .Should().Be(BranchDefaults.MainBranchId);
        (await db.Users.SingleAsync(u => u.Username == "branch_sales")).DefaultBranchId
            .Should().Be(BranchDefaults.MainBranchId);
    }
}
