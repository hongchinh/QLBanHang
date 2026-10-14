using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class DemoBranchPreviewTests : DemoBranchFixture
{
    public DemoBranchPreviewTests(PostgresFixture pg) : base(pg) { }

    private static string PreviewUrl(Guid branchId) => $"/api/branches/{branchId}/demo-deletion";

    [Fact]
    public async Task Preview_counts_documents_and_users()
    {
        var (branchId, client) = await CreateDemoBranchAsync("DP01");
        var customer = await CreatePartnerAsync("DPKH01", isCustomer: true, isSupplier: false);
        var sale = await PostCreditSaleAsync(branchId, client, customer, 3_000m);
        await PostAllocatedReceiptAsync(branchId, client, customer, sale.Id, 1_000m);
        await PostFundTransferAsync(branchId, client, "DPNH", 500m);
        await CreateTestUserAsync("dp_user", "Pass@123", RoleCodes.Sales, defaultBranchId: branchId);

        var response = await _client.GetAsync(PreviewUrl(branchId));

        var preview = await ReadDataAsync<DemoBranchDeletionPreviewDto>(response);
        preview.DocumentCount.Should().Be(3);
        preview.ReassignedUserCount.Should().Be(1);
    }

    [Fact]
    public async Task Preview_refuses_a_normal_branch()
    {
        var branchId = await CreateBranchAsync("DP02");

        var response = await _client.GetAsync(PreviewUrl(branchId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorAsync(response)).Code.Should().Be("NOT_A_DEMO_BRANCH");
    }

    [Fact]
    public async Task Preview_refuses_a_running_branch()
    {
        var (branchId, _) = await CreateDemoBranchAsync("DP03", SampleDataStatus.Running);

        var response = await _client.GetAsync(PreviewUrl(branchId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorAsync(response)).Code.Should().Be("SAMPLE_DATA_RUNNING");
    }

    [Fact]
    public async Task Preview_of_unknown_branch_is_404()
    {
        var response = await _client.GetAsync(PreviewUrl(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_needs_branches_manage()
    {
        var (branchId, _) = await CreateDemoBranchAsync("DP04");
        var client = await CreateClientWithPermissionsAsync("dp_noperm", null, Permissions.Reports.Debt);

        var response = await client.GetAsync(PreviewUrl(branchId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options);
        return body!.Error!;
    }
}
