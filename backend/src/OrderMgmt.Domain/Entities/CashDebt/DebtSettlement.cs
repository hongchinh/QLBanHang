using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// Matches part of an Increase entry against a Decrease entry of the same branch, partner and side.
/// Derived data: not a BaseEntity, hard-deleted.
public class DebtSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncreaseEntryId { get; set; }
    public Guid DecreaseEntryId { get; set; }
    public decimal Amount { get; set; }
    public SettlementOrigin Origin { get; set; }
    public Guid BatchId { get; set; }

    /// max(PostedAt) of the two entries (UTC).
    public DateTimeOffset EffectiveAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
