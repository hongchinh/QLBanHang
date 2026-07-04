using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Sales.Quotations.Models;

public enum QuotationAction
{
    Send = 1,
    Confirm = 2,
    AccountingConfirm = 3,
    Cancel = 9,
}

public class QuotationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public DateOnly QuotationDate { get; set; }

    public Guid OwnerUserId { get; set; }
    public string? OwnerFullName { get; set; }
    public bool IsOwnerDeleted { get; set; }

    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string? CustomerTaxCode { get; set; }
    public string? CustomerAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    public string? DeliveryAddress { get; set; }
    public string? DeliveryRecipient { get; set; }
    public string? DeliveryPhone { get; set; }
    public string? TransportVehicleNumber { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? DeliveryNote { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Freight { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AdvancePayment { get; set; }

    // Cost / profit fields are redacted to null when the caller lacks
    // `quotations.view_cost`. Treat null as "not authorized to see", not "zero".
    public decimal? TotalCost { get; set; }
    public decimal? GrossProfit { get; set; }

    public QuotationStatus Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? AccountingConfirmedAt { get; set; }
    public Guid? AccountingConfirmedByUserId { get; set; }
    public string? AccountingConfirmedByName { get; set; }
    public string? InternalNote { get; set; }

    public bool CanEdit { get; set; }
    public bool CanClone { get; set; }

    public List<QuotationLineDto> Lines { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}

public class QuotationActivityDto
{
    public Guid Id { get; set; }
    public Guid QuotationId { get; set; }
    public QuotationActivityAction Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Description { get; set; } = default!;
    public string? MetadataJson { get; set; }
}

public class QuotationLineDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }

    public Guid? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductGroupCode { get; set; }
    public string? ProductGroupName { get; set; }
    public int? ProductGroupSortOrder { get; set; }
    public string ProductName { get; set; } = default!;
    public string? Specification { get; set; }
    public string UnitName { get; set; } = default!;
    public PricingMode PricingMode { get; set; }

    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal? Density { get; set; }
    public decimal? SheetCount { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? LineCost { get; set; }
    public decimal? LineProfit { get; set; }

    public string? Note { get; set; }
}

public class QuotationListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public DateOnly QuotationDate { get; set; }
    public string CustomerName { get; set; } = default!;
    public string? ContactPhone { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Freight { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AdvancePayment { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? GrossProfit { get; set; }
    public QuotationStatus Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? AccountingConfirmedAt { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerFullName { get; set; }
    public bool IsOwnerDeleted { get; set; }
    public bool CanClone { get; set; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? RevenueDate { get; set; }
}

public class QuotationListAggregates
{
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Freight { get; set; }
    public decimal Total { get; set; }
    public decimal AdvancePayment { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? GrossProfit { get; set; }
}

public class QuotationListResult : PagedResult<QuotationListItemDto>
{
    public QuotationListAggregates Aggregates { get; init; } = new();
}

public class UpsertQuotationRequest
{
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public DateOnly QuotationDate { get; set; }

    public string? DeliveryAddress { get; set; }
    public string? DeliveryRecipient { get; set; }
    public string? DeliveryPhone { get; set; }
    public string? TransportVehicleNumber { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? DeliveryNote { get; set; }

    public decimal TaxRate { get; set; }
    public decimal Discount { get; set; }
    public decimal Freight { get; set; }
    public decimal AdvancePayment { get; set; }
    public string? InternalNote { get; set; }

    public IReadOnlyList<UpsertQuotationLineRequest> Lines { get; set; } = Array.Empty<UpsertQuotationLineRequest>();
}

public class UpsertQuotationLineRequest
{
    public Guid? Id { get; set; }
    public int SortOrder { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string ProductName { get; set; } = default!;
    public string? Specification { get; set; }
    public string UnitName { get; set; } = default!;
    public PricingMode PricingMode { get; set; } = PricingMode.PerUnit;

    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal? Density { get; set; }
    public decimal? SheetCount { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Note { get; set; }
}

public class QuotationListRequest : PageRequest
{
    // Comma-separated list of QuotationStatus values, e.g. "Draft,Sent".
    // Backward compatible: single value "Draft" still works (split yields a 1-element list).
    public string? Status { get; set; }
    public Guid? CustomerId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    // CSV "guid1,guid2"; honored only when caller has quotations.view_all (silently ignored otherwise).
    public string? OwnerUserIds { get; set; }
    public DateOnly? RevenueDateFrom { get; set; }
    public DateOnly? RevenueDateTo { get; set; }
    public DateOnly? DeliveryDateFrom { get; set; }
    public DateOnly? DeliveryDateTo { get; set; }
}

public class TransitionQuotationRequest
{
    public QuotationAction Action { get; set; }
}

public class TransferOwnerRequest
{
    public Guid NewOwnerUserId { get; set; }
    public string? Reason { get; set; }
}

public class QuotationOwnerOptionDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public int QuotationCount { get; set; }
}
