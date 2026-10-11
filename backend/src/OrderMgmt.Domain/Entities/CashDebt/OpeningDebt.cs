using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// One legacy document of a partner's opening debt in a branch, posted as one Opening debt entry at the
/// branch opening date (Debt → Increase, Prepayment → Decrease).
public class OpeningDebt : BaseEntity
{
    public Guid BranchId { get; set; }

    public Guid PartnerId { get; set; }
    public Customer? Partner { get; set; }

    public DebtSide Side { get; set; }
    public OpeningDebtKind Kind { get; set; }
    public string DocNo { get; set; } = default!;
    public DateOnly DocDate { get; set; }

    /// Debt only.
    public DateOnly? DueDate { get; set; }

    public decimal Amount { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? Note { get; set; }

    /// Optimistic concurrency token (Postgres xmin, D29).
    public uint Version { get; set; }
}
