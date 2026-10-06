using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

/// Product fields are snapshots taken when the line is saved.
public class StockVoucherLine : BaseEntity
{
    public Guid StockVoucherId { get; set; }
    public StockVoucher? StockVoucher { get; set; }

    public int SortOrder { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductCode { get; set; } = default!;
    public string ProductName { get; set; } = default!;

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public bool TrackInventory { get; set; }
    public PricingMode PricingMode { get; set; }
    public string UnitName { get; set; } = default!;
    public bool PriceIncludesVat { get; set; }

    public decimal? SheetCount { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool DiscountManual { get; set; }
    public decimal OrderDiscountAllocated { get; set; }
    public decimal FreightAllocated { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal NetAmount { get; set; }

    /// Net′ + allocated freight, without VAT (D6).
    public decimal InboundValue { get; set; }

    public string? Note { get; set; }
}
