using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.CashVouchers;

[Collection(nameof(PostgresCollection))]
public class FundOpeningBalanceTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    private string _suffix = default!;

    public FundOpeningBalanceTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _suffix = Guid.NewGuid().ToString("N")[..8];
    }

    [Fact]
    public async Task Set_opening_balance()
    {
        var (branchId, client) = await BranchClientAsync("A", Oct1);
        var fundId = await CashFundIdAsync(branchId);

        var set = await PutAsync(client, fundId, 5_000_000m);
        set.Status.Should().Be(HttpStatusCode.OK, set.Error?.Message);
        set.Fund!.Id.Should().Be(fundId);
        set.Fund.OpeningAmount.Should().Be(5_000_000m);
        (await InDbAsync(db => db.Funds.Where(f => f.Id == fundId).Select(f => f.OpeningAmount).SingleAsync()))
            .Should().Be(5_000_000m);

        var negative = await PutAsync(client, fundId, -1m);
        negative.Status.Should().Be(HttpStatusCode.BadRequest);
        negative.Error!.Details.Should().ContainKey("amount");

        using var sales = await CreateClientForRoleAsync("fob_sales_" + _suffix, RoleCodes.Sales);
        (await PutAsync(sales, fundId, 1_000m)).Status.Should().Be(HttpStatusCode.Forbidden);

        // A branch without an opening date.
        var (noDateBranchId, noDateClient) = await BranchClientAsync("B", null);
        var noDate = await PutAsync(noDateClient, await CashFundIdAsync(noDateBranchId), 1_000m);
        noDate.Status.Should().Be(HttpStatusCode.BadRequest);
        noDate.Error!.Code.Should().Be("OPENING_DATE_NOT_SET");

        // The opening date is inside the locked period.
        var lockResponse = await _client.PutAsJsonAsync($"/api/branches/{branchId}/lock",
            new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 10, 5) });
        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        var locked = await PutAsync(client, fundId, 1_000m);
        locked.Status.Should().Be(HttpStatusCode.BadRequest);
        locked.Error!.Code.Should().Be("PERIOD_LOCKED");
        (await InDbAsync(db => db.Funds.Where(f => f.Id == fundId).Select(f => f.OpeningAmount).SingleAsync()))
            .Should().Be(5_000_000m);
    }

    [Fact]
    public async Task Lowering_opening_below_later_payments_warns()
    {
        try
        {
            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Warn);
            var (branchId, client) = await BranchClientAsync("C", Oct1);
            var fundId = await CashFundIdAsync(branchId);

            (await PutAsync(client, fundId, 2_000m)).Status.Should().Be(HttpStatusCode.OK);
            await InsertCashVoucherAsync(branchId, fundId, CashVoucherType.Payment, 1_500m, Vn("2026-10-03 09:00"));

            var lowered = await PutAsync(client, fundId, 1_000m);
            lowered.Status.Should().Be((HttpStatusCode)422);
            lowered.Error!.Code.Should().Be("NEGATIVE_FUND_WARNING");
            lowered.Error.Details.Should().ContainKey("QTM")
                .WhoseValue.Should().Equal("Âm 500 tại 03/10/2026 09:00");
            (await InDbAsync(db => db.Funds.Where(f => f.Id == fundId).Select(f => f.OpeningAmount).SingleAsync()))
                .Should().Be(2_000m, "the rejected change is rolled back");

            var acknowledged = await PutAsync(client, fundId, 1_000m, acknowledge: true);
            acknowledged.Status.Should().Be(HttpStatusCode.OK, acknowledged.Error?.Message);
            acknowledged.Fund!.OpeningAmount.Should().Be(1_000m);

            // Raising it again never warns.
            (await PutAsync(client, fundId, 3_000m)).Status.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s => s.NegativeFundPolicy = LimitPolicy.Warn);
        }
    }

    [Fact]
    public async Task Fund_with_vouchers_cannot_be_deleted()
    {
        var (branchId, client) = await BranchClientAsync("D", Oct1);
        var bank = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", new CreateFundRequest
        {
            Code = "NH01", Name = "Ngân hàng", Kind = FundKind.Bank, IsActive = true,
        }));
        var voucherId = await InsertCashVoucherAsync(branchId, bank.Id, CashVoucherType.Receipt, 1_000m,
            Vn("2026-10-03 09:00"));

        var delete = await client.DeleteAsync($"/api/funds/{bank.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict, await delete.Content.ReadAsStringAsync());

        var toCash = new UpdateFundRequest { Name = bank.Name, Kind = FundKind.Cash, IsDefault = false, IsActive = true };
        (await client.PutAsJsonAsync($"/api/funds/{bank.Id}", toCash)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A soft-deleted voucher no longer holds the fund.
        await InDbAsync(db => db.CashVouchers.Where(v => v.Id == voucherId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDeleted, true)));
        (await client.DeleteAsync($"/api/funds/{bank.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Clearing_branch_opening_date_with_fund_opening_balance_is_409()
    {
        var (branchId, client) = await BranchClientAsync("E", Oct1);
        var fundId = await CashFundIdAsync(branchId);
        (await PutAsync(client, fundId, 1_000m)).Status.Should().Be(HttpStatusCode.OK);

        // The fund opening balance sits at the branch opening date (P8), so the date cannot be cleared.
        var blocked = await PutOpeningDateAsync(branchId, null);
        blocked.Status.Should().Be(HttpStatusCode.Conflict);
        blocked.Error!.Code.Should().Be("OPENING_DATA_EXISTS");
        (await InDbAsync(db => db.Branches.Where(b => b.Id == branchId).Select(b => b.OpeningDate).SingleAsync()))
            .Should().Be(Oct1);

        // Without opening money the date can be cleared.
        (await PutAsync(client, fundId, 0m)).Status.Should().Be(HttpStatusCode.OK);
        var cleared = await PutOpeningDateAsync(branchId, null);
        cleared.Status.Should().Be(HttpStatusCode.OK, cleared.Error?.Message);
        cleared.Branch!.OpeningDate.Should().BeNull();
    }

    /// A fresh branch (opening date set straight in the DB when given) and an admin client working in it.
    private async Task<(Guid BranchId, HttpClient Client)> BranchClientAsync(string tag, DateOnly? openingDate)
    {
        var branchId = await CreateBranchAsync("FO" + tag + _suffix[..6]);
        if (openingDate is not null)
            await SetBranchOpeningDateAsync(branchId, openingDate);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client);
    }

    private static async Task<(HttpStatusCode Status, FundDto? Fund, ApiError? Error)> PutAsync(
        HttpClient client, Guid fundId, decimal amount, bool acknowledge = false)
    {
        var response = await client.PutAsJsonAsync($"/api/funds/{fundId}/opening-balance",
            new SetFundOpeningBalanceRequest { Amount = amount, AcknowledgeNegativeFund = acknowledge });
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : JsonSerializer.Deserialize<ApiResponse<FundDto>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }

    private async Task<(HttpStatusCode Status, BranchDto? Branch, ApiError? Error)> PutOpeningDateAsync(
        Guid branchId, DateOnly? date)
    {
        var response = await _client.PutAsJsonAsync($"/api/branches/{branchId}/opening-date",
            new SetOpeningDateRequest { OpeningDate = date });
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : JsonSerializer.Deserialize<ApiResponse<BranchDto>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }
}
