using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Catalog;

/// The billable quantity of a line, shared by quotations and stock vouchers.
public static class PricingQuantity
{
    // Dimensions in mm. PerUnit returns `quantity` unchanged; missing dimensions count as 0.
    public static decimal Compute(PricingMode mode, decimal? sheetCount, decimal? length,
        decimal? width, decimal? thickness, decimal quantity)
    {
        var l = length ?? 0m;
        var w = width ?? 0m;
        var t = thickness ?? 0m;
        var sheets = sheetCount ?? 0m;

        return mode switch
        {
            PricingMode.PerLinearMeter => l * sheets / 1000m,
            PricingMode.PerSquareMeter => l * w * sheets / 1_000_000m,
            PricingMode.PerCubicMeter => l * w * t * sheets / 1_000_000_000m,
            _ => quantity,
        };
    }
}
