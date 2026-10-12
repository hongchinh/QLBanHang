using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// One side of a document's effect on a partner's debt. Derived data: not a BaseEntity, hard-deleted,
/// at most one row per (SourceType, SourceId, Side), updated in place on every save of the source.
public class DebtEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
    public DebtDirection Direction { get; set; }
    public decimal Amount { get; set; }

    /// UTC instant of the source document.
    public DateTimeOffset PostedAt { get; set; }

    /// VN date of the source document.
    public DateOnly DocDate { get; set; }

    /// Set on Increase rows (DebtDates.EntryDueDate), null on Decrease rows.
    public DateOnly? DueDate { get; set; }

    public Guid? OwnerUserId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
