using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Infrastructure.Identity;

namespace OrderMgmt.WebApi.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly IAppDbContext _db;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(IAppDbContext db, ILogger<NotificationHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        if (await DashboardGroupAsync(userId) is { } dashboardGroup)
            await Groups.AddToGroupAsync(Context.ConnectionId, dashboardGroup);

        await base.OnConnectedAsync();
    }

    /// The group whose dashboard signals this connection gets; null when it gets none.
    private async Task<string?> DashboardGroupAsync(string? userId)
    {
        var hasDashboard = HasPermission(Permissions.Dashboard.Executive);
        var accessAll = HasPermission(Permissions.Branches.AccessAll);

        // Only a dashboard user limited to one branch has a group that depends on the default branch; nobody else
        // costs a query.
        Guid? defaultBranchId = null;
        if (hasDashboard && !accessAll && Guid.TryParse(userId, out var id))
            defaultBranchId = await DefaultBranchIdAsync(id);

        return DashboardGroups.GroupFor(hasDashboard, accessAll, defaultBranchId);
    }

    /// The user's default branch; null when the user is not found or the database cannot be read.
    private async Task<Guid?> DefaultBranchIdAsync(Guid userId)
    {
        try
        {
            return await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => (Guid?)u.DefaultBranchId)
                .SingleOrDefaultAsync(Context.ConnectionAborted);
        }
        catch (Exception ex)
        {
            // A failed lookup must not refuse the connection: the user keeps the notifications, and the dashboard
            // catches up with its periodic refresh.
            _logger.LogWarning(ex,
                "Could not read the default branch of user {UserId}; the connection gets no dashboard signal.", userId);
            return null;
        }
    }

    private bool HasPermission(string permission) =>
        Context.User?.HasClaim(JwtTokenGenerator.PermissionClaimType, permission) ?? false;
}
