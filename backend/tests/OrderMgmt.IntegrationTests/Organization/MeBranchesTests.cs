using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;
using Xunit;

namespace OrderMgmt.IntegrationTests.Organization;

[Collection(nameof(PostgresCollection))]
public class MeBranchesTests : QuotationTestBase
{
    public MeBranchesTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Admin_works_in_default_or_requested_branch()
    {
        var b = await CreateBranchAsync("CN02");

        var mine = await GetMyBranchesAsync(_client, header: null);
        mine.WorkingBranchId.Should().Be(BranchDefaults.MainBranchId);
        mine.DefaultBranchId.Should().Be(BranchDefaults.MainBranchId);
        mine.CanSwitch.Should().BeTrue();
        mine.Branches.Select(x => x.Id).Should().Contain(new[] { BranchDefaults.MainBranchId, b });

        (await GetMyBranchesAsync(_client, header: b.ToString())).WorkingBranchId.Should().Be(b);
    }

    [Fact]
    public async Task User_without_access_all_is_forced_to_default_branch()
    {
        var b = await CreateBranchAsync("CN02");
        await CreateTestUserAsync("me_sales", "Pass@123", RoleCodes.Sales);
        var sales = await LoginAsync("me_sales", "Pass@123");

        var mine = await GetMyBranchesAsync(sales, header: b.ToString());
        mine.WorkingBranchId.Should().Be(BranchDefaults.MainBranchId);
        mine.CanSwitch.Should().BeFalse();
        mine.Branches.Select(x => x.Id).Should().Equal(BranchDefaults.MainBranchId);
    }

    [Fact]
    public async Task Unknown_or_malformed_header_falls_back_to_default()
    {
        (await GetMyBranchesAsync(_client, header: "not-a-guid")).WorkingBranchId
            .Should().Be(BranchDefaults.MainBranchId);
        (await GetMyBranchesAsync(_client, header: Guid.NewGuid().ToString())).WorkingBranchId
            .Should().Be(BranchDefaults.MainBranchId);
    }

    [Fact]
    public async Task Soft_deleted_user_with_valid_token_gets_401()
    {
        await CreateTestUserAsync("me_deleted", "Pass@123", RoleCodes.Sales);
        var client = await LoginAsync("me_deleted", "Pass@123");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Username == "me_deleted");
            user.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        (await client.GetAsync("/api/me/branches")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> CreateBranchAsync(string code)
    {
        var response = await _client.PostAsJsonAsync("/api/branches", new CreateBranchRequest { Code = code, Name = code });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<BranchDto>>(TestJson.Options))!.Data!.Id;
    }

    private static async Task<MyBranchesDto> GetMyBranchesAsync(HttpClient client, string? header)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me/branches");
        if (header is not null) request.Headers.Add("X-Branch-Id", header);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<MyBranchesDto>>(TestJson.Options))!.Data!;
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
}
