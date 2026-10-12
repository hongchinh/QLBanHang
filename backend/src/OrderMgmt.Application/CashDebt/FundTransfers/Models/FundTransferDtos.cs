using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.FundTransfers.Models;

public class UpsertFundTransferRequest
{
    public DateTimeOffset VoucherAt { get; set; }
    public Guid FromFundId { get; set; }
    public Guid ToFundId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Note { get; set; }
    public uint? Version { get; set; }                  // required on update
    public bool AcknowledgeNegativeFund { get; set; }
}

public class FundTransferDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public Guid BranchId { get; set; }
    public Guid FromFundId { get; set; }
    public string FromFundCode { get; set; } = default!;
    public string FromFundName { get; set; } = default!;
    public FundKind FromFundKind { get; set; }
    public Guid ToFundId { get; set; }
    public string ToFundCode { get; set; } = default!;
    public string ToFundName { get; set; } = default!;
    public FundKind ToFundKind { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Note { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public uint Version { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCancel { get; set; }
    public bool CanDelete { get; set; }
}

public class FundTransferListRequest : PageRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? FundId { get; set; }                   // matches the source or the destination fund
    public DocumentStatusFilter Status { get; set; } = DocumentStatusFilter.Active;
    public string? OwnerIds { get; set; }               // comma-separated, like cash vouchers
}

public class FundTransferListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }
    public string FromFundCode { get; set; } = default!;
    public string FromFundName { get; set; } = default!;
    public string ToFundCode { get; set; } = default!;
    public string ToFundName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DocumentStatus Status { get; set; }
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
}

/// `TotalAmount` = Σ Amount of the whole filtered set, not only the page.
public class FundTransferListResult : PagedResult<FundTransferListItemDto>
{
    public decimal TotalAmount { get; set; }
}

public class FundTransferDefaultsDto
{
    public string NextCode { get; set; } = default!;    // peek: the counter is not used
    public DateTimeOffset VoucherAt { get; set; }
}
