namespace OrderMgmt.Domain.Constants;

/// Codes of the system stock reasons that the executive dashboard treats as sales; a reason a user created never counts.
public static class StockReasonCodes
{
    /// Xuất bán hàng.
    public const string Sale = "XBH";

    /// Nhập hàng bán bị trả lại.
    public const string SaleReturn = "NTL";
}
