using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Real data that already uses a sample code. Every test gets its own cloned database (xUnit creates a new instance,
/// hence a new factory, per test), so the real rows are inserted before any sample row exists.
[Collection(nameof(PostgresCollection))]
public class SampleDataRunnerConflictTests : CashDebtTestBase
{
    public SampleDataRunnerConflictTests(PostgresFixture pg) : base(pg) { }

    [Theory]
    [InlineData("DM-SP01")]
    [InlineData("DM-KH01")]
    public async Task Code_taken_by_real_data_fails_the_job(string code)
    {
        await InsertRealRowAsync(code, deleted: false);
        var branchId = await CreateBranchAsync("RS06");

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeFalse();
        var branch = await InDbAsync(db => db.Branches.AsNoTracking().SingleAsync(b => b.Id == branchId));
        branch.SampleDataStatus.Should().Be(SampleDataStatus.Failed);
        branch.SampleDataError.Should().StartWith($"CATALOG: Mã {code} đã được dùng cho dữ liệu thật.");
        result.Error.Should().Be(branch.SampleDataError);
    }

    [Fact]
    public async Task Soft_deleted_real_row_leaves_the_code_free()
    {
        await InsertRealRowAsync("DM-KH01", deleted: true);
        var branchId = await CreateBranchAsync("RS10");

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeTrue(result.Error);
        var rows = await InDbAsync(db => db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Code == "DM-KH01").ToListAsync());
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(c => !c.IsDeleted).Which.IsSample.Should().BeTrue();
        rows.Should().ContainSingle(c => c.IsDeleted).Which.IsSample.Should().BeFalse("the real row stays deleted");
    }

    [Fact]
    public async Task Group_code_taken_by_real_data_fails_the_job()
    {
        await InDbAsync(async db =>
        {
            db.ProductGroups.Add(new ProductGroup { Code = "DM", Name = "Dây mềm", SortOrder = 5, IsActive = true });
            await db.SaveChangesAsync();
        });
        var branchId = await CreateBranchAsync("RS11");

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("CATALOG: Mã nhóm DM đã được dùng cho dữ liệu thật.");
        (await InDbAsync(db => db.ProductGroups.SingleAsync(g => g.Code == "DM"))).Name.Should().Be("Dây mềm");
        (await InDbAsync(db => db.Products.IgnoreQueryFilters().AnyAsync(p => p.Code.StartsWith("DM-")))).Should().BeFalse();
    }

    /// A row with IsSample = false: a product for "DM-SP…" codes, a partner otherwise.
    private Task InsertRealRowAsync(string code, bool deleted) => InDbAsync(async db =>
    {
        if (code.StartsWith("DM-SP"))
            db.Products.Add(new Product
            {
                Code = code,
                Name = "Hàng thật",
                IsSample = false,
                Status = ProductStatus.Active,
                IsDeleted = deleted,
                DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
            });
        else
            db.Customers.Add(new Customer
            {
                Code = code,
                Name = "Khách thật",
                IsSample = false,
                Status = CustomerStatus.Active,
                IsDeleted = deleted,
                DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
            });
        await db.SaveChangesAsync();
    });
}
