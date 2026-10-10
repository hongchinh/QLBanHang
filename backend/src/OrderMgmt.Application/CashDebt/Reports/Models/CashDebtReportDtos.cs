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

/// Sổ quỹ. FundId set → that fund only (it must match Kind unless Kind is All); otherwise every fund of the working
/// branch matching Kind, active or not. The fund's OpeningAmount is always opening balance (P29).
public class CashBookReportRequest
{
    public FundKindFilter Kind { get; set; } = FundKindFilter.All;
    public Guid? FundId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

/// GrandOpening = Σ Opening and GrandClosing = Σ Closing. GrandIn / GrandOut leave out a transfer whose two funds are
/// both selected (P25), so GrandOpening + GrandIn − GrandOut == GrandClosing.
public class CashBookReportDto
{
    public FundKindFilter Kind { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// One group per selected fund, ordered by Kind and Code.
    public List<CashBookGroupDto> Groups { get; set; } = new();

    public decimal GrandOpening { get; set; }
    public decimal GrandIn { get; set; }
    public decimal GrandOut { get; set; }
    public decimal GrandClosing { get; set; }
}

/// One fund. Opening = OpeningAmount + the movements before StartOfDay(From) (P17); the rows always include the
/// fund's transfer halves, so its running balance reconciles (P25).
public class CashBookGroupDto
{
    public Guid FundId { get; set; }
    public string FundCode { get; set; } = default!;
    public string FundName { get; set; } = default!;
    public FundKind FundKind { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public decimal Opening { get; set; }

    /// Ordered by At, inflows before outflows, then code.
    public List<CashBookRowDto> Rows { get; set; } = new();

    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
    public decimal Closing { get; set; }
}

public class CashBookRowDto
{
    public DateTimeOffset At { get; set; }
    public CashBookSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string? ReceiptCode { get; set; }
    public string? PaymentCode { get; set; }
    public string? TransferCode { get; set; }

    /// Partner name snapshot, else the person name.
    public string? PartnerOrPerson { get; set; }

    /// Receipt/payment: Description, else the reason name. Transfer: Description, else "Chuyển quỹ {from} → {to}".
    public string? Description { get; set; }

    public decimal In { get; set; }
    public decimal Out { get; set; }

    /// Balance after this row: the group opening plus Σ In − Σ Out up to and including it.
    public decimal Running { get; set; }
}
