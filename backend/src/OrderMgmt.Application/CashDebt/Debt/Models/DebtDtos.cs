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
