using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.Common.Models;
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

    private async Task<Guid> CreateBankFundAsync(string code, Guid? branchId = null)
    {
        var client = _branchClient;
        if (branchId is { } other)
        {
            client = CloneAdminClient();
            UseBranch(client, other);
        }
        var fund = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = code, Name = "Quỹ " + code, Kind = FundKind.Bank, IsActive = true,
        }));
        return fund.Id;
    }
}
