using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;
using Xunit;

namespace OrderMgmt.IntegrationTests.Organization;

[Collection(nameof(PostgresCollection))]
public class BranchCrudTests : QuotationTestBase
{
    public BranchCrudTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Crud_and_period_lock_happy_path()
    {
        var created = await CreateAsync("CN02", "Chi nhánh 2");
        created.Code.Should().Be("CN02");
        created.Address.Should().Be("Hà Nội");

        var list = await _client.GetFromJsonAsync<ApiResponse<List<BranchDto>>>("/api/branches", TestJson.Options);
        list!.Data!.Select(b => b.Code).Should().ContainInOrder("CN01", "CN02");

        var get = await _client.GetFromJsonAsync<ApiResponse<BranchDto>>($"/api/branches/{created.Id}", TestJson.Options);
        get!.Data!.Name.Should().Be("Chi nhánh 2");

        var update = await _client.PutAsJsonAsync($"/api/branches/{created.Id}",
            new UpdateBranchRequest { Name = "Chi nhánh Hai", Address = null });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadAsync<BranchDto>(update);
        updated.Name.Should().Be("Chi nhánh Hai");
        updated.Address.Should().BeNull();

        var locked = await _client.PutAsJsonAsync($"/api/branches/{created.Id}/lock",
            new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 9, 30) });
        locked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<BranchDto>(locked)).LockedUntil.Should().Be(new DateOnly(2026, 9, 30));

        var unlocked = await _client.PutAsJsonAsync($"/api/branches/{created.Id}/lock",
            new SetPeriodLockRequest { LockedUntil = null });
        (await ReadAsync<BranchDto>(unlocked)).LockedUntil.Should().BeNull();

        var delete = await _client.DeleteAsync($"/api/branches/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/branches/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Conflicts_return_409()
    {
        await CreateAsync("CN02", "Chi nhánh 2");
        var duplicate = await _client.PostAsJsonAsync("/api/branches",
            new CreateBranchRequest { Code = "CN02", Name = "Trùng mã" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await _client.DeleteAsync($"/api/branches/{BranchDefaults.MainBranchId}"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var used = await CreateAsync("CN03", "Chi nhánh 3");
        await CreateTestUserAsync("cn03_user", "Pass@123", RoleCodes.Sales, used.Id);
        (await _client.DeleteAsync($"/api/branches/{used.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Mutations_and_lock_require_their_permissions()
    {
        await CreateTestUserAsync("branch_sales", "Pass@123", RoleCodes.Sales);
        var sales = await LoginAsync("branch_sales", "Pass@123");

        (await sales.PostAsJsonAsync("/api/branches", new CreateBranchRequest { Code = "CN09", Name = "X" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sales.PutAsJsonAsync($"/api/branches/{BranchDefaults.MainBranchId}/lock",
                new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 9, 30) }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<BranchDto> CreateAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/branches",
            new CreateBranchRequest { Code = code, Name = name, Address = "Hà Nội" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadAsync<BranchDto>(response);
    }

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(TestJson.Options))!.Data!;
}
