using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Numbering;

/// D13 defaults: PN / PX, length 5, no reset, pattern {KH}{STT}. Used by the seeder and on branch creation.
public static class DocumentNumberingDefaults
{
    public static readonly DocumentType[] AllTypes = { DocumentType.StockIn, DocumentType.StockOut };

    public static DocumentNumbering Create(DocumentType docType, Guid branchId, DateTimeOffset now) => new()
    {
        DocType = docType,
        BranchId = branchId,
        Prefix = docType == DocumentType.StockIn ? "PN" : "PX",
        Length = 5,
        ResetPolicy = NumberingResetPolicy.None,
        Pattern = "{KH}{STT}",
        UpdatedAt = now,
    };
}
