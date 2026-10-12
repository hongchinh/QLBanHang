using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.FundTransfers;

/// List, defaults, activities, and the branch in-use check of fund transfers.
[Collection(nameof(PostgresCollection))]
public class FundTransferQueryTests : CashDebtTestBase
{
    private const string AdminName = "Quản trị hệ thống";

    private Guid _branchId;
    private HttpClient _branchClient = default!;
    private Guid _qtmId;
    private Guid _nh01Id;

    public FundTransferQueryTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch opened on 10-01: its funds have no other movements and CQ codes start from scratch.
        _branchId = await CreateBranchAsync(NewCode("FTQ"));
        await SetBranchOpeningDateAsync(_branchId, new DateOnly(2026, 10, 1));
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
        _qtmId = await CashFundIdAsync(_branchId);
        _nh01Id = await CreateBankFundAsync("NH01");
        // Enough money everywhere that no transfer asks for the negative-fund confirmation.
        await SetFundOpeningAmountAsync(_qtmId, 1_000_000m);
        await SetFundOpeningAmountAsync(_nh01Id, 1_000_000m);
    }

    [Fact]
    public async Task List_filters_by_fund_status_and_date()
    {
        var nh02 = await CreateBankFundAsync("NH02");
        await SetFundOpeningAmountAsync(nh02, 1_000_000m);

        var t1Request = TransferRequest(_qtmId, _nh01Id, 1_000m, "2026-10-02 09:00");
        t1Request.Description = "Nộp tiền alpha";
        var t1 = await CreateTransferAsync(t1Request);
        // 23:30 VN on 10-04 is still 10-04; 00:30 VN on 10-05 is 10-04 in UTC but must fall on 10-05.
        var t2 = await CreateTransferAsync(TransferRequest(_nh01Id, nh02, 2_000m, "2026-10-04 23:30"));
        var t3 = await CreateTransferAsync(TransferRequest(nh02, _qtmId, 500m, "2026-10-05 00:30"));
        var t4 = await CreateTransferAsync(TransferRequest(_qtmId, nh02, 400m, "2026-10-03 10:00"));
        var (cancelStatus, _, cancelError) = await CancelTransferAsync(_branchClient, t4.Id,
            new CashDocumentActionRequest { Version = t4.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

        // Default: Active transfers of the working branch, VoucherAt desc, TotalAmount over the filtered set.
        var list = await ListAsync(_branchClient, "");
        list.Items.Select(i => i.Id).Should().Equal(t3.Id, t2.Id, t1.Id);
        list.TotalItems.Should().Be(3);
        list.TotalAmount.Should().Be(3_500m);
        list.Items.Single(i => i.Id == t1.Id).Should().BeEquivalentTo(new
        {
            Code = t1.Code, VoucherAt = t1.VoucherAt, FromFundCode = "QTM", FromFundName = "Quỹ tiền mặt",
            ToFundCode = "NH01", ToFundName = "Quỹ NH01", Amount = 1_000m, Description = "Nộp tiền alpha",
            Status = DocumentStatus.Active, OwnerUserId = t1.OwnerUserId, OwnerName = AdminName,
        });

        // The total covers the filtered set, not the page.
        var page = await ListAsync(_branchClient, "page=1&pageSize=2");
        page.Items.Select(i => i.Id).Should().Equal(t3.Id, t2.Id);
        page.TotalItems.Should().Be(3);
        page.TotalAmount.Should().Be(3_500m);

        var all = await ListAsync(_branchClient, "status=All");
        all.Items.Select(i => i.Id).Should().Equal(t3.Id, t2.Id, t4.Id, t1.Id);
        all.TotalAmount.Should().Be(3_900m);
        (await ListAsync(_branchClient, "status=Cancelled")).Items.Select(i => i.Id).Should().Equal(t4.Id);

        // A fund filter matches either side of the transfer.
        (await ListAsync(_branchClient, $"fundId={_nh01Id}")).Items.Select(i => i.Id).Should().Equal(t2.Id, t1.Id);
        (await ListAsync(_branchClient, $"fundId={nh02}")).Items.Select(i => i.Id).Should().Equal(t3.Id, t2.Id);
        var qtm = await ListAsync(_branchClient, $"fundId={_qtmId}&status=All");
        qtm.Items.Select(i => i.Id).Should().Equal(t3.Id, t4.Id, t1.Id);
        qtm.TotalAmount.Should().Be(1_900m);

        // VN dates.
        (await ListAsync(_branchClient, "from=2026-10-03&to=2026-10-04")).Items.Select(i => i.Id).Should().Equal(t2.Id);
        (await ListAsync(_branchClient, "from=2026-10-05")).Items.Select(i => i.Id).Should().Equal(t3.Id);

        // Search (ILIKE): code and description; % and _ match literally.
        (await ListAsync(_branchClient, $"search={t2.Code}")).Items.Select(i => i.Id).Should().Equal(t2.Id);
        (await ListAsync(_branchClient, "search=ALPHA")).Items.Select(i => i.Id).Should().Equal(t1.Id);
        (await ListAsync(_branchClient, $"search={Uri.EscapeDataString("CQ_")}")).Items.Should().BeEmpty();
        (await ListAsync(_branchClient, $"search={Uri.EscapeDataString("%")}")).Items.Should().BeEmpty();

        // Owners.
        (await ListAsync(_branchClient, $"ownerIds={t1.OwnerUserId},{Guid.NewGuid()}")).TotalItems.Should().Be(3);
        (await ListAsync(_branchClient, $"ownerIds={Guid.NewGuid()}")).Items.Should().BeEmpty();
        (await _branchClient.GetAsync("/api/fund-transfers?ownerIds=abc")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        // Another branch sees none of them.
        var branchB = await CreateBranchAsync(NewCode("FTQ"));
        var clientB = CloneAdminClient();
        UseBranch(clientB, branchB);
        var listB = await ListAsync(clientB, "status=All");
        listB.Items.Should().BeEmpty();
        listB.TotalAmount.Should().Be(0m);

        // The view permission.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var viewer = await CreateClientWithPermissionsAsync("ftq_view_" + suffix, _branchId,
            Permissions.FundTransfers.View);
        UseBranch(viewer, _branchId);
        (await viewer.GetAsync("/api/fund-transfers")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var outsider = await CreateClientWithPermissionsAsync("ftq_none_" + suffix, _branchId,
            Permissions.Receipts.View);
        UseBranch(outsider, _branchId);
        (await outsider.GetAsync("/api/fund-transfers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Defaults_and_activities()
    {
        var defaults = await DefaultsAsync(_branchClient);
        defaults.NextCode.Should().Be("CQ00001");
        defaults.VoucherAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        // A peek: the counter is not consumed.
        (await DefaultsAsync(_branchClient)).NextCode.Should().Be("CQ00001");

        var transfer = await CreateTransferAsync(TransferRequest(_qtmId, _nh01Id, 300m, "2026-10-05 09:00"));
        transfer.Code.Should().Be("CQ00001");
        (await DefaultsAsync(_branchClient)).NextCode.Should().Be("CQ00002");

        var update = TransferRequest(_qtmId, _nh01Id, 350m, "2026-10-05 09:00");
        update.Version = transfer.Version;
        var (updateStatus, updated, updateError) = await PutTransferAsync(_branchClient, transfer.Id, update);
        updateStatus.Should().Be(HttpStatusCode.OK, updateError?.Message);
        var (cancelStatus, _, cancelError) = await CancelTransferAsync(_branchClient, transfer.Id,
            new CashDocumentActionRequest { Version = updated!.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);

        // Activities, newest first.
        var activities = await ReadDataAsync<List<CashDocumentActivityDto>>(
            await _branchClient.GetAsync($"/api/fund-transfers/{transfer.Id}/activities"));
        activities.Select(a => a.Action).Should().Equal(CashDocumentActivityAction.Cancelled,
            CashDocumentActivityAction.Updated, CashDocumentActivityAction.Created);
        activities.Select(a => a.OccurredAt).Should().BeInDescendingOrder();
        activities.Should().AllSatisfy(a => a.ActorName.Should().Be(AdminName));

        // Another branch cannot read them; an unknown transfer is a 404.
        var clientB = CloneAdminClient();
        UseBranch(clientB, await CreateBranchAsync(NewCode("FTQ")));
        (await clientB.GetAsync($"/api/fund-transfers/{transfer.Id}/activities")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await _branchClient.GetAsync($"/api/fund-transfers/{Guid.NewGuid()}/activities")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        // Both GETs need the view permission.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var outsider = await CreateClientWithPermissionsAsync("ftq_def_" + suffix, _branchId,
            Permissions.Receipts.View);
        UseBranch(outsider, _branchId);
        (await outsider.GetAsync("/api/fund-transfers/defaults")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsider.GetAsync($"/api/fund-transfers/{transfer.Id}/activities")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Branch_with_transfer_has_documents()
    {
        var transfer = await CreateTransferAsync(TransferRequest(_qtmId, _nh01Id, 300m, "2026-10-05 09:00"));

        await AssertOpeningDateBlockedAsync();

        // A cancelled transfer still counts.
        var (cancelStatus, cancelled, cancelError) = await CancelTransferAsync(_branchClient, transfer.Id,
            new CashDocumentActionRequest { Version = transfer.Version });
        cancelStatus.Should().Be(HttpStatusCode.OK, cancelError?.Message);
        await AssertOpeningDateBlockedAsync();

        // A deleted one no longer does.
        var (deleteStatus, _, deleteError) = await DeleteTransferAsync(_branchClient, transfer.Id,
            new CashDocumentActionRequest { Version = cancelled!.Version });
        deleteStatus.Should().Be(HttpStatusCode.OK, deleteError?.Message);
        var ok = await PutOpeningDateAsync(new DateOnly(2026, 10, 2));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private async Task AssertOpeningDateBlockedAsync()
    {
        var blocked = await PutOpeningDateAsync(new DateOnly(2026, 10, 2));
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code
            .Should().Be("BRANCH_HAS_DOCUMENTS");
    }

    private Task<HttpResponseMessage> PutOpeningDateAsync(DateOnly date) =>
        _client.PutAsJsonAsync($"/api/branches/{_branchId}/opening-date", new SetOpeningDateRequest { OpeningDate = date });

    private async Task<FundTransferDto> CreateTransferAsync(UpsertFundTransferRequest request)
    {
        var (status, transfer, error) = await PostTransferAsync(_branchClient, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return transfer!;
    }

    private static async Task<FundTransferListResult> ListAsync(HttpClient client, string query) =>
        await ReadDataAsync<FundTransferListResult>(await client.GetAsync($"/api/fund-transfers?{query}"));

    private static async Task<FundTransferDefaultsDto> DefaultsAsync(HttpClient client) =>
        await ReadDataAsync<FundTransferDefaultsDto>(await client.GetAsync("/api/fund-transfers/defaults"));

    private async Task<Guid> CreateBankFundAsync(string code) =>
        (await ReadDataAsync<FundDto>(await _branchClient.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = code, Name = "Quỹ " + code, Kind = FundKind.Bank, IsActive = true,
        }))).Id;

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
