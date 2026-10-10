using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.DebtOffsets.Models;

/// Each item is an open Increase entry of the partner on that side; Σ receivables must equal Σ payables (> 0).
public class CreateDebtOffsetRequest
{
    public DateTimeOffset VoucherAt { get; set; }
    public Guid PartnerId { get; set; }
    public string? Note { get; set; }
    public List<AllocationRequest> Receivables { get; set; } = new();
    public List<AllocationRequest> Payables { get; set; } = new();
}

public class DebtOffsetDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string PartnerName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public uint Version { get; set; }
    public bool CanCancel { get; set; }
    public bool CanDelete { get; set; }

    /// The documents offset by the voucher (its Offset settlements). Empty once cancelled: cancelling removes them (P23).
    public List<DebtOffsetItemDto> Items { get; set; } = new();
}

/// One offset document: the entry settled against the offset's own entry of that side.
public class DebtOffsetItemDto
{
    public DebtSide Side { get; set; }
    public Guid EntryId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public decimal Amount { get; set; }
}

public class DebtOffsetListRequest : PageRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? PartnerId { get; set; }
    public DocumentStatusFilter Status { get; set; } = DocumentStatusFilter.Active;
}

public class DebtOffsetListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public string? PartnerCode { get; set; }
    public string PartnerName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public DocumentStatus Status { get; set; }
    public string? OwnerName { get; set; }
}

/// `TotalAmount` = Σ Amount of the whole filtered set, not only the page.
public class DebtOffsetListResult : PagedResult<DebtOffsetListItemDto>
{
    public decimal TotalAmount { get; set; }
}

public class DebtOffsetDefaultsDto
{
    public string NextCode { get; set; } = default!;    // peek: the counter is not used
    public DateTimeOffset VoucherAt { get; set; }
}
