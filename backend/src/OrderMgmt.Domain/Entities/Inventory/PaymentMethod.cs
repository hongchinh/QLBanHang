using OrderMgmt.Domain.Common;

namespace OrderMgmt.Domain.Entities.Inventory;

public class PaymentMethod : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// Used by Round 2 (automatic cash vouchers).
    public bool IsCash { get; set; }
}
