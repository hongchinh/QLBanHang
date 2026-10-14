using OrderMgmt.Application.Dashboards.Executive.Realtime;

namespace OrderMgmt.WebApi.Hubs;

/// Names of the "dashboard changed" signal: its event, the hub groups it is sent to and the domain names it carries.
public static class DashboardGroups
{
    public const string EventName = "DashboardChanged";

    /// The group of the users who may see every branch.
    public const string All = "dashboard-all";

    // The order the names are sent in.
    private static readonly (DashboardDomains Domain, string Name)[] Names =
    {
        (DashboardDomains.Sales, "sales"),
        (DashboardDomains.Cash, "cash"),
        (DashboardDomains.Debt, "debt"),
        (DashboardDomains.Inventory, "inventory"),
    };

    public static string ForBranch(Guid branchId) => $"dashboard-branch-{branchId}";

    /// The group a connection joins: null without dashboard.executive; All with branches.access_all;
    /// otherwise the group of the user's default branch (null when it is unknown).
    public static string? GroupFor(bool hasDashboard, bool accessAll, Guid? defaultBranchId)
    {
        if (!hasDashboard)
            return null;
        if (accessAll)
            return All;
        return defaultBranchId is { } branchId ? ForBranch(branchId) : null;
    }

    /// "sales", "cash", "debt", "inventory" for the flags that are set, in that order.
    public static string[] DomainNames(DashboardDomains domains) =>
        Names.Where(n => domains.HasFlag(n.Domain)).Select(n => n.Name).ToArray();
}
