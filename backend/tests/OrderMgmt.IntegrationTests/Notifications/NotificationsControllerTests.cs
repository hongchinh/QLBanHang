using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Notifications.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Domain.Notifications;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Notifications;

[Collection(nameof(PostgresCollection))]
public class NotificationsControllerTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;
    private Guid _userAId;
    private Guid _userBId;

    public NotificationsControllerTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();

        _userAId = await CreateUserAsync("notif_a", "Notif@123", RoleCodes.Sales);
        _userBId = await CreateUserAsync("notif_b", "Notif@123", RoleCodes.Sales);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    private async Task<Guid> CreateUserAsync(string username, string password, string roleCode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var role = await db.Roles.FirstAsync(r => r.Code == roleCode);
        var user = new User
        {
            Username = username,
            Email = $"{username}@test.local",
            FullName = $"Test {username}",
            PasswordHash = hasher.Hash(password),
            Status = UserStatus.Active,
            UserRoles = new List<UserRole> { new() { RoleId = role.Id } },
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task AuthenticateAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = username, Password = password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    private async Task<Guid> SeedNotificationAsync(Guid userId, string title, bool isRead = false, string? link = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notif = new Notification
        {
            UserId = userId,
            Type = "Test",
            Title = title,
            Body = "Test body",
            Link = link,
            IsRead = isRead,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Notifications.Add(notif);
        await db.SaveChangesAsync();
        return notif.Id;
    }

    [Fact]
    public async Task List_returns_only_notifications_for_current_user()
    {
        await SeedNotificationAsync(_userAId, "A-1");
        await SeedNotificationAsync(_userAId, "A-2");
        await SeedNotificationAsync(_userBId, "B-1");

        await AuthenticateAsync("notif_a", "Notif@123");
        var res = await _client.GetAsync("/api/notifications");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<List<NotificationDto>>>(TestJson.Options);
        body!.Data.Should().HaveCount(2);
        body.Data.Should().OnlyContain(n => n.Title.StartsWith("A-"));
    }

    [Fact]
    public async Task List_with_unreadOnly_filters_out_read()
    {
        await SeedNotificationAsync(_userAId, "unread");
        await SeedNotificationAsync(_userAId, "read", isRead: true);

        await AuthenticateAsync("notif_a", "Notif@123");
        var res = await _client.GetAsync("/api/notifications?unreadOnly=true");
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<List<NotificationDto>>>(TestJson.Options);
        body!.Data.Should().HaveCount(1);
        body.Data![0].Title.Should().Be("unread");
    }

    [Fact]
    public async Task CountUnread_returns_correct_count()
    {
        await SeedNotificationAsync(_userAId, "n1");
        await SeedNotificationAsync(_userAId, "n2");
        await SeedNotificationAsync(_userAId, "n3", isRead: true);

        await AuthenticateAsync("notif_a", "Notif@123");
        var res = await _client.GetAsync("/api/notifications/unread-count");
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<int>>(TestJson.Options);
        body!.Data.Should().Be(2);
    }

    [Fact]
    public async Task MarkRead_on_other_users_notification_returns_404()
    {
        var bNotif = await SeedNotificationAsync(_userBId, "B-only");

        await AuthenticateAsync("notif_a", "Notif@123");
        var res = await _client.PostAsync($"/api/notifications/{bNotif}/read", null);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_on_own_notification_succeeds_and_flips_flag()
    {
        var aNotif = await SeedNotificationAsync(_userAId, "to-read");

        await AuthenticateAsync("notif_a", "Notif@123");
        var res = await _client.PostAsync($"/api/notifications/{aNotif}/read", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Notifications.FirstAsync(n => n.Id == aNotif);
        entity.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllRead_only_affects_current_user()
    {
        await SeedNotificationAsync(_userAId, "a-1");
        await SeedNotificationAsync(_userAId, "a-2");
        await SeedNotificationAsync(_userBId, "b-1");

        await AuthenticateAsync("notif_a", "Notif@123");
        var res = await _client.PostAsync("/api/notifications/mark-all-read", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var aUnread = await db.Notifications.CountAsync(n => n.UserId == _userAId && !n.IsRead);
        var bUnread = await db.Notifications.CountAsync(n => n.UserId == _userBId && !n.IsRead);
        aUnread.Should().Be(0);
        bUnread.Should().Be(1);
    }
}
