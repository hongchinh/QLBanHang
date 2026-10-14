using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

public interface IDebtMetricsService
{
    Task<DebtSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<DebtBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);

    /// The receivable balance per branch of the scope at the end of `at`. No permission check: the caller masks.
    Task<IReadOnlyDictionary<Guid, decimal>> GetReceivableByBranchAsync(ExecutiveScope scope, DateOnly at,
        CancellationToken ct = default);
}
