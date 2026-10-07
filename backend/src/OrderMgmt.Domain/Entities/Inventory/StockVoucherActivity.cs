using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

public class StockVoucherActivity : BaseEntity
{
    public Guid StockVoucherId { get; set; }
    public StockVoucher? StockVoucher { get; set; }

    public StockVoucherActivityAction Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Description { get; set; } = default!;
    public string? MetadataJson { get; set; }
}
