using System.Collections.Concurrent;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Notifications.Interfaces;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// Stands in for the SignalR notifier of a test host and keeps the dashboard signals it is asked to send.
public sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public ConcurrentQueue<(Guid BranchId, DashboardDomains Domains)> DashboardSignals { get; } = new();

    /// When set, a dashboard signal fails instead of being recorded.
    public bool ThrowOnDashboardSignal { get; set; }

    public Task NotifyUserAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;

    public Task NotifyDashboardChangedAsync(Guid branchId, DashboardDomains domains, CancellationToken ct = default)
    {
        if (ThrowOnDashboardSignal)
            throw new InvalidOperationException("The notifier is down.");

        DashboardSignals.Enqueue((branchId, domains));
        return Task.CompletedTask;
    }
}
