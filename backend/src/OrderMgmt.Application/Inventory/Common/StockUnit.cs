using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Common;

/// Unit of the stock quantity (D24): dimension-priced products are counted in m, m² or m³.
public static class StockUnit
{
    public static string NameFor(PricingMode mode, string? unitName) => mode switch
    {
        PricingMode.PerLinearMeter => "m",
        PricingMode.PerSquareMeter => "m²",
        PricingMode.PerCubicMeter => "m³",
        _ => unitName ?? "",
    };
}
