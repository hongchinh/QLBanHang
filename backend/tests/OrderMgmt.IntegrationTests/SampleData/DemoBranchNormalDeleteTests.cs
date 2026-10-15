using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class DemoBranchNormalDeleteTests : DemoBranchFixture
{
    public DemoBranchNormalDeleteTests(PostgresFixture pg) : base(pg) { }

    [Theory]
    [InlineData(SampleDataStatus.Ready)]
    [InlineData(SampleDataStatus.Running)]
    public async Task Normal_delete_refuses_a_demo_branch_and_points_to_the_demo_delete(SampleDataStatus status)
    {
        // No warehouse and no documents yet: every other delete check passes, so only the demo guard can refuse.
        var branchId = await CreateBranchAsync("ND" + (int)status);
        await InDbAsync(db => db.Branches.Where(b => b.Id == branchId).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.IsDemo, true)
            .SetProperty(b => b.SampleDataStatus, status)));

        var response = await _client.DeleteAsync($"/api/branches/{branchId}");

        var (actual, _, error) = await ReadDocumentResponseAsync<object>(response);
        actual.Should().Be(HttpStatusCode.Conflict, error?.Message);
        error!.Code.Should().Be("DEMO_BRANCH_USE_DEMO_DELETE");
        error.Message.Should().Be("Chi nhánh demo phải xóa bằng chức năng Xóa chi nhánh demo.");
        (await InDbAsync(db => db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.IsDeleted).SingleAsync()))
            .Should().BeFalse();
    }
}
