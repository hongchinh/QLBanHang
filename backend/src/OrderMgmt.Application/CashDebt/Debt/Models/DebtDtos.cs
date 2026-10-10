using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Debt.Models;

/// Debt panel of the stock-out form (P6): the partner's receivable position in the working branch.
public sealed class PartnerDebtSummaryDto
{
    public Guid PartnerId { get; set; }

    /// Σ Increase − Σ Decrease of the receivable side, all dates, working branch.
    public decimal ReceivableBalance { get; set; }

    public decimal? CreditLimit { get; set; }

    /// CreditLimit − ReceivableBalance (may be negative); null when the partner has no limit in the branch.
    public decimal? Available { get; set; }

    /// Σ Open of receivable Increase entries with DueDate before today (VN date).
    public decimal OverdueAmount { get; set; }

    public int? CreditDays { get; set; }
}

/// GET /api/debt/open-items: open entries of one partner and side in the working branch. With ForSourceType and
/// ForSourceId (P26) that source's own entries are left out and their settlements are added back.
public sealed class OpenItemsRequest
{
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
    public DebtDirection? Direction { get; set; }
    public DebtSourceType? ForSourceType { get; set; }
    public Guid? ForSourceId { get; set; }
}

/// GET /api/debt/partners: Side Receivable → customers, Payable → suppliers; BothRoles → partners with both roles;
/// neither → any role.
public sealed class DebtPartnerSearchRequest
{
    public DebtSide? Side { get; set; }
    public bool BothRoles { get; set; }
    public string? Keyword { get; set; }
    public int Limit { get; set; } = 20;

    /// Same default as CustomerSearchRequest: inactive partners are included only when the caller asks
    /// (list filters, reports).
    public bool ActiveOnly { get; set; } = true;
}

/// GET /api/debt/settlements: every settlement between entries of one partner and side in the working branch.
public sealed class SettlementListRequest
{
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
}

/// POST /api/debt/settlements: one Manual batch (P22). Every item pairs an Increase and a Decrease entry of the
/// request's partner and side in the working branch.
public sealed class CreateSettlementBatchRequest
{
    public Guid PartnerId { get; set; }
    public DebtSide Side { get; set; }
    public List<SettlementItemRequest> Items { get; set; } = new();
}

public sealed class SettlementItemRequest
{
    public Guid IncreaseEntryId { get; set; }
    public Guid DecreaseEntryId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class SettlementBatchDto
{
    public Guid BatchId { get; set; }
    public List<SettlementDto> Settlements { get; set; } = new();
}

/// A settlement with the code, date and source of both its entries.
public sealed class SettlementDto
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public SettlementOrigin Origin { get; set; }
    public Guid IncreaseEntryId { get; set; }
    public string IncreaseDocCode { get; set; } = default!;
    public DateOnly IncreaseDocDate { get; set; }
    public DebtSourceType IncreaseSourceType { get; set; }
    public Guid IncreaseSourceId { get; set; }
    public Guid DecreaseEntryId { get; set; }
    public string DecreaseDocCode { get; set; } = default!;
    public DateOnly DecreaseDocDate { get; set; }
    public DebtSourceType DecreaseSourceType { get; set; }
    public Guid DecreaseSourceId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
}
