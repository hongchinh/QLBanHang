using OrderMgmt.Domain.Common;

namespace OrderMgmt.Domain.Entities.Payments;

public class Bank : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ShortName { get; set; }
    public string Bin { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
