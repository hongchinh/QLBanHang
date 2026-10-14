using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Models;

public class DebtSummaryDto
{
    /// As-at date of every figure.
    public DateOnly At { get; set; }

    /// The overdue buckets of the overdue report with its default boundaries (1-30, 31-60, 61-90, 91+).
    public List<AgingBucketDto> OverdueBuckets { get; set; } = new();
    public DebtSideSummaryDto Receivable { get; set; } = new();
    public DebtSideSummaryDto Payable { get; set; } = new();
}

public class DebtSideSummaryDto
{
    /// TotalOpen − Unallocated.
    public decimal Balance { get; set; }

    /// Open amount of the documents still owed.
    public decimal TotalOpen { get; set; }

    /// Money not yet matched to a document (prepayments, receipts and payments without an allocation).
    public decimal Unallocated { get; set; }

    /// TotalOpen − Overdue.
    public decimal NotDue { get; set; }
    public decimal Overdue { get; set; }
    public int OverduePartnerCount { get; set; }

    /// One per bucket of the summary.
    public List<decimal> OverdueAmounts { get; set; } = new();

    /// Due within the next 7 days.
    public decimal DueSoon { get; set; }

    /// Documents.
    public int DueSoonCount { get; set; }
}

public class DebtBreakdownDto
{
    public DateOnly At { get; set; }

    /// The ten customers with the largest balance.
    public List<DebtPartnerRowDto> TopDebtors { get; set; } = new();
    public List<CreditLimitBreachDto> OverLimit { get; set; } = new();
    public List<DebtEmployeeRowDto> ByEmployee { get; set; } = new();

    /// Payables due within the next 30 days, at most 50.
    public List<PayableDueRowDto> PayablesDue { get; set; } = new();
}

public class DebtPartnerRowDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public decimal Balance { get; set; }
    public decimal Overdue { get; set; }
}

public class CreditLimitBreachDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal Limit { get; set; }

    /// Balance − Limit.
    public decimal Over { get; set; }
}

public class DebtEmployeeRowDto
{
    /// Null: documents without an owner.
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public decimal Open { get; set; }
    public decimal Overdue { get; set; }
}

public class PayableDueRowDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public decimal Open { get; set; }
}
