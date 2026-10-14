using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class BranchSampleDataFlagsTests : CashDebtTestBase
{
    public BranchSampleDataFlagsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task New_branch_has_no_sample_data_flags()
    {
        var response = await _client.PostAsJsonAsync("/api/branches", new CreateBranchRequest { Code = "SDF1", Name = "CN SDF1" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadDataAsync<BranchDto>(response);
        AssertNoFlags(created);

        var list = await ReadDataAsync<List<BranchDto>>(await _client.GetAsync("/api/branches"));
        AssertNoFlags(list.Single(b => b.Id == created.Id));
    }

    [Fact]
    public async Task Product_and_partner_default_to_not_sample()
    {
        var productId = await CreateInventoryProductAsync("SDP" + Guid.NewGuid().ToString("N")[..6]);
        var partnerId = await CreatePartnerAsync("SDC" + Guid.NewGuid().ToString("N")[..6], true, false);

        var (productSample, partnerSample) = await InDbAsync(async db => (
            await db.Products.Where(p => p.Id == productId).Select(p => p.IsSample).SingleAsync(),
            await db.Customers.Where(c => c.Id == partnerId).Select(c => c.IsSample).SingleAsync()));

        productSample.Should().BeFalse();
        partnerSample.Should().BeFalse();
    }

    private static void AssertNoFlags(BranchDto b)
    {
        b.IsDemo.Should().BeFalse();
        b.SampleDataStatus.Should().Be(SampleDataStatus.None);
        b.SampleDataProgress.Should().Be(0);
        b.SampleDataError.Should().BeNull();
    }
}
