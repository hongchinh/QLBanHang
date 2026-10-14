using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

public interface IInventoryMetricsService
{
    Task<InventorySummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<InventoryBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);

    /// Stock value now per branch of the scope. No permission check: the caller masks.
    Task<IReadOnlyDictionary<Guid, decimal>> GetValueByBranchAsync(ExecutiveScope scope, CancellationToken ct = default);
}
