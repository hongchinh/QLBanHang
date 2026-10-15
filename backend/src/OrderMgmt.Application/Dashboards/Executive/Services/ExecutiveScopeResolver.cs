using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

public class ExecutiveScopeResolver : IExecutiveScopeResolver
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;

    public ExecutiveScopeResolver(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
    }

    public async Task<ExecutiveScope> ResolveAsync(Guid? branchId, CancellationToken ct = default)
    {
        if (_currentUser.HasPermission(Permissions.Branches.AccessAll))
        {
            if (branchId is not { } named)
            {
                var branchIds = await _db.Branches.AsNoTracking()
                    .Where(b => !b.IsDemo) // S15
                    .OrderBy(b => b.Code)
                    .Select(b => b.Id)
                    .ToListAsync(ct);
                return new ExecutiveScope(branchIds, true);
            }

            if (!await _db.Branches.AnyAsync(b => b.Id == named, ct))
                throw new NotFoundException("Branch", named);
            return new ExecutiveScope(new[] { named }, false);
        }

        // For these users the working branch is their default branch, whatever the X-Branch-Id header says.
        var own = await _currentBranch.GetIdAsync(ct);
        if (branchId is { } requested && requested != own)
            throw new ForbiddenException("Bạn không có quyền xem chi nhánh này.");
        return new ExecutiveScope(new[] { own }, false);
    }
}
