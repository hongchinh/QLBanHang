using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderMgmt.Application.Notifications.Interfaces;

namespace OrderMgmt.WebApi.Filters;

/// Marks a controller whose every non-GET action writes quotations: after each successful write the open overview
/// dashboards are told that quotations changed. The signal has no branch and no payload.
[AttributeUsage(AttributeTargets.Class)]
public sealed class QuotationSignalAttribute : TypeFilterAttribute
{
    public QuotationSignalAttribute() : base(typeof(QuotationSignalFilter))
    {
    }
}

public sealed class QuotationSignalFilter : IAsyncActionFilter
{
    /// How long the signal may hold the answer to a saved write before it is given up.
    private static readonly TimeSpan SendLimit = TimeSpan.FromSeconds(3);

    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<QuotationSignalFilter> _logger;

    public QuotationSignalFilter(IRealtimeNotifier notifier, ILogger<QuotationSignalFilter> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // The action runs first: when it returns, the service has committed.
        var executed = await next();
        if (!DashboardSignalFilter.IsSuccessfulWrite(context, executed, skipAttribute: null))
            return;

        try
        {
            // The request's token is left out on purpose, as in DashboardSignalFilter: the quotation is saved.
            using var limit = new CancellationTokenSource(SendLimit);
            await _notifier.NotifyQuotationsChangedAsync(limit.Token);
        }
        catch (Exception ex)
        {
            // A signal that cannot be sent must not turn a saved quotation into an error answer.
            var request = context.HttpContext.Request;
            _logger.LogWarning(ex, "Could not send the quotations signal of {Method} {Path}.",
                request.Method, request.Path);
        }
    }
}
