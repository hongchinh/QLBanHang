using Microsoft.AspNetCore.SignalR;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Notifications.Interfaces;

namespace OrderMgmt.WebApi.Hubs;

public class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotifier(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task NotifyUserAsync(Guid userId, CancellationToken ct = default)
        => _hub.Clients.Group($"user-{userId}")
               .SendAsync("NewNotification", cancellationToken: ct);

    /// One event for the users who see every branch and the users of this branch. The payload names the branch and
    /// the domains only; the clients fetch the figures themselves.
    public Task NotifyDashboardChangedAsync(Guid branchId, DashboardDomains domains, CancellationToken ct = default)
    {
        if (domains == DashboardDomains.None)
            return Task.CompletedTask;

        return _hub.Clients.Groups(DashboardGroups.All, DashboardGroups.ForBranch(branchId))
            .SendAsync(DashboardGroups.EventName, new { branchId, domains = DashboardGroups.DomainNames(domains) }, ct);
    }
}
