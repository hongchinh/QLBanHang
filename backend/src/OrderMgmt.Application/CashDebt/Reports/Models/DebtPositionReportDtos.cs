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

public class DebtOverdueReportRequest
{
    public DebtSide Side { get; set; }
    public DateOnly At { get; set; }
    public Guid? PartnerId { get; set; }

    /// Comma-separated day boundaries, e.g. "30,60,90"; missing means the defaults.
    public string? Buckets { get; set; }
}

public class DebtOverdueReportDto
{
    public DebtSide Side { get; set; }
    public DateOnly At { get; set; }
    public List<AgingBucketDto> Buckets { get; set; } = new();
    public List<DebtOverdueGroupDto> Groups { get; set; } = new();

    /// One per bucket; zero-filled when there are no groups.
    public List<decimal> TotalAmounts { get; set; } = new();
    public decimal TotalOverdue { get; set; }
    public decimal TotalUnallocated { get; set; }
}

public class DebtOverdueGroupDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public List<DebtOverdueRowDto> Items { get; set; } = new();
    public List<decimal> Amounts { get; set; } = new();
    public decimal TotalOverdue { get; set; }

    /// Open Decrease total at the date; information only, not subtracted.
    public decimal Unallocated { get; set; }
}

public class DebtOverdueRowDto
{
    public Guid EntryId { get; set; }
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = default!;
    public DateOnly DocDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal Remaining { get; set; }
    public int DaysOverdue { get; set; }
    public int BucketIndex { get; set; }
}

public class DebtBySalespersonReportRequest
{
    public DebtSide Side { get; set; } = DebtSide.Receivable;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public Guid? PartnerId { get; set; }
}

public class DebtBySalespersonReportDto
{
    public DebtSide Side { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// Owners with any non-zero value, ordered by owner name (vi-VN), the row without an owner last.
    public List<DebtBySalespersonRowDto> Rows { get; set; } = new();

    /// Sum of each column; OwnerUserId and OwnerName are null.
    public DebtBySalespersonRowDto Totals { get; set; } = new();

    public DebtUnallocatedLineDto Unallocated { get; set; } = new();

    /// Totals.OpeningOpen - Unallocated.Opening.
    public decimal OpeningBalance { get; set; }

    /// Totals.ClosingOpen - Unallocated.Closing.
    public decimal ClosingBalance { get; set; }
}

/// OpeningOpen + Increase - Settled == ClosingOpen (B7).
public class DebtBySalespersonRowDto
{
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public decimal OpeningOpen { get; set; }
    public decimal Increase { get; set; }
    public decimal Settled { get; set; }
    public decimal ClosingOpen { get; set; }

    /// Part of ClosingOpen whose due date (or document date) is before To.
    public decimal ClosingOverdue { get; set; }
}

/// Open Decrease entries (prepayments, unmatched receipts) at the start and at the end of the period.
public class DebtUnallocatedLineDto
{
    public decimal Opening { get; set; }
    public decimal Closing { get; set; }
}
