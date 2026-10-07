using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

public class PaymentMethod : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// Which kind of fund receives the money; None = no fund (e.g. debt-only methods).
    public FundKind FundKind { get; set; }
}
