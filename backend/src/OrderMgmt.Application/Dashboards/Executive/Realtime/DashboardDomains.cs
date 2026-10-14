namespace OrderMgmt.Application.Dashboards.Executive.Realtime;

/// The groups of dashboard figures a saved document can change. A signal names domains, never figures.
[Flags]
public enum DashboardDomains
{
    None = 0,
    Sales = 1,
    Cash = 2,
    Debt = 4,
    Inventory = 8,
    All = Sales | Cash | Debt | Inventory,
}
