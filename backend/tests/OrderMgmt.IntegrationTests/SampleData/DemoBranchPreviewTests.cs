using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
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
        var branch = await CreateDemoBranchAsync("DP01");
        var customer = await CreatePartnerAsync("DPKH01", isCustomer: true, isSupplier: false);
        var product = await CreateInventoryProductAsync("DPSP01", track: false);
        var sale = await PostCreditSaleAsync(branch, customer, product, 3_000m);
        await PostAllocatedReceiptAsync(branch, customer, sale.Id, 1_000m);
        var bank = await CreateBankFundAsync("DPNH", branch.BranchId, opening: 1_000m);
        await PostFundTransferAsync(branch, bank, 500m);
        // Soft-deleted rows go with the branch too, so they count.
        var deleted = await PostFundTransferAsync(branch, bank, 200m);
        var (deleteStatus, _, deleteError) = await DeleteTransferAsync(branch.Client, deleted.Id,
            new CashDocumentActionRequest { Version = deleted.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        await CreateTestUserAsync("dp_user", "Pass@123", RoleCodes.Sales, defaultBranchId: branch.BranchId);
        await CreateTestUserAsync("dp_gone", "Pass@123", RoleCodes.Sales, defaultBranchId: branch.BranchId);
        await InDbAsync(db => db.Users.Where(u => u.Username == "dp_gone")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDeleted, true)));

        var response = await _client.GetAsync(PreviewUrl(branch.BranchId));

        var preview = await ReadDataAsync<DemoBranchDeletionPreviewDto>(response);
        preview.DocumentCount.Should().Be(4);
        preview.ReassignedUserCount.Should().Be(2);
    }

    [Fact]
    public async Task Preview_refuses_a_normal_branch()
    {
        var branchId = await CreateBranchAsync("DP02");

        var response = await _client.GetAsync(PreviewUrl(branchId));

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "NOT_A_DEMO_BRANCH");
    }

    [Fact]
    public async Task Preview_refuses_a_running_branch()
    {
        var branch = await CreateDemoBranchAsync("DP03", SampleDataStatus.Running);

        var response = await _client.GetAsync(PreviewUrl(branch.BranchId));

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "SAMPLE_DATA_RUNNING");
    }

    [Fact]
    public async Task Preview_of_unknown_branch_is_404()
    {
        var response = await _client.GetAsync(PreviewUrl(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_of_soft_deleted_branch_is_404()
    {
        var branch = await CreateDemoBranchAsync("DP05");
        await InDbAsync(db => db.Branches.Where(b => b.Id == branch.BranchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsDeleted, true)));

        var response = await _client.GetAsync(PreviewUrl(branch.BranchId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_needs_branches_manage()
    {
        var branch = await CreateDemoBranchAsync("DP04");
        var client = await CreateClientWithPermissionsAsync("dp_noperm", null, Permissions.Reports.Debt);

        var response = await client.GetAsync(PreviewUrl(branch.BranchId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
