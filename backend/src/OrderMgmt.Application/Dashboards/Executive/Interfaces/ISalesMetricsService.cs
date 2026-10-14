using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

/// Revenue and cost of sales; a return counts negative on both.
public sealed record SalesTotals(decimal Revenue, decimal Cost);

public interface ISalesMetricsService
{
    Task<SalesSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<SalesSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default);
    Task<List<SalesTopRowDto>> GetTopAsync(SalesTopQuery query, CancellationToken ct = default);

    /// Revenue and cost per branch of the scope in the period. No permission check: the caller masks.
    Task<IReadOnlyDictionary<Guid, SalesTotals>> GetByBranchAsync(ExecutiveScope scope, ExecutivePeriod period,
        CancellationToken ct = default);
}
