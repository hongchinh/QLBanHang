using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Models;

public class UpsertStockVoucherRequest
{
    public StockDirection Type { get; set; }            // create: chooses type; update: must equal stored type
    public DateTimeOffset VoucherAt { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ReasonId { get; set; }
    public Guid? PartnerId { get; set; }
    public string? PartnerName { get; set; }            // null → partner.Name
    public string? PartnerAddress { get; set; }         // null → partner.CompanyAddress
    public string? PartnerTaxCode { get; set; }         // null → partner.TaxCode
    public string? HandlerName { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? Note { get; set; }
    public decimal Freight { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal? PaidAmount { get; set; }            // null → Total
    public uint? Version { get; set; }                  // required on update
    public bool AcknowledgeNegativeStock { get; set; }
    public List<UpsertStockVoucherLineRequest> Lines { get; set; } = new();
}

public class UpsertStockVoucherLineRequest
{
    public Guid? Id { get; set; }
    public int SortOrder { get; set; }
    public Guid ProductId { get; set; }
    public Guid? WarehouseId { get; set; }              // null → header warehouse
    public decimal? SheetCount { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal Quantity { get; set; }               // PerUnit only; others are computed
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public bool DiscountManual { get; set; }
    public decimal VatRate { get; set; }
    public string? Note { get; set; }
}

public class StockVoucherActionRequest
{
    public uint? Version { get; set; }
    public bool AcknowledgeNegativeStock { get; set; }
}

public class StockVoucherDto
{
    public Guid Id { get; set; }
    public StockDirection Type { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public Guid BranchId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
    public Guid? PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerAddress { get; set; }
    public string? PartnerTaxCode { get; set; }
    public string? HandlerName { get; set; }
    public Guid ReasonId { get; set; }
    public string? ReasonName { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? Note { get; set; }
    public decimal Freight { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal GoodsAmount { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public StockVoucherStatus Status { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCancel { get; set; }
    public bool CanDelete { get; set; }
    public List<StockVoucherLineDto> Lines { get; set; } = new();
}

public class StockVoucherLineDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
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
    public string? Note { get; set; }
}

public class StockVoucherListRequest : PageRequest
{
    public StockDirection Type { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? ReasonId { get; set; }
    public StockVoucherStatus? Status { get; set; }
    public string? OwnerUserIds { get; set; }
}

public class StockVoucherListItemDto
{
    public Guid Id { get; set; }
    public StockDirection Type { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public string? WarehouseName { get; set; }
    public string? PartnerName { get; set; }
    public string? ReasonName { get; set; }
    public string? PaymentMethodName { get; set; }
    public decimal GoodsAmount { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal Freight { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public StockVoucherStatus Status { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class StockVoucherListAggregates
{
    public decimal GoodsAmount { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal Freight { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
}

public class StockVoucherListResult : PagedResult<StockVoucherListItemDto>
{
    public StockVoucherListAggregates Aggregates { get; init; } = new();
}

public class StockVoucherOwnerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
}

public class StockVoucherActivityDto
{
    public Guid Id { get; set; }
    public StockVoucherActivityAction Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Description { get; set; } = default!;
}

public class StockVoucherDefaultsDto
{
    public string NextCode { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ReasonId { get; set; }
    public Guid? PaymentMethodId { get; set; }
}

public class StockAtRequest
{
    public StockDirection Type { get; set; }
    public DateTimeOffset At { get; set; }
    public Guid? ExcludeVoucherId { get; set; }
    public List<StockAtItem> Items { get; set; } = new();
}

public class StockAtItem
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
}

public class StockAtResult
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
}
