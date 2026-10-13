using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

public class ExecutiveScopeResolver : IExecutiveScopeResolver
{
    private readonly IAppDbContext _db;

    public ExecutiveScopeResolver(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ExecutiveScope> ResolveAsync(Guid? branchId, CancellationToken ct = default)
    {
        if (branchId is { } id)
            return new ExecutiveScope(new[] { id }, false);

        var branchIds = await _db.Branches.AsNoTracking()
            .OrderBy(b => b.Code)
            .Select(b => b.Id)
            .ToListAsync(ct);
        return new ExecutiveScope(branchIds, true);
    }
}
