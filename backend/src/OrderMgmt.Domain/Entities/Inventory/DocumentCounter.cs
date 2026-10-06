using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

/// Last number issued per (document type, branch, period). PeriodKey is "" when numbering never resets.
public class DocumentCounter
{
    public DocumentType DocType { get; set; }
    public Guid BranchId { get; set; }
    public string PeriodKey { get; set; } = default!;
    public long Value { get; set; }
}
