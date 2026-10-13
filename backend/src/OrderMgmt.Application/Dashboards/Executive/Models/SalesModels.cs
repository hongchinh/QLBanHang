namespace OrderMgmt.Application.Dashboards.Executive.Models;

public class SalesSummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly PrevFrom { get; set; }
    public DateOnly PrevTo { get; set; }
    public decimal Revenue { get; set; }
    public decimal PrevRevenue { get; set; }
    public decimal? RevenueDeltaPct { get; set; }
    public bool CanViewCost { get; set; }

    /// Null without inventory.view_cost.
    public decimal? Cost { get; set; }

    /// Revenue − Cost.
    public decimal? GrossProfit { get; set; }

    /// GrossProfit / Revenue × 100.
    public decimal? MarginPct { get; set; }
    public decimal? PrevMarginPct { get; set; }
    public bool IsProvisional { get; set; }
}

public class SalesSeriesDto
{
    public SeriesGranularity Granularity { get; set; }
    public List<SalesPointDto> Points { get; set; } = new();
}

public class SalesPointDto
{
    /// Bucket start.
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
    public decimal? Cost { get; set; }
    public decimal? GrossProfit { get; set; }
}

public class SalesTopRowDto
{
    /// Partner, product or user id.
    public Guid Id { get; set; }

    /// Partner or product code; null for employees.
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal? GrossProfit { get; set; }
}
