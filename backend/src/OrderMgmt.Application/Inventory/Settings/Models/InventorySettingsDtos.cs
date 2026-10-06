using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Settings.Models;

public class InventorySettingsDto
{
    public CostingMethod CostingMethod { get; set; }
    public CostingPeriod CostingPeriod { get; set; }
    public CostingScope CostingScope { get; set; }
    public bool PurchaseCostIncludesVat { get; set; }
    public NegativeStockPolicy NegativeStockPolicy { get; set; }
    public bool NetExcludesVat { get; set; }
    public DefaultDateMode DefaultDateMode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class UpdateInventorySettingsRequest
{
    public CostingMethod CostingMethod { get; set; }
    public CostingPeriod CostingPeriod { get; set; }
    public CostingScope CostingScope { get; set; }
    public bool PurchaseCostIncludesVat { get; set; }
    public NegativeStockPolicy NegativeStockPolicy { get; set; }
    public bool NetExcludesVat { get; set; }
    public DefaultDateMode DefaultDateMode { get; set; }
}
