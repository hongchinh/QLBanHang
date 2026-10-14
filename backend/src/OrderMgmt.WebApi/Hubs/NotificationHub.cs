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

    public NotificationHub(IAppDbContext db) => _db = db;

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

    /// The user's default branch; null when the user is not found or the client has gone away.
    /// A failed read is not caught: a connection left without its dashboard group would look live to the client and
    /// never get a signal. SignalR logs the error and ends the connection, and the client starts it again.
    private async Task<Guid?> DefaultBranchIdAsync(Guid userId)
    {
        try
        {
            return await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => (Guid?)u.DefaultBranchId)
                .SingleOrDefaultAsync(Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // The client left while connecting: nothing went wrong, and there is no connection to put in a group.
            return null;
        }
    }

    private bool HasPermission(string permission) =>
        Context.User?.HasClaim(JwtTokenGenerator.PermissionClaimType, permission) ?? false;
}
