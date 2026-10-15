using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Notifications.Interfaces;

namespace OrderMgmt.WebApi.Filters;

/// Marks a controller whose writes change dashboard figures: after each successful write the open dashboards are
/// told which domains changed. The signal names the working branch, so the controller may only write documents of
/// the working branch. An action that is not a GET and changes nothing carries [SkipDashboardSignal].
[AttributeUsage(AttributeTargets.Class)]
public sealed class DashboardSignalAttribute : TypeFilterAttribute
{
    public DashboardSignalAttribute(DashboardDomains domains) : base(typeof(DashboardSignalFilter))
    {
        Domains = domains;
        Arguments = new object[] { domains };
    }

    public DashboardDomains Domains { get; }
}

/// Marks an action of a [DashboardSignal] controller that only reads although it is not a GET.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipDashboardSignalAttribute : Attribute
{
}

public sealed class DashboardSignalFilter : IAsyncActionFilter
{
    /// How long the signal may hold the answer to a saved write before it is given up.
    private static readonly TimeSpan SendLimit = TimeSpan.FromSeconds(3);

    private readonly IRealtimeNotifier _notifier;
    private readonly ICurrentBranch _branch;
    private readonly ILogger<DashboardSignalFilter> _logger;
    private readonly DashboardDomains _domains;

    public DashboardSignalFilter(IRealtimeNotifier notifier, ICurrentBranch branch,
        ILogger<DashboardSignalFilter> logger, DashboardDomains domains)
    {
        _notifier = notifier;
        _branch = branch;
        _logger = logger;
        _domains = domains;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // The action runs first: when it returns, the service has committed.
        var executed = await next();
        if (!IsSuccessfulWrite(context, executed, typeof(SkipDashboardSignalAttribute)))
            return;

        try
        {
            // The request's cancellation token is left out on purpose: the document is saved, so the dashboards are
            // told even when the caller has gone away. A time limit of its own keeps a client that has stopped
            // reading from holding the answer to the write.
            using var limit = new CancellationTokenSource(SendLimit);
            var branchId = await _branch.GetIdAsync(limit.Token);
            await _notifier.NotifyDashboardChangedAsync(branchId, _domains, limit.Token);
        }
        catch (Exception ex)
        {
            // A signal that cannot be sent must not turn a saved document into an error answer; the dashboards
            // catch up with their periodic refresh. The write is named so that a missing signal can be traced to it.
            var request = context.HttpContext.Request;
            _logger.LogWarning(ex, "Could not send the dashboard signal of {Method} {Path} for {Domains}.",
                request.Method, request.Path, _domains);
        }
    }

    /// Whether the action was a write that succeeded. An action marked with `skipAttribute` (when given) is not one.
    internal static bool IsSuccessfulWrite(ActionExecutingContext context, ActionExecutedContext executed,
        Type? skipAttribute)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            return false;

        if (skipAttribute is not null && context.ActionDescriptor.EndpointMetadata.Any(skipAttribute.IsInstanceOfType))
            return false;

        // Canceled: another filter answered in place of the action, which never ran.
        if (executed.Exception is not null || executed.Canceled)
            return false;

        // A result without a status code is answered with 200.
        return executed.Result is not IStatusCodeActionResult { StatusCode: >= 400 };
    }
}
