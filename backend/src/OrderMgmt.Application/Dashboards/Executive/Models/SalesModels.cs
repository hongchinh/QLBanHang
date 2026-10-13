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
