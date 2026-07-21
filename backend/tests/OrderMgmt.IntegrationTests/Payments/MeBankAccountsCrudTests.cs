using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Payments;

[Collection(nameof(PostgresCollection))]
public class MeBankAccountsCrudTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;
    private Guid _vcbBankId;
    private Guid _tcbBankId;

    public MeBankAccountsCrudTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg.ConnectionString);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _vcbBankId = (await db.Banks.FirstAsync(b => b.Code == "VCB")).Id;
        _tcbBankId = (await db.Banks.FirstAsync(b => b.Code == "TCB")).Id;
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    [Fact]
    public async Task Create_first_account_is_automatically_default()
    {
        var response = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0011122233",
            AccountName = "Nguyen Van A",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        body!.Data!.IsDefault.Should().BeTrue();
        body.Data.BankCode.Should().Be("VCB");
    }

    [Fact]
    public async Task Create_second_account_as_default_unsets_previous_default()
    {
        var first = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "1112223334",
            AccountName = "Account One",
        });
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var second = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _tcbBankId,
            AccountNumber = "2223334445",
            AccountName = "Account Two",
            IsDefault = true,
        });
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        secondBody!.Data!.IsDefault.Should().BeTrue();

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Single(a => a.Id == firstBody!.Data!.Id).IsDefault.Should().BeFalse();
        list.Data.Single(a => a.Id == secondBody.Data.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task List_only_returns_current_users_accounts()
    {
        await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "3334445556",
            AccountName = "Admin Account",
        });

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Should().OnlyContain(a => a.AccountNumber != null);
        list.Data.Should().Contain(a => a.AccountNumber == "3334445556");
    }

    [Fact]
    public async Task Update_changes_account_fields()
    {
        var create = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "4445556667",
            AccountName = "Before Update",
        });
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var update = await _client.PutAsJsonAsync($"/api/me/bank-accounts/{created!.Data!.Id}",
            new UpdateUserBankAccountRequest
            {
                BankId = _tcbBankId,
                AccountNumber = "5556667778",
                AccountName = "After Update",
            });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await update.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        body!.Data!.AccountName.Should().Be("After Update");
        body.Data.BankCode.Should().Be("TCB");
    }

    [Fact]
    public async Task SetDefault_marks_target_and_unmarks_others()
    {
        var first = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "6667778889",
            AccountName = "First",
        });
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var second = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _tcbBankId,
            AccountNumber = "7778889990",
            AccountName = "Second",
        });
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var setDefault = await _client.PutAsync($"/api/me/bank-accounts/{firstBody!.Data!.Id}/default", null);
        setDefault.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Single(a => a.Id == firstBody.Data.Id).IsDefault.Should().BeTrue();
        list.Data.Single(a => a.Id == secondBody!.Data!.Id).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_removes_account_from_list()
    {
        var create = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "8889990001",
            AccountName = "Will Delete",
        });
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var delete = await _client.DeleteAsync($"/api/me/bank-accounts/{created!.Data!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Should().NotContain(a => a.Id == created.Data.Id);
    }

    [Fact]
    public async Task Delete_of_default_account_promotes_another_remaining_account_to_default()
    {
        var first = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "9990001112",
            AccountName = "First (default)",
        });
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);
        firstBody!.Data!.IsDefault.Should().BeTrue(); // first account is auto-default

        var second = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _tcbBankId,
            AccountNumber = "0001112223",
            AccountName = "Second",
        });
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponse<UserBankAccountDto>>(TestJson.Options);

        var delete = await _client.DeleteAsync($"/api/me/bank-accounts/{firstBody.Data.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<UserBankAccountDto>>>(
            "/api/me/bank-accounts", TestJson.Options);
        list!.Data!.Should().ContainSingle(a => a.IsDefault); // exactly one default remains
        list.Data.Single(a => a.Id == secondBody!.Data!.Id).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Create_with_invalid_account_number_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/me/bank-accounts", new CreateUserBankAccountRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "12",
            AccountName = "Too Short",
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unauthenticated_list_returns_401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/me/bank-accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
