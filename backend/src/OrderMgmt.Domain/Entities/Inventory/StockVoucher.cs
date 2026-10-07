using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

/// Stock-in (phiếu nhập) or stock-out (phiếu xuất) voucher.
public class StockVoucher : BaseEntity
{
    public StockDirection Type { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid? PartnerId { get; set; }
    public Customer? Partner { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerAddress { get; set; }
    public string? PartnerTaxCode { get; set; }
    public string? HandlerName { get; set; }

    public Guid ReasonId { get; set; }
    public StockReason? Reason { get; set; }

    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public string? Note { get; set; }

    public decimal Freight { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal GoodsAmount { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }

    public StockVoucherStatus Status { get; set; } = StockVoucherStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }

    public Guid OwnerUserId { get; set; }
    public User? Owner { get; set; }

    /// PostgreSQL xmin (D16).
    public uint Version { get; set; }

    public ICollection<StockVoucherLine> Lines { get; set; } = new List<StockVoucherLine>();
    public ICollection<StockVoucherActivity> Activities { get; set; } = new List<StockVoucherActivity>();
}
