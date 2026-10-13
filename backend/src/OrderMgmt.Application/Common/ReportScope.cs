using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;

namespace OrderMgmt.Application.Common;

/// Which branches a report reads: the working branch only, or every branch of the company.
public enum ReportScope { Branch = 0, Company = 1 }

public static class ReportScopeGuard
{
    /// Company needs branches.access_all (T7); Branch is always allowed.
    public static void EnsureAllowed(ICurrentUser user, ReportScope scope)
    {
        if (scope == ReportScope.Company && !user.HasPermission(Permissions.Branches.AccessAll))
            throw new ForbiddenException("Cần quyền làm việc ở mọi chi nhánh để xem toàn công ty.");
    }
}
