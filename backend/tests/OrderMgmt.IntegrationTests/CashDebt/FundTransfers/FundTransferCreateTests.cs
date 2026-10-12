using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.FundTransfers;

[Collection(nameof(PostgresCollection))]
public class FundTransferCreateTests : CashDebtTestBase
{
    private Guid _branchId;
    private HttpClient _branchClient = default!;
    private Guid _qtmId;
    private Guid _nh01Id;

    public FundTransferCreateTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // A fresh branch: its funds have no other movements and CQ codes start from scratch.
        _branchId = await CreateBranchAsync("FT" + Guid.NewGuid().ToString("N")[..8]);
        _branchClient = CloneAdminClient();
        UseBranch(_branchClient, _branchId);
        _qtmId = await CashFundIdAsync(_branchId);
        _nh01Id = await CreateBankFundAsync("NH01");
    }

    [Fact]
    public async Task Schema_rejects_same_fund_and_duplicate_code()
    {
        var at = Vn("2026-10-05 09:00");

        var sameFund = async () => await InsertFundTransferAsync(_branchId, _qtmId, _qtmId, 300m, at);
        (await sameFund.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.ConstraintName.Should().Be("ck_fund_transfers_distinct_funds");

        await InsertFundTransferAsync(_branchId, _qtmId, _nh01Id, 300m, at, code: "CQ00001");
        var duplicate = async () => await InsertFundTransferAsync(_branchId, _nh01Id, _qtmId, 100m, at, code: "CQ00001");
        (await duplicate.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        // The same code in another branch is allowed.
        var otherBranch = await CreateBranchAsync("FT" + Guid.NewGuid().ToString("N")[..8]);
        await InsertFundTransferAsync(otherBranch, await CashFundIdAsync(otherBranch),
            await CreateBankFundAsync("NH01", otherBranch), 100m, at, code: "CQ00001");
    }

    [Fact]
    public async Task Transfers_are_fund_movements()
    {
        var at = Vn("2026-10-05 09:00");
        await InsertFundTransferAsync(_branchId, _qtmId, _nh01Id, 300m, at, code: "CQ00001");
        // Neither a Cancelled nor a deleted transfer moves money.
        await InsertFundTransferAsync(_branchId, _qtmId, _nh01Id, 100m, at, DocumentStatus.Cancelled, code: "CQ00002");
        var deletedId = await InsertFundTransferAsync(_branchId, _qtmId, _nh01Id, 50m, at, code: "CQ00003");
        await InDbAsync(db => db.FundTransfers.Where(t => t.Id == deletedId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDeleted, true)));

        await using var scope = _factory.Services.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<IFundBalanceGuard>();

        // Opening first, then the transfer: an outflow on the source fund, an inflow on the destination fund.
        (await guard.MovementsAsync(_qtmId)).Should().Equal(
            new BalanceMovement(DateTimeOffset.MinValue, 0, string.Empty, 0m),
            new BalanceMovement(at, 1, "CQ00001", -300m));
        (await guard.MovementsAsync(_nh01Id)).Should().Equal(
            new BalanceMovement(DateTimeOffset.MinValue, 0, string.Empty, 0m),
            new BalanceMovement(at, 0, "CQ00001", 300m));
    }

    [Fact]
    public async Task Fund_with_transfer_cannot_be_deleted()
    {
        var nh02 = await CreateBankFundAsync("NH02");
        var nh03 = await CreateBankFundAsync("NH03");
        // NH02 receives a transfer, NH03 sends one; neither is the default bank fund (NH01).
        await InsertFundTransferAsync(_branchId, _qtmId, nh02, 300m, Vn("2026-10-05 09:00"));
        await InsertFundTransferAsync(_branchId, nh03, _qtmId, 100m, Vn("2026-10-05 10:00"),
            DocumentStatus.Cancelled);

        foreach (var fundId in new[] { nh02, nh03 })
        {
            var response = await _branchClient.DeleteAsync($"/api/funds/{fundId}");
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Message
                .Should().Be("Quỹ đã có phiếu chuyển quỹ, không thể xóa hoặc đổi loại.");

            var fund = await ReadDataAsync<FundDto>(await _branchClient.GetAsync($"/api/funds/{fundId}"));
            var kindChange = new UpdateFundRequest
            {
                Name = fund.Name, Kind = FundKind.Cash, IsDefault = false, IsActive = true,
            };
            (await _branchClient.PutAsJsonAsync($"/api/funds/{fundId}", kindChange)).StatusCode
                .Should().Be(HttpStatusCode.Conflict);
        }

        // A fund without transfers can still be deleted.
        var nh04 = await CreateBankFundAsync("NH04");
        (await _branchClient.DeleteAsync($"/api/funds/{nh04}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_transfer_numbers_and_logs()
    {
        await SetFundOpeningAmountAsync(_qtmId, 1_000_000m);
        var adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());
        var at = Vn("2026-10-05 09:00");
        var request = TransferRequest(_qtmId, _nh01Id, 400_000m, "2026-10-05 09:00");
        request.Description = "Nộp tiền vào ngân hàng";
        request.Note = "Ghi chú";

        var (status, transfer, error) = await PostTransferAsync(_branchClient, request);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        transfer.Should().BeEquivalentTo(new
        {
            Code = "CQ00001",
            VoucherAt = at,
            BranchId = _branchId,
            FromFundId = _qtmId,
            FromFundCode = "QTM",
            FromFundName = "Quỹ tiền mặt",
            FromFundKind = FundKind.Cash,
            ToFundId = _nh01Id,
            ToFundCode = "NH01",
            ToFundName = "Quỹ NH01",
            ToFundKind = FundKind.Bank,
            Amount = 400_000m,
            Description = "Nộp tiền vào ngân hàng",
            Note = "Ghi chú",
            Status = DocumentStatus.Active,
            CancelledAt = (DateTimeOffset?)null,
            OwnerUserId = adminId,
            CanEdit = true,
            CanCancel = true,
            CanDelete = true,
        });
        transfer!.VoucherAt.Offset.Should().Be(TimeSpan.Zero);
        transfer.Version.Should().NotBe(0u);

        var activities = await InDbAsync(db => db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentId == transfer.Id).ToListAsync());
        activities.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            DocumentType = DocumentType.FundTransfer,
            Action = CashDocumentActivityAction.Created,
            ActorUserId = (Guid?)adminId,
        });

        // The transfer leaves QTM and enters NH01.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var guard = scope.ServiceProvider.GetRequiredService<IFundBalanceGuard>();
            (await guard.MovementsAsync(_qtmId)).Should().Equal(
                new BalanceMovement(DateTimeOffset.MinValue, 0, string.Empty, 1_000_000m),
                new BalanceMovement(at, 1, "CQ00001", -400_000m));
            (await guard.MovementsAsync(_nh01Id)).Should().Equal(
                new BalanceMovement(DateTimeOffset.MinValue, 0, string.Empty, 0m),
                new BalanceMovement(at, 0, "CQ00001", 400_000m));
        }

        var (getStatus, fetched, getError) = await GetTransferAsync(_branchClient, transfer.Id);
        getStatus.Should().Be(HttpStatusCode.OK, getError?.Message);
        fetched.Should().BeEquivalentTo(transfer);

        // The next transfer of the branch takes the next number, whatever its direction.
        var (_, back, backError) = await PostTransferAsync(_branchClient,
            TransferRequest(_nh01Id, _qtmId, 100_000m, "2026-10-06 09:00"));
        back.Should().NotBeNull(backError?.Message);
        back!.Code.Should().Be("CQ00002");
    }

    [Fact]
    public async Task Validation_rules()
    {
        await SetFundOpeningAmountAsync(_qtmId, 1_000_000m);
        var otherBranch = await CreateBranchAsync("FT" + Guid.NewGuid().ToString("N")[..8]);
        var otherFund = await CashFundIdAsync(otherBranch);
        var inactiveFund = await CreateBankFundAsync("NH02", isActive: false);

        AssertFieldError(await PostTransferAsync(_branchClient,
            TransferRequest(_qtmId, _qtmId, 100m, "2026-10-05 09:00")), "toFundId", "Quỹ đến phải khác quỹ đi.");
        AssertFieldError(await PostTransferAsync(_branchClient,
            TransferRequest(otherFund, _nh01Id, 100m, "2026-10-05 09:00")), "fromFundId");
        AssertFieldError(await PostTransferAsync(_branchClient,
            TransferRequest(Guid.NewGuid(), _nh01Id, 100m, "2026-10-05 09:00")), "fromFundId");
        AssertFieldError(await PostTransferAsync(_branchClient,
            TransferRequest(_qtmId, inactiveFund, 100m, "2026-10-05 09:00")), "toFundId");
        AssertFieldError(await PostTransferAsync(_branchClient,
            TransferRequest(_qtmId, _nh01Id, 0m, "2026-10-05 09:00")), "amount");
        AssertFieldError(await PostTransferAsync(_branchClient,
            TransferRequest(_qtmId, _nh01Id, 100.005m, "2026-10-05 09:00")), "amount");
        var longDescription = TransferRequest(_qtmId, _nh01Id, 100m, "2026-10-05 09:00");
        longDescription.Description = new string('a', 501);
        AssertFieldError(await PostTransferAsync(_branchClient, longDescription), "description");

        var lockResponse = await _client.PutAsJsonAsync($"/api/branches/{_branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 10, 5) });
        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        try
        {
            var (status, _, error) = await PostTransferAsync(_branchClient,
                TransferRequest(_qtmId, _nh01Id, 100m, "2026-10-05 23:00"));
            status.Should().Be(HttpStatusCode.BadRequest, error?.Message);
            error!.Code.Should().Be("PERIOD_LOCKED");
        }
        finally
        {
            (await _client.PutAsJsonAsync($"/api/branches/{_branchId}/lock",
                new SetPeriodLockRequest { LockedUntil = null })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await TransferCountAsync()).Should().Be(0, "a rejected create saves nothing");
    }

    [Fact]
    public async Task Overdrawing_the_source_fund_follows_policy()
    {
        await SetFundOpeningAmountAsync(_qtmId, 100m);
        try
        {
            var request = TransferRequest(_qtmId, _nh01Id, 500m, "2026-10-05 09:00");
            var (warnStatus, _, warnError) = await PostTransferAsync(_branchClient, request);
            warnStatus.Should().Be(HttpStatusCode.UnprocessableEntity, warnError?.Message);
            warnError!.Code.Should().Be("NEGATIVE_FUND_WARNING");
            // Only the source fund is reported: the destination only gains.
            warnError.Details.Should().ContainSingle().Which.Key.Should().Be("QTM");
            warnError.Details!["QTM"].Should().Equal("Âm 400 tại 05/10/2026 09:00");
            (await TransferCountAsync()).Should().Be(0, "a rejected create saves nothing");

            request.AcknowledgeNegativeFund = true;
            var (ackStatus, acknowledged, ackError) = await PostTransferAsync(_branchClient, request);
            ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
            acknowledged!.Code.Should().Be("CQ00001");

            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Block);
            var more = TransferRequest(_qtmId, _nh01Id, 100m, "2026-10-06 09:00");
            more.AcknowledgeNegativeFund = true;
            var (blockStatus, _, blockError) = await PostTransferAsync(_branchClient, more);
            blockStatus.Should().Be(HttpStatusCode.UnprocessableEntity, blockError?.Message);
            blockError!.Code.Should().Be("NEGATIVE_FUND_BLOCKED");
            blockError.Details.Should().ContainKey("QTM");
            (await TransferCountAsync()).Should().Be(1);

            // A transfer into the overdrawn fund passes even under Block: QTM only gains.
            await SetFundOpeningAmountAsync(_nh01Id, 1_000m);
            var (inStatus, _, inError) = await PostTransferAsync(_branchClient,
                TransferRequest(_nh01Id, _qtmId, 100m, "2026-10-07 09:00"));
            inStatus.Should().Be(HttpStatusCode.OK, inError?.Message);
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Requires_permission()
    {
        await SetFundOpeningAmountAsync(_qtmId, 1_000_000m);
        var transfer = (await PostTransferAsync(_branchClient,
            TransferRequest(_qtmId, _nh01Id, 100m, "2026-10-05 09:00"))).Transfer!;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        using var sales = await CreateClientForRoleAsync("ft_sales_" + suffix, RoleCodes.Sales, _branchId);
        UseBranch(sales, _branchId);
        (await PostTransferAsync(sales, TransferRequest(_qtmId, _nh01Id, 100m, "2026-10-05 10:00")))
            .Status.Should().Be(HttpStatusCode.Forbidden);
        (await GetTransferAsync(sales, transfer.Id)).Status.Should().Be(HttpStatusCode.Forbidden);

        // View alone reads a transfer but cannot create one.
        using var viewer = await CreateClientWithPermissionsAsync("ft_view_" + suffix, _branchId,
            Permissions.FundTransfers.View);
        UseBranch(viewer, _branchId);
        (await GetTransferAsync(viewer, transfer.Id)).Status.Should().Be(HttpStatusCode.OK);
        (await PostTransferAsync(viewer, TransferRequest(_qtmId, _nh01Id, 100m, "2026-10-05 10:00")))
            .Status.Should().Be(HttpStatusCode.Forbidden);
        (await TransferCountAsync()).Should().Be(1);
    }

    private static void AssertFieldError((HttpStatusCode Status, FundTransferDto? Transfer, ApiError? Error) result,
        string key, string? message = null)
    {
        result.Status.Should().Be(HttpStatusCode.BadRequest, result.Error?.Message);
        result.Error!.Details.Should().ContainKey(key);
        if (message is not null)
            result.Error.Details![key].Should().Equal(message);
    }

    /// Transfers of this test's branch, soft-deleted ones included.
    private Task<int> TransferCountAsync() =>
        InDbAsync(db => db.FundTransfers.IgnoreQueryFilters().CountAsync(t => t.BranchId == _branchId));

    private async Task<Guid> CreateBankFundAsync(string code, Guid? branchId = null, bool isActive = true)
    {
        var client = _branchClient;
        if (branchId is { } other)
        {
            client = CloneAdminClient();
            UseBranch(client, other);
        }
        var fund = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = code, Name = "Quỹ " + code, Kind = FundKind.Bank, IsActive = isActive,
        }));
        return fund.Id;
    }
}
