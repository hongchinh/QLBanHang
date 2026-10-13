using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

public interface ISalesMetricsService
{
    Task<SalesSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<SalesSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default);
}
