using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

/// Singleton (Id = 1), seeded by migration.
public class InventorySettings
{
    public int Id { get; set; }
    public CostingMethod CostingMethod { get; set; } = CostingMethod.PeriodicAverage;
    public CostingPeriod CostingPeriod { get; set; } = CostingPeriod.Month;
    public CostingScope CostingScope { get; set; } = CostingScope.Branch;
    public bool PurchaseCostIncludesVat { get; set; } = true;
    public NegativeStockPolicy NegativeStockPolicy { get; set; } = NegativeStockPolicy.Warn;
    public bool NetExcludesVat { get; set; }
    public DefaultDateMode DefaultDateMode { get; set; } = DefaultDateMode.Now;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
