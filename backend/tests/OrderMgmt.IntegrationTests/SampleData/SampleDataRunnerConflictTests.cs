using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Own class so the cloned database has no sample rows yet when the real row is inserted.
[Collection(nameof(PostgresCollection))]
public class SampleDataRunnerConflictTests : CashDebtTestBase
{
    public SampleDataRunnerConflictTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Customer_code_taken_by_real_data_fails_the_job()
    {
        await InDbAsync(async db =>
        {
            db.Customers.Add(new Customer { Code = "DM-KH01", Name = "Khách thật", IsSample = false, Status = CustomerStatus.Active });
            await db.SaveChangesAsync();
        });
        var branchId = await CreateBranchAsync("RS06");

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeFalse();
        var branch = await InDbAsync(db => db.Branches.AsNoTracking().SingleAsync(b => b.Id == branchId));
        branch.SampleDataStatus.Should().Be(SampleDataStatus.Failed);
        branch.SampleDataError.Should().StartWith("CATALOG: Mã DM-KH01 đã được dùng cho dữ liệu thật.");
    }
}
