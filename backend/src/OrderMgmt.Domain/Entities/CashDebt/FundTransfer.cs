using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// Phiếu chuyển quỹ: moves money between two funds of the same branch. Never touches debt.
public class FundTransfer : BaseEntity
{
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }

    public Guid BranchId { get; set; }

    public Guid FromFundId { get; set; }
    public Fund? FromFund { get; set; }

    public Guid ToFundId { get; set; }
    public Fund? ToFund { get; set; }

    public decimal Amount { get; set; }

    public string? Description { get; set; }
    public string? Note { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }

    public Guid OwnerUserId { get; set; }
    public User? Owner { get; set; }

    /// PostgreSQL xmin.
    public uint Version { get; set; }
}
