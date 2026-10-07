using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class FundCrudTests : CashDebtTestBase
{
    public FundCrudTests(PostgresFixture pg) : base(pg) { }

    private async Task<(HttpClient Client, Guid BranchId)> NewBranchClientAsync()
    {
        var branchId = await CreateBranchAsync("F" + Guid.NewGuid().ToString("N")[..8]);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (client, branchId);
    }

    private static CreateFundRequest NewFund(string code, FundKind kind, bool isDefault = false) => new()
    {
        Code = code, Name = "Quỹ " + code, Kind = kind, IsDefault = isDefault, IsActive = true,
    };

    private static UpdateFundRequest ToUpdate(FundDto f) => new()
    {
        Name = f.Name, Kind = f.Kind, BankName = f.BankName, AccountNumber = f.AccountNumber,
        IsDefault = f.IsDefault, IsActive = f.IsActive,
    };

    private static async Task<string[]> ErrorKeysAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details!.Keys.ToArray();

    [Fact]
    public async Task Seed_and_new_branch_have_default_cash_fund()
    {
        var main = await ReadDataAsync<List<FundDto>>(await _client.GetAsync("/api/funds"));
        var qtm = main.Single(f => f.Code == "QTM");
        qtm.Kind.Should().Be(FundKind.Cash);
        qtm.IsDefault.Should().BeTrue();
        qtm.BranchId.Should().Be(MainBranchId);
        qtm.Name.Should().Be("Quỹ tiền mặt");

        var (client, branchId) = await NewBranchClientAsync();
        var created = await ReadDataAsync<List<FundDto>>(await client.GetAsync("/api/funds"));
        var only = created.Should().ContainSingle().Subject;
        only.Code.Should().Be("QTM");
        only.Kind.Should().Be(FundKind.Cash);
        only.IsDefault.Should().BeTrue();
        only.BranchId.Should().Be(branchId);
    }

    [Fact]
    public async Task Crud_happy_path_and_guard()
    {
        var (client, branchId) = await NewBranchClientAsync();

        var created = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds",
            new CreateFundRequest
            {
                Code = "NH01", Name = "Vietcombank", Kind = FundKind.Bank, BankName = "Vietcombank",
                AccountNumber = "0011223344", IsDefault = false, IsActive = true,
            }));
        created.BranchId.Should().Be(branchId);
        created.BankName.Should().Be("Vietcombank");
        created.AccountNumber.Should().Be("0011223344");
        created.OpeningAmount.Should().Be(0);
        created.IsDefault.Should().BeTrue("the first fund of a kind becomes the default");

        var list = await ReadDataAsync<List<FundDto>>(await client.GetAsync("/api/funds?kind=Bank"));
        list.Select(f => f.Code).Should().Equal("NH01");

        var update = ToUpdate(created);
        update.Name = "VCB chính";
        var updated = await ReadDataAsync<FundDto>(await client.PutAsJsonAsync($"/api/funds/{created.Id}", update));
        updated.Name.Should().Be("VCB chính");
        updated.IsDefault.Should().BeTrue();

        // Another branch cannot see the fund; the main-branch list does not include it either.
        (await _client.GetAsync($"/api/funds/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var mainList = await ReadDataAsync<List<FundDto>>(await _client.GetAsync("/api/funds"));
        mainList.Should().NotContain(f => f.Id == created.Id);
        var branchList = await ReadDataAsync<List<FundDto>>(await client.GetAsync("/api/funds"));
        branchList.Should().OnlyContain(f => f.BranchId == branchId);

        var sales = await CreateClientForRoleAsync("fund_sales", RoleCodes.Sales);
        (await sales.PostAsJsonAsync("/api/funds", NewFund("X" + Guid.NewGuid().ToString("N")[..6], FundKind.Cash)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var none = await client.PostAsJsonAsync("/api/funds", NewFund("BAD", FundKind.None));
        none.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorKeysAsync(none)).Should().Contain("kind");

        (await client.DeleteAsync($"/api/funds/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/funds/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Default_fund_rules()
    {
        var (client, _) = await NewBranchClientAsync();

        var nh01 = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", NewFund("NH01", FundKind.Bank)));
        nh01.IsDefault.Should().BeTrue();

        var nh02 = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", NewFund("NH02", FundKind.Bank, isDefault: true)));
        nh02.IsDefault.Should().BeTrue();
        (await ReadDataAsync<FundDto>(await client.GetAsync($"/api/funds/{nh01.Id}"))).IsDefault.Should().BeFalse();

        var off = ToUpdate(nh02);
        off.IsDefault = false;
        var offResponse = await client.PutAsJsonAsync($"/api/funds/{nh02.Id}", off);
        offResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorKeysAsync(offResponse)).Should().Contain("isDefault");

        var inactive = ToUpdate(nh02);
        inactive.IsActive = false;
        var inactiveResponse = await client.PutAsJsonAsync($"/api/funds/{nh02.Id}", inactive);
        inactiveResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorKeysAsync(inactiveResponse)).Should().Contain("isActive");

        // Making NH01 default again clears NH02.
        var back = ToUpdate(nh01);
        back.IsDefault = true;
        await ReadDataAsync<FundDto>(await client.PutAsJsonAsync($"/api/funds/{nh01.Id}", back));
        (await ReadDataAsync<FundDto>(await client.GetAsync($"/api/funds/{nh02.Id}"))).IsDefault.Should().BeFalse();

        var funds = await ReadDataAsync<List<FundDto>>(await client.GetAsync("/api/funds"));
        var qtm = funds.Single(f => f.Code == "QTM");
        await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", NewFund("QTM2", FundKind.Cash)));
        (await client.DeleteAsync($"/api/funds/{qtm.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A non-default fund can be deleted.
        (await client.DeleteAsync($"/api/funds/{nh02.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Opening_amount_blocks_delete_and_kind_change()
    {
        var (client, _) = await NewBranchClientAsync();
        var fund = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", NewFund("NH01", FundKind.Bank)));
        await InDbAsync(async db =>
        {
            var entity = db.Funds.Single(f => f.Id == fund.Id);
            entity.OpeningAmount = 1000m;
            await db.SaveChangesAsync();
        });

        var kind = ToUpdate(fund);
        kind.Kind = FundKind.Cash;
        (await client.PutAsJsonAsync($"/api/funds/{fund.Id}", kind)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"/api/funds/{fund.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Kind_change_keeps_the_default_rules()
    {
        var (client, _) = await NewBranchClientAsync();
        var qtm = (await ReadDataAsync<List<FundDto>>(await client.GetAsync("/api/funds"))).Single();
        var nh01 = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", NewFund("NH01", FundKind.Bank)));
        var nh02 = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", NewFund("NH02", FundKind.Bank)));
        nh02.IsDefault.Should().BeFalse();

        // A non-default fund moves into a kind that already has a default: stays non-default.
        var toCash = ToUpdate(nh02);
        toCash.Kind = FundKind.Cash;
        var moved = await ReadDataAsync<FundDto>(await client.PutAsJsonAsync($"/api/funds/{nh02.Id}", toCash));
        moved.Kind.Should().Be(FundKind.Cash);
        moved.IsDefault.Should().BeFalse();
        (await ReadDataAsync<FundDto>(await client.GetAsync($"/api/funds/{qtm.Id}"))).IsDefault.Should().BeTrue();
        (await ReadDataAsync<FundDto>(await client.GetAsync($"/api/funds/{nh01.Id}"))).IsDefault.Should().BeTrue();

        // The default of a kind cannot leave while a sibling remains.
        var sibling = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", NewFund("NH03", FundKind.Bank)));
        var leave = ToUpdate(nh01);
        leave.Kind = FundKind.Cash;
        (await client.PutAsJsonAsync($"/api/funds/{nh01.Id}", leave)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadDataAsync<FundDto>(await client.GetAsync($"/api/funds/{nh01.Id}"))).Kind.Should().Be(FundKind.Bank);

        // A non-default fund of the default's kind moves freely, the old kind keeps its default.
        var siblingToCash = ToUpdate(sibling);
        siblingToCash.Kind = FundKind.Cash;
        (await client.PutAsJsonAsync($"/api/funds/{sibling.Id}", siblingToCash)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<FundDto>(await client.GetAsync($"/api/funds/{nh01.Id}"))).IsDefault.Should().BeTrue();

        // The only fund of its kind moves into an empty kind and becomes the default there.
        var (client2, _) = await NewBranchClientAsync();
        var lone = await ReadDataAsync<FundDto>(await client2.PostAsJsonAsync("/api/funds", NewFund("NH01", FundKind.Bank)));
        var toCashAlone = ToUpdate(lone);
        toCashAlone.Kind = FundKind.Cash;
        // Empty the Cash kind first by deleting the seeded QTM.
        var qtm2 = (await ReadDataAsync<List<FundDto>>(await client2.GetAsync("/api/funds?kind=Cash"))).Single();
        (await client2.DeleteAsync($"/api/funds/{qtm2.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var alone = await ReadDataAsync<FundDto>(await client2.PutAsJsonAsync($"/api/funds/{lone.Id}", toCashAlone));
        alone.Kind.Should().Be(FundKind.Cash);
        alone.IsDefault.Should().BeTrue();
    }
}
