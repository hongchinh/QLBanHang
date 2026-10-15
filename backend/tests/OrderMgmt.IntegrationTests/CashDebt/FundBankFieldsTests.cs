using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class FundBankFieldsTests : CashDebtTestBase
{
    public FundBankFieldsTests(PostgresFixture pg) : base(pg) { }

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
        BankId = f.BankId, AccountHolderName = f.AccountHolderName,
        IsDefault = f.IsDefault, IsActive = f.IsActive,
    };

    private static async Task<string[]> ErrorKeysAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details!.Keys.ToArray();

    private async Task<List<BankDto>> BanksAsync()
        => await ReadDataAsync<List<BankDto>>(await _client.GetAsync("/api/banks"));

    private static CreateFundRequest BankFund(string code, Guid? bankId, string? account, string? holder = null)
    {
        var request = NewFund(code, FundKind.Bank);
        request.BankId = bankId;
        request.AccountNumber = account;
        request.AccountHolderName = holder;
        return request;
    }

    private static string ShortOf(BankDto bank) => bank.ShortName ?? bank.Code;

    [Fact]
    public async Task Bank_fund_stores_bank_and_holder()
    {
        var bank = (await BanksAsync()).First();
        var (client, _) = await NewBranchClientAsync();

        var created = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds",
            BankFund("NH01", bank.Id, "0123456789", "CONG TY ABC")));
        created.BankId.Should().Be(bank.Id);
        created.AccountHolderName.Should().Be("CONG TY ABC");
        created.BankShortName.Should().Be(ShortOf(bank));

        var read = await ReadDataAsync<FundDto>(await client.GetAsync($"/api/funds/{created.Id}"));
        read.BankId.Should().Be(bank.Id);
        read.AccountHolderName.Should().Be("CONG TY ABC");
        read.BankShortName.Should().Be(ShortOf(bank));

        var listed = (await ReadDataAsync<List<FundDto>>(await client.GetAsync("/api/funds?kind=Bank"))).Single();
        listed.BankShortName.Should().Be(ShortOf(bank));
    }

    [Fact]
    public async Task Update_changes_bank_and_holder()
    {
        var banks = await BanksAsync();
        var (client, _) = await NewBranchClientAsync();
        var created = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds",
            BankFund("NH01", banks[0].Id, "0123456789", "CONG TY ABC")));

        var update = ToUpdate(created);
        update.BankId = banks[1].Id;
        update.AccountHolderName = "NGUYEN VAN A";
        var updated = await ReadDataAsync<FundDto>(await client.PutAsJsonAsync($"/api/funds/{created.Id}", update));

        updated.BankId.Should().Be(banks[1].Id);
        updated.BankShortName.Should().Be(ShortOf(banks[1]));
        updated.AccountHolderName.Should().Be("NGUYEN VAN A");
    }

    [Fact]
    public async Task Other_kind_stores_no_bank_fields()
    {
        var bank = (await BanksAsync()).First();
        var (client, _) = await NewBranchClientAsync();

        var cash = NewFund("TM02", FundKind.Cash);
        cash.BankId = bank.Id;
        cash.AccountHolderName = "CONG TY ABC";
        var createdCash = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds", cash));
        createdCash.BankId.Should().BeNull();
        createdCash.BankShortName.Should().BeNull();
        createdCash.AccountHolderName.Should().BeNull();

        var bankFund = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds",
            BankFund("NH01", bank.Id, "0123456789", "CONG TY ABC")));
        var toCash = ToUpdate(bankFund);
        toCash.Kind = FundKind.Cash;
        var moved = await ReadDataAsync<FundDto>(await client.PutAsJsonAsync($"/api/funds/{bankFund.Id}", toCash));
        moved.Kind.Should().Be(FundKind.Cash);
        moved.BankId.Should().BeNull();
        moved.BankShortName.Should().BeNull();
        moved.AccountHolderName.Should().BeNull();
    }

    [Fact]
    public async Task Unknown_bank_is_rejected()
    {
        var (client, _) = await NewBranchClientAsync();
        var response = await client.PostAsJsonAsync("/api/funds", BankFund("NH01", Guid.NewGuid(), "0123456789"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorKeysAsync(response)).Should().Contain("bankId");
    }

    [Fact]
    public async Task Inactive_bank_is_rejected()
    {
        var banks = await BanksAsync();
        var other = banks[1];
        var (client, _) = await NewBranchClientAsync();

        await InDbAsync(db => db.Banks.Where(b => b.Id == other.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false)));
        try
        {
            var response = await client.PostAsJsonAsync("/api/funds", BankFund("NH01", other.Id, "0123456789"));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ErrorKeysAsync(response)).Should().Contain("bankId");
        }
        finally
        {
            await InDbAsync(db => db.Banks.Where(b => b.Id == other.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, true)));
        }
    }

    [Fact]
    public async Task Account_number_rules_apply_with_a_bank()
    {
        var bank = (await BanksAsync()).First();
        var (client, _) = await NewBranchClientAsync();

        var code = 0;
        async Task<HttpResponseMessage> PostAsync(Guid? bankId, string? account)
            => await client.PostAsJsonAsync("/api/funds", BankFund("NH" + ++code, bankId, account));

        foreach (var bad in new[] { "12 34", "AB-12", "1234567890123456789A", null })
        {
            var response = await PostAsync(bank.Id, bad);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, bad ?? "(null)");
            (await ErrorKeysAsync(response)).Should().Contain("accountNumber", bad ?? "(null)");
        }

        (await PostAsync(bank.Id, "1234567890123456AB")).StatusCode.Should().Be(HttpStatusCode.OK);
        var nineteen = "123456789012345678A";
        nineteen.Length.Should().Be(19);
        (await PostAsync(bank.Id, nineteen)).StatusCode.Should().Be(HttpStatusCode.OK);

        // No bank chosen: the old free text (up to 50 characters) stays valid.
        (await PostAsync(null, "1234567890123456789A")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Holder_name_is_limited()
    {
        var bank = (await BanksAsync()).First();
        var (client, _) = await NewBranchClientAsync();

        var tooLong = await client.PostAsJsonAsync("/api/funds",
            BankFund("NH01", bank.Id, "0123456789", new string('A', 256)));
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorKeysAsync(tooLong)).Should().Contain("accountHolderName");

        var fits = await ReadDataAsync<FundDto>(await client.PostAsJsonAsync("/api/funds",
            BankFund("NH02", bank.Id, "0123456789", new string('A', 255))));
        fits.AccountHolderName.Should().HaveLength(255);
    }
}
