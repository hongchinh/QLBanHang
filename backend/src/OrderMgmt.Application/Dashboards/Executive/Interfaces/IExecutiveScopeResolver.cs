using OrderMgmt.Application.Dashboards.Executive.Common;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

/// The branches a dashboard request reads: the one it names, else every branch the user may see (P7).
public interface IExecutiveScopeResolver
{
    Task<ExecutiveScope> ResolveAsync(Guid? branchId, CancellationToken ct = default);
}
