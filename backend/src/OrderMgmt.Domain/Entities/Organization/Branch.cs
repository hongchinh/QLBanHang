using OrderMgmt.Domain.Common;

namespace OrderMgmt.Domain.Entities.Organization;

public class Branch : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }

    /// <summary>Period lock: vouchers dated on or before this VN date can no longer change.</summary>
    public DateOnly? LockedUntil { get; set; }
}
