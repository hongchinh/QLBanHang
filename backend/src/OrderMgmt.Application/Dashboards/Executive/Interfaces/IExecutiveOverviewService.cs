using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

/// The parts of the dashboard that read several domains; each part follows the user's permissions.
public interface IExecutiveOverviewService
{
    Task<List<BranchComparisonRowDto>> GetBranchComparisonAsync(ExecutiveQuery query, CancellationToken ct = default);
}
