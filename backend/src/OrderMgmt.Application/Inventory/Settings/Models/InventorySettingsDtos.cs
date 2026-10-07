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

public class DocumentNumberingDto
{
    public DocumentType DocType { get; set; }
    public string Prefix { get; set; } = default!;
    public int Length { get; set; }
    public NumberingResetPolicy ResetPolicy { get; set; }
    public string Pattern { get; set; } = default!;
}

public class UpdateNumberingRequest
{
    public string Prefix { get; set; } = default!;
    public int Length { get; set; }
    public NumberingResetPolicy ResetPolicy { get; set; }
    public string Pattern { get; set; } = default!;
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
