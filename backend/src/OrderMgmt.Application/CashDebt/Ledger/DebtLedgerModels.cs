using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

/// The wanted state of one side of a source document. `PostedAt` is normalized to UTC by the ledger (D27).
public sealed record DebtEntryDraft(Guid BranchId, Guid PartnerId, DebtSide Side, DebtDirection Direction,
    decimal Amount, DateTimeOffset PostedAt, DateOnly DocDate, DateOnly? DueDate, Guid? OwnerUserId,
    string DocCode, string? Description);

public sealed record SettlementDraft(Guid IncreaseEntryId, Guid DecreaseEntryId, decimal Amount);

public sealed record RemovedSettlement(Guid SettlementId, Guid IncreaseEntryId, Guid DecreaseEntryId, decimal Amount,
    SettlementOrigin Origin);

/// Open items of one partner and side. With ForSourceType + ForSourceId (P26), that source's entries are left out
/// and its own AtVoucher settlements are added back to the other entries' open amounts.
public sealed class OpenItemsQuery
{
    public Guid BranchId { get; set; }
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
    public DebtDirection? Direction { get; set; }
    public DebtSourceType? ForSourceType { get; set; }
    public Guid? ForSourceId { get; set; }
}

public sealed class OpenItemDto
{
    public Guid EntryId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public DebtDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal Settled { get; set; }
    public decimal Open { get; set; }
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
}
