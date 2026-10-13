using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

public interface ICashMetricsService
{
    Task<CashSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<CashSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default);
    Task<CashBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);
}
