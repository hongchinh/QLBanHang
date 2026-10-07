using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Numbering;

/// D13 / P4 defaults: PN / PX / PT / PC / CQ / BT, length 5, no reset, pattern {KH}{STT}. Used by the seeder and on branch creation.
public static class DocumentNumberingDefaults
{
    public static readonly DocumentType[] AllTypes = 
    {
        DocumentType.StockIn, DocumentType.StockOut, DocumentType.Receipt,
        DocumentType.Payment, DocumentType.FundTransfer, DocumentType.DebtOffset,
    };

    public static DocumentNumbering Create(DocumentType docType, Guid branchId, DateTimeOffset now) => new()
    {
        DocType = docType,
        BranchId = branchId,
        Prefix = PrefixOf(docType),
        Length = 5,
        ResetPolicy = NumberingResetPolicy.None,
        Pattern = "{KH}{STT}",
        UpdatedAt = now,
    };

    private static string PrefixOf(DocumentType docType) => docType switch
    {
        DocumentType.StockIn => "PN",
        DocumentType.StockOut => "PX",
        DocumentType.Receipt => "PT",
        DocumentType.Payment => "PC",
        DocumentType.FundTransfer => "CQ",
        DocumentType.DebtOffset => "BT",
        _ => throw new ArgumentOutOfRangeException(nameof(docType), docType, null),
    };
}
