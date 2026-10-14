using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Models;

public class CashSummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly PrevFrom { get; set; }
    public DateOnly PrevTo { get; set; }

    /// As-at date of the balances.
    public DateOnly At { get; set; }

    /// Receipts of the period.
    public decimal Receipts { get; set; }

    /// Payments of the period, positive.
    public decimal Payments { get; set; }

    /// Receipts − Payments.
    public decimal Net { get; set; }
    public decimal PrevNet { get; set; }
    public decimal? NetDeltaPct { get; set; }

    /// Every fund of the scope, as at At.
    public decimal Balance { get; set; }

    /// The cash funds.
    public decimal CashBalance { get; set; }

    /// The bank funds.
    public decimal BankBalance { get; set; }
    public List<FundBalanceDto> Funds { get; set; } = new();
}

public class FundBalanceDto
{
    public Guid FundId { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FundKind Kind { get; set; }
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
}

public class CashSeriesDto
{
    public SeriesGranularity Granularity { get; set; }
    public List<CashPointDto> Points { get; set; } = new();
}

public class CashPointDto
{
    /// Bucket start.
    public DateOnly Date { get; set; }
    public decimal Receipts { get; set; }

    /// Positive.
    public decimal Payments { get; set; }
}

public class CashBreakdownDto
{
    /// Receipts first, then payments; the largest total first.
    public List<CashReasonTotalDto> ByReason { get; set; } = new();

    /// The ten largest receipts and payments of the period.
    public List<CashDocumentDto> Largest { get; set; } = new();
}

public class CashReasonTotalDto
{
    public Guid ReasonId { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonName { get; set; }
    public CashVoucherType Type { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class CashDocumentDto
{
    public Guid Id { get; set; }
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string? PartnerOrPerson { get; set; }
    public string? Description { get; set; }

    /// Positive.
    public decimal Total { get; set; }
}
