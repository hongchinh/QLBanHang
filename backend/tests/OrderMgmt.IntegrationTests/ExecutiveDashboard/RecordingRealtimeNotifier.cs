using System.Collections.Concurrent;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Notifications.Interfaces;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// Stands in for the SignalR notifier of a test host and keeps the dashboard signals it is asked to send.
public sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public ConcurrentQueue<(Guid BranchId, DashboardDomains Domains)> DashboardSignals { get; } = new();

    /// When set, a dashboard signal fails instead of being recorded. It fails through the returned task, as a send
    /// fails, so a caller that does not await the task never sees the failure.
    public bool ThrowOnDashboardSignal { get; set; }

    /// When set, a dashboard signal never completes until its token is cancelled, as a send to a client that has
    /// stopped reading.
    public bool StallOnDashboardSignal { get; set; }

    /// How many quotation signals were asked for.
    private int _quotationSignals;
    public int QuotationSignals => _quotationSignals;

    /// When set, a quotation signal fails instead of being counted, through the returned task like a failed send.
    public bool ThrowOnQuotationSignal { get; set; }

    public Task NotifyUserAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;

    public Task NotifyDashboardChangedAsync(Guid branchId, DashboardDomains domains, CancellationToken ct = default)
    {
        if (ThrowOnDashboardSignal)
            return Task.FromException(new InvalidOperationException("The notifier is down."));
        if (StallOnDashboardSignal)
            return Task.Delay(Timeout.Infinite, ct);

        DashboardSignals.Enqueue((branchId, domains));
        return Task.CompletedTask;
    }

    public Task NotifyQuotationsChangedAsync(CancellationToken ct = default)
    {
        if (ThrowOnQuotationSignal)
            return Task.FromException(new InvalidOperationException("The notifier is down."));

        Interlocked.Increment(ref _quotationSignals);
        return Task.CompletedTask;
    }
}
