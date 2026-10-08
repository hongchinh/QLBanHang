using OrderMgmt.Domain.Common;

namespace OrderMgmt.Domain.Entities.CashDebt;

public class CashVoucherLine : BaseEntity
{
    public Guid CashVoucherId { get; set; }
    public CashVoucher? CashVoucher { get; set; }

    public int SortOrder { get; set; }
    public string Description { get; set; } = default!;

    /// Greater than zero.
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}
