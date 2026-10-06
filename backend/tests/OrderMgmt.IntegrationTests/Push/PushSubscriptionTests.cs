using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Push;

[Collection(nameof(PostgresCollection))]
public class PushSubscriptionTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;

    public PushSubscriptionTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();

        // Authenticate as admin
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody!.Data!.AccessToken);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    private static object ValidSubscriptionBody(string endpoint = "https://push.example.com/sub/123") => new
    {
        endpoint,
        p256dh = "BNbxSuT_abc123",
        auth = "auth_token_xyz"
    };

    [Fact]
    public async Task Subscribe_CreatesSubscription()
    {
        var resp = await _client.PostAsJsonAsync("/api/push/subscribe", ValidSubscriptionBody());
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sub = await db.PushSubscriptions.FirstOrDefaultAsync(
            s => s.Endpoint == "https://push.example.com/sub/123");
        sub.Should().NotBeNull();
    }

    [Fact]
    public async Task Subscribe_SameEndpoint_Upserts()
    {
        var body = ValidSubscriptionBody("https://push.example.com/sub/upsert");
        await _client.PostAsJsonAsync("/api/push/subscribe", body);
        await _client.PostAsJsonAsync("/api/push/subscribe", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.PushSubscriptions.CountAsync(
            s => s.Endpoint == "https://push.example.com/sub/upsert");
        count.Should().Be(1);
    }

    [Fact]
    public async Task Unsubscribe_DeletesSubscription()
    {
        var endpoint = "https://push.example.com/sub/delete";
        await _client.PostAsJsonAsync("/api/push/subscribe", ValidSubscriptionBody(endpoint));

        var resp = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/push/subscribe")
        {
            Content = JsonContent.Create(new { endpoint })
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sub = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        sub.Should().BeNull();
    }

    [Fact]
    public async Task Unsubscribe_NonExistent_ReturnsOk()
    {
        var resp = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/push/subscribe")
        {
            Content = JsonContent.Create(new { endpoint = "https://push.example.com/nonexistent" })
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
