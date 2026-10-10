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
