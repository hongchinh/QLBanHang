using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Reports.Models;

// Every member is a public auto-property: System.Text.Json ignores public fields.

/// Số dư công nợ. From/To are VN dates (From <= To); opening = entries before StartOfDay(From) plus every Opening
/// entry (P29), period = the other entries up to StartOfNextDay(To).
public class DebtBalanceReportRequest
{
    public DebtSide Side { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public Guid? PartnerId { get; set; }

    /// Matches the partner code or name.
    public string? Search { get; set; }
}

public class DebtBalanceReportDto
{
    public DebtSide Side { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// One row per partner with any non-zero value, ordered by PartnerCode.
    public List<DebtBalanceRowDto> Rows { get; set; } = new();

    /// Sum of each column; PartnerId, PartnerCode and PartnerName are null.
    public DebtBalanceRowDto Totals { get; set; } = new();
}

/// Opening and closing are split into Dư Nợ / Dư Có with DebtBalanceSign.Split (P24).
public class DebtBalanceRowDto
{
    public Guid? PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal Increase { get; set; }
    public decimal Decrease { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
}

/// Sổ chi tiết công nợ of one partner and side. Same window rule as the balance report.
public class DebtLedgerReportRequest
{
    public DebtSide Side { get; set; }
    public Guid PartnerId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

/// Opening and closing are split into Dư Nợ / Dư Có with DebtBalanceSign.Split (P24).
public class DebtLedgerReportDto
{
    public DebtSide Side { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerCode { get; set; } = default!;
    public string PartnerName { get; set; } = default!;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }

    /// Period entries ordered by PostedAt, SourceType, DocCode.
    public List<DebtLedgerRowDto> Rows { get; set; } = new();

    public decimal TotalIncrease { get; set; }
    public decimal TotalDecrease { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
}

public class DebtLedgerRowDto
{
    public Guid EntryId { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public DateOnly DocDate { get; set; }
    public string DocCode { get; set; } = default!;
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string? Description { get; set; }
    public decimal Increase { get; set; }
    public decimal Decrease { get; set; }

    /// Balance after this row: the opening balance plus Σ Increase − Σ Decrease up to and including it.
    public decimal RunningBalance { get; set; }
}
