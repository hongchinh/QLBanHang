using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

public interface IInventoryMetricsService
{
    Task<InventorySummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
}
