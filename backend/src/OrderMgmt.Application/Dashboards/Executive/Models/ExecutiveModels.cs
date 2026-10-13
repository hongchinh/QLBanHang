namespace OrderMgmt.Application.Dashboards.Executive.Models;

public class ExecutiveQuery
{
    /// Default: first day of To's month.
    public DateOnly? From { get; set; }

    /// Default: VN today.
    public DateOnly? To { get; set; }

    /// Omitted: every branch the user may see (P7).
    public Guid? BranchId { get; set; }

    /// As-at date of the balance figures, default VN today (P6).
    public DateOnly? At { get; set; }
}

public enum SeriesGranularity
{
    Day = 0,
    Week = 1,
    Month = 2,
}

public class ExecutiveSeriesQuery : ExecutiveQuery
{
    public SeriesGranularity Granularity { get; set; }
}

public enum SalesTopBy
{
    Customer = 0,
    Product = 1,
    Employee = 2,
}

public enum SalesTopSort
{
    Revenue = 0,
    GrossProfit = 1,
}

public class SalesTopQuery : ExecutiveQuery
{
    public SalesTopBy By { get; set; }
    public SalesTopSort Sort { get; set; }
    public int Limit { get; set; } = 10;
}

public class RecentQuery
{
    public Guid? BranchId { get; set; }
    public int Limit { get; set; } = 10;
}
