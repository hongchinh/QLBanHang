using OrderMgmt.Application.Dashboards.Executive.Realtime;

namespace OrderMgmt.Application.Notifications.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyUserAsync(Guid userId, CancellationToken ct = default);

    /// Tells the open dashboards that figures of these domains changed in this branch. Carries no figures.
    Task NotifyDashboardChangedAsync(Guid branchId, DashboardDomains domains, CancellationToken ct = default);

    /// Tells the open overview dashboards that quotations changed. Carries nothing.
    Task NotifyQuotationsChangedAsync(CancellationToken ct = default);
}
