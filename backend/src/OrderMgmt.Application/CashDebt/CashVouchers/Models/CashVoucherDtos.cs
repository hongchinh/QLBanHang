using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.CashVouchers.Models;

public class UpsertCashVoucherRequest
{
    public CashVoucherType Type { get; set; }           // create: chooses type; update: must equal stored type
    public DateTimeOffset VoucherAt { get; set; }
    public Guid FundId { get; set; }
    public Guid ReasonId { get; set; }
    public Guid? PartnerId { get; set; }
    public string? PartnerName { get; set; }            // null → partner.Name
    public string? PartnerAddress { get; set; }         // null → partner.CompanyAddress
    public string? PartnerTaxCode { get; set; }         // null → partner.TaxCode
    public string? PersonName { get; set; }
    public string? Description { get; set; }
    public string? AttachedDocs { get; set; }
    public string? Note { get; set; }
    public uint? Version { get; set; }                  // required on update
    public bool ConfirmRemoveSettlements { get; set; }
    public bool AcknowledgeNegativeFund { get; set; }
    public List<UpsertCashVoucherLineRequest> Lines { get; set; } = new();
    public List<AllocationRequest>? Allocations { get; set; }   // null on update: keep the AtVoucher settlements
}

public class UpsertCashVoucherLineRequest
{
    public Guid? Id { get; set; }
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class AllocationRequest
{
    public Guid EntryId { get; set; }
    public decimal Amount { get; set; }
}

public class CashDocumentActionRequest
{
    public uint? Version { get; set; }
    public bool ConfirmRemoveSettlements { get; set; }
    public bool AcknowledgeNegativeFund { get; set; }
}

public class CashVoucherDto
{
    public Guid Id { get; set; }
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public Guid BranchId { get; set; }
    public Guid FundId { get; set; }
    public string FundCode { get; set; } = default!;
    public string FundName { get; set; } = default!;
    public FundKind FundKind { get; set; }
    public Guid ReasonId { get; set; }
    public string ReasonCode { get; set; } = default!;
    public string ReasonName { get; set; } = default!;
    public DebtEffect ReasonDebtEffect { get; set; }
    public Guid? PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerAddress { get; set; }
    public string? PartnerTaxCode { get; set; }
    public string? PersonName { get; set; }
    public string? Description { get; set; }
    public string? AttachedDocs { get; set; }
    public string? Note { get; set; }
    public decimal Total { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public uint Version { get; set; }
    public Guid? SourceStockVoucherId { get; set; }
    public string? SourceStockVoucherCode { get; set; }
    public StockDirection? SourceStockVoucherType { get; set; }
    public bool IsManaged { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCancel { get; set; }
    public bool CanDelete { get; set; }
    public List<CashVoucherLineDto> Lines { get; set; } = new();
    public List<CashVoucherAllocationDto> Allocations { get; set; } = new();
}

public class CashVoucherLineDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class CashVoucherAllocationDto
{
    public Guid SettlementId { get; set; }
    public Guid EntryId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public decimal Amount { get; set; }
    public SettlementOrigin Origin { get; set; }
}

public class CashVoucherListRequest : PageRequest
{
    public CashVoucherType Type { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? FundId { get; set; }
    public Guid? ReasonId { get; set; }
    public Guid? PartnerId { get; set; }
    public DocumentStatusFilter Status { get; set; } = DocumentStatusFilter.Active;
    public string? OwnerIds { get; set; }               // comma-separated, like stock vouchers
}

public class CashVoucherListItemDto
{
    public Guid Id { get; set; }
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public string FundCode { get; set; } = default!;
    public string FundName { get; set; } = default!;
    public string ReasonName { get; set; } = default!;
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public string? PersonName { get; set; }
    public string? Description { get; set; }
    public decimal Total { get; set; }
    public DocumentStatus Status { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public Guid? SourceStockVoucherId { get; set; }
    public string? SourceStockVoucherCode { get; set; }
    public bool IsManaged { get; set; }
}

/// `TotalAmount` = Σ Total of the whole filtered set, not only the page.
public class CashVoucherListResult : PagedResult<CashVoucherListItemDto>
{
    public decimal TotalAmount { get; set; }
}

public class CashVoucherDefaultsDto
{
    public string NextCode { get; set; } = default!;    // peek: the counter is not used
    public DateTimeOffset VoucherAt { get; set; }
    public Guid? FundId { get; set; }                   // default Cash fund of the working branch
    public Guid? ReasonId { get; set; }                 // TKH (receipt) or TNCC (payment)
}

public class CashDocumentActivityDto
{
    public Guid Id { get; set; }
    public CashDocumentActivityAction Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Description { get; set; } = default!;
}
