using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

/// Numbering rule per (document type, branch). The running counter lives in DocumentCounter.
public class DocumentNumbering
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DocumentType DocType { get; set; }
    public Guid BranchId { get; set; }
    public string Prefix { get; set; } = default!;
    public int Length { get; set; }
    public NumberingResetPolicy ResetPolicy { get; set; }
    public string Pattern { get; set; } = default!;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
