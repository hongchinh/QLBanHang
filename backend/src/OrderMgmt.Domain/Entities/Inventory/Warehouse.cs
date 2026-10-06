using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Organization;

namespace OrderMgmt.Domain.Entities.Inventory;

public class Warehouse : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsActive { get; set; } = true;
}
