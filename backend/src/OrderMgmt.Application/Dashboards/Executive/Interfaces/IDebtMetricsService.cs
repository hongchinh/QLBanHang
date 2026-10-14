using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

public interface IDebtMetricsService
{
    Task<DebtSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<DebtBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);
}
