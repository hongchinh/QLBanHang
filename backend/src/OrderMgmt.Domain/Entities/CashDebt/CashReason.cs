using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

public class CashReason : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public CashVoucherType Type { get; set; }
    public PartnerType PartnerType { get; set; }
    public DebtEffect DebtEffect { get; set; }

    /// System reasons keep their type and partner type and cannot be deleted.
    public bool IsSystem { get; set; }

    /// Used only by automatic vouchers; cannot be edited and is hidden from manual pickers.
    public bool IsAuto { get; set; }
}
