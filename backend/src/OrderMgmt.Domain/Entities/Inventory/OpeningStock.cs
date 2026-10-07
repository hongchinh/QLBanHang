using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Organization;

namespace OrderMgmt.Domain.Entities.Inventory;

/// Opening quantity and value of a product in a warehouse, posted at 00:00 VN on OpeningDate (D20).
public class OpeningStock : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public DateOnly OpeningDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}
