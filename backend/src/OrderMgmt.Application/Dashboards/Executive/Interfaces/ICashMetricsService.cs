using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

public interface ICashMetricsService
{
    Task<CashSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
}
