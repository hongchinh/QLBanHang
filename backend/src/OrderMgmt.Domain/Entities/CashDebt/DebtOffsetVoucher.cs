using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// Chứng từ bù trừ công nợ (P23): offsets a partner's receivable documents against their payable documents. Its two
/// Decrease entries (R− and P− of Amount) and their Offset settlements live in the debt ledger. Create, cancel and
/// delete only.
public class DebtOffsetVoucher : BaseEntity
{
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }

    public Guid BranchId { get; set; }

    public Guid PartnerId { get; set; }
    public Customer? Partner { get; set; }

    /// Snapshot of the partner's name at creation.
    public string PartnerName { get; set; } = default!;

    /// Σ receivable items = Σ payable items (computed on the server).
    public decimal Amount { get; set; }

    public string? Note { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }

    public Guid OwnerUserId { get; set; }
    public User? Owner { get; set; }

    /// PostgreSQL xmin.
    public uint Version { get; set; }
}
