using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Reports.Models;

public class DebtDocumentsReportRequest
{
    public DebtSide Side { get; set; }
    public DateOnly At { get; set; }
    public Guid? PartnerId { get; set; }
}

public class DebtDocumentsReportDto
{
    public DebtSide Side { get; set; }
    public DateOnly At { get; set; }
    public List<DebtDocumentsGroupDto> Groups { get; set; } = new();
    public decimal TotalRemaining { get; set; }
    public decimal TotalUnallocated { get; set; }
    public decimal TotalBalance { get; set; }
}

public class DebtDocumentsGroupDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public List<DebtDocumentRowDto> Items { get; set; } = new();
    public List<DebtUnallocatedRowDto> Unallocated { get; set; } = new();
    public decimal TotalRemaining { get; set; }
    public decimal TotalUnallocated { get; set; }

    /// TotalRemaining - TotalUnallocated.
    public decimal Balance { get; set; }
}

public class DebtDocumentRowDto
{
    public Guid EntryId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal Settled { get; set; }
    public decimal Remaining { get; set; }
    public int DaysOutstanding { get; set; }
    public int DaysOverdue { get; set; }
}

public class DebtUnallocatedRowDto
{
    public Guid EntryId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal Settled { get; set; }
    public decimal Remaining { get; set; }
}

public class DebtAgingReportRequest
{
    public DebtSide Side { get; set; }
    public DateOnly At { get; set; }
    public Guid? PartnerId { get; set; }

    /// Comma-separated day boundaries, e.g. "30,60,90"; missing means the defaults.
    public string? Buckets { get; set; }
}

/// One age range in days; To null is the open-ended last bucket.
public class AgingBucketDto
{
    public int From { get; set; }
    public int? To { get; set; }
}

public class DebtAgingReportDto
{
    public DebtSide Side { get; set; }
    public DateOnly At { get; set; }
    public List<AgingBucketDto> Buckets { get; set; } = new();
    public List<DebtAgingRowDto> Rows { get; set; } = new();
    public DebtAgingRowDto Totals { get; set; } = new();
}

public class DebtAgingRowDto
{
    public Guid? PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }

    /// One per bucket, same order as the report's Buckets.
    public List<decimal> Amounts { get; set; } = new();

    /// Sum of Amounts.
    public decimal TotalOpen { get; set; }

    /// Sum of open Decrease amounts.
    public decimal Unallocated { get; set; }

    /// TotalOpen - Unallocated.
    public decimal Balance { get; set; }
}
