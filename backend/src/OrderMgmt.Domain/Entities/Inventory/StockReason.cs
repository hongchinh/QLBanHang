using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

public class StockReason : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public StockDirection Direction { get; set; }
    public PartnerType PartnerType { get; set; }

    /// System reasons keep their direction and partner type and cannot be deleted.
    public bool IsSystem { get; set; }
}
