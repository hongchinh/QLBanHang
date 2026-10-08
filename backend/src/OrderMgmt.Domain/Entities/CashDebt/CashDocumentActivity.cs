using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// Activity of a receipt, payment, fund transfer or debt offset voucher. Append-only: not a BaseEntity, never deleted.
public class CashDocumentActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// Receipt, Payment, FundTransfer or DebtOffset.
    public DocumentType DocumentType { get; set; }

    public Guid DocumentId { get; set; }
    public CashDocumentActivityAction Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Description { get; set; } = default!;
    public string? MetadataJson { get; set; }
}
