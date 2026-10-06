using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Constants;

namespace OrderMgmt.WebApi.Services;

public class CurrentBranch : ICurrentBranch
{
    public const string HeaderName = "X-Branch-Id";

    private readonly IHttpContextAccessor _accessor;
    private readonly ICurrentUser _currentUser;
    private readonly IAppDbContext _db;
    private Guid? _resolved;

    public CurrentBranch(IHttpContextAccessor accessor, ICurrentUser currentUser, IAppDbContext db)
    {
        _accessor = accessor;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<Guid> GetIdAsync(CancellationToken ct = default)
    {
        if (_resolved.HasValue) return _resolved.Value;

        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();

        if (_currentUser.HasPermission(Permissions.Branches.AccessAll)
            && Guid.TryParse(_accessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault(), out var requested)
            && await _db.Branches.AnyAsync(b => b.Id == requested, ct))
        {
            _resolved = requested;
            return requested;
        }

        // A soft-deleted user can still hold a valid token: the query filter hides the row → 401, not 500.
        var defaultBranchId = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => (Guid?)u.DefaultBranchId)
            .SingleOrDefaultAsync(ct)
            ?? throw new UnauthorizedAccessException();

        _resolved = defaultBranchId;
        return defaultBranchId;
    }
}
