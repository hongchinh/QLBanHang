using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
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
        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        data.GetProperty("sampleDataStatus").GetString().Should().Be("None");
        data.GetProperty("sampleDataError").ValueKind.Should().Be(JsonValueKind.Null);
        var created = await ReadDataAsync<BranchDto>(response);
        AssertNoFlags(created);

        var list = await ReadDataAsync<List<BranchDto>>(await _client.GetAsync("/api/branches"));
        AssertNoFlags(list.Single(b => b.Id == created.Id));
    }

    [Fact]
    public async Task Create_with_sample_data_marks_branch_demo_and_running()
    {
        var response = await _client.PostAsJsonAsync("/api/branches", new { code = "SDF2", name = "CN demo", withSampleData = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        data.GetProperty("sampleDataStatus").GetString().Should().Be("Running");
        var created = await ReadDataAsync<BranchDto>(response);
        // Assert on the response only: a background worker may already have moved the row on.
        created.IsDemo.Should().BeTrue();
        created.SampleDataStatus.Should().Be(SampleDataStatus.Running);
        created.SampleDataProgress.Should().Be(0);

        var isDemo = await InDbAsync(db => db.Branches.Where(b => b.Id == created.Id).Select(b => b.IsDemo).SingleAsync());
        isDemo.Should().BeTrue();
    }

    [Fact]
    public async Task Create_without_sample_data_is_unchanged()
    {
        var response = await _client.PostAsJsonAsync("/api/branches", new { code = "SDF3", name = "CN thường", withSampleData = false });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        data.GetProperty("sampleDataStatus").GetString().Should().Be("None");
        var created = await ReadDataAsync<BranchDto>(response);
        AssertNoFlags(created);

        var (numberings, hasFund) = await InDbAsync(async db => (
            await db.DocumentNumberings.CountAsync(n => n.BranchId == created.Id),
            await db.Funds.AnyAsync(f => f.BranchId == created.Id && f.Code == "QTM")));
        numberings.Should().Be(DocumentNumberingDefaults.AllTypes.Count());
        hasFund.Should().BeTrue();
    }

    [Fact]
    public async Task Product_and_partner_default_to_not_sample()
    {
        var productId = await CreateInventoryProductAsync("SDP" + Guid.NewGuid().ToString("N")[..6]);
        var partnerId = await CreatePartnerAsync("SDC" + Guid.NewGuid().ToString("N")[..6], isCustomer: true, isSupplier: false);

        var (productSample, partnerSample) = await InDbAsync(async db => (
            await db.Products.Where(p => p.Id == productId).Select(p => p.IsSample).SingleAsync(),
            await db.Customers.Where(c => c.Id == partnerId).Select(c => c.IsSample).SingleAsync()));

        productSample.Should().BeFalse();
        partnerSample.Should().BeFalse();
    }

    [Fact]
    public async Task Sample_data_services_are_registered()
    {
        await using var scope1 = _factory.Services.CreateAsyncScope();
        await using var scope2 = _factory.Services.CreateAsyncScope();

        var queue1 = scope1.ServiceProvider.GetRequiredService<ISampleDataQueue>();
        scope1.ServiceProvider.GetRequiredService<ISampleDataGenerator>();
        scope1.ServiceProvider.GetRequiredService<ISampleDataRunner>();
        scope1.ServiceProvider.GetRequiredService<IDemoBranchService>();

        scope2.ServiceProvider.GetRequiredService<ISampleDataQueue>().Should().BeSameAs(queue1);
    }

    private static void AssertNoFlags(BranchDto b)
    {
        b.IsDemo.Should().BeFalse();
        b.SampleDataStatus.Should().Be(SampleDataStatus.None);
        b.SampleDataProgress.Should().Be(0);
        b.SampleDataError.Should().BeNull();
    }
}
