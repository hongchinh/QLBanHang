using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Common;

/// The open part of one debt entry at a date: what is still owed (Increase) or still unallocated (Decrease). A public
/// copy of the open part of the report query's item, so the math on it is testable without a database.
public sealed record OpenDebtItem(Guid BranchId, Guid PartnerId, DebtDirection Direction, DebtSourceType SourceType,
    Guid SourceId, string DocCode, DateOnly DocDate, DateOnly? DueDate, Guid? OwnerUserId, decimal Open)
{
    /// Without a term a debt is due on the document date.
    public DateOnly Due => DueDate ?? DocDate;
}
