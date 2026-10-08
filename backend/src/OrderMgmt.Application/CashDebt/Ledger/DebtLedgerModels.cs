using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

/// The wanted state of one side of a source document. `PostedAt` is normalized to UTC by the ledger (D27).
public sealed record DebtEntryDraft(Guid BranchId, Guid PartnerId, DebtSide Side, DebtDirection Direction,
    decimal Amount, DateTimeOffset PostedAt, DateOnly DocDate, DateOnly? DueDate, Guid? OwnerUserId,
    string DocCode, string? Description);

public sealed record SettlementDraft(Guid IncreaseEntryId, Guid DecreaseEntryId, decimal Amount);

public sealed record RemovedSettlement(Guid SettlementId, Guid IncreaseEntryId, Guid DecreaseEntryId, decimal Amount,
    SettlementOrigin Origin);
