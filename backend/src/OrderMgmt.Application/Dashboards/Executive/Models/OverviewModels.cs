namespace OrderMgmt.Application.Dashboards.Executive.Models;

public class BranchComparisonRowDto
{
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;

    /// Period; null without reports.profit.
    public decimal? Revenue { get; set; }

    /// Null without reports.profit or inventory.view_cost.
    public decimal? MarginPct { get; set; }

    /// Balance at At; null without reports.debt.
    public decimal? Receivable { get; set; }

    /// Now; null without reports.inventory or inventory.view_cost.
    public decimal? StockValue { get; set; }
}

public class AttentionDto
{
    public DateOnly At { get; set; }

    /// Count = customers, Amount = overdue amount.
    public AttentionItemDto? OverdueReceivables { get; set; }

    /// Count = customer × branch pairs, Amount = Σ Over.
    public AttentionItemDto? OverCreditLimit { get; set; }

    /// Count = documents due within 7 days, Amount.
    public AttentionItemDto? PayablesDueSoon { get; set; }

    /// Count = product × warehouse pairs, Amount = null.
    public AttentionItemDto? NegativeStock { get; set; }

    /// Count = funds, Amount = Σ |balance|.
    public AttentionItemDto? NegativeFunds { get; set; }
}

public class AttentionItemDto
{
    public int Count { get; set; }
    public decimal? Amount { get; set; }
}

public enum RecentDocumentKind
{
    Receipt = 1,
    Payment = 2,
    StockIn = 3,
    StockOut = 4,
}

public class RecentDocumentDto
{
    public RecentDocumentKind Kind { get; set; }
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string? PartnerName { get; set; }
    public string? OwnerName { get; set; }
    public decimal Amount { get; set; }
}
