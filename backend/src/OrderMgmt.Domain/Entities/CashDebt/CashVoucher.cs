using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.CashDebt;

/// Receipt (phiếu thu) or payment (phiếu chi). Manual, or automatic when SourceStockVoucherId is set (P20).
public class CashVoucher : BaseEntity
{
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = default!;
    public DateTimeOffset VoucherAt { get; set; }

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid FundId { get; set; }
    public Fund? Fund { get; set; }

    public Guid ReasonId { get; set; }
    public CashReason? Reason { get; set; }

    public Guid? PartnerId { get; set; }
    public Customer? Partner { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerAddress { get; set; }
    public string? PartnerTaxCode { get; set; }

    /// Người nộp / người nhận.
    public string? PersonName { get; set; }

    /// Lý do nộp / lý do chi.
    public string? Description { get; set; }

    /// Kèm theo (chứng từ gốc).
    public string? AttachedDocs { get; set; }

    public string? Note { get; set; }

    /// Σ lines, computed on the server.
    public decimal Total { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }

    public Guid OwnerUserId { get; set; }
    public User? Owner { get; set; }

    /// The stock voucher that manages this automatic voucher.
    public Guid? SourceStockVoucherId { get; set; }
    public StockVoucher? SourceStockVoucher { get; set; }

    /// PostgreSQL xmin.
    public uint Version { get; set; }

    public ICollection<CashVoucherLine> Lines { get; set; } = new List<CashVoucherLine>();
}
