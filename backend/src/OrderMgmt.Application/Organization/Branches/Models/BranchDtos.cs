namespace OrderMgmt.Application.Organization.Branches.Models;

public class BranchDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public DateOnly? LockedUntil { get; set; }
}

public class CreateBranchRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
}

public class UpdateBranchRequest
{
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
}

public class SetPeriodLockRequest
{
    public DateOnly? LockedUntil { get; set; }
}

public class MyBranchesDto
{
    public Guid DefaultBranchId { get; set; }
    public Guid WorkingBranchId { get; set; }
    public bool CanSwitch { get; set; }
    /// All branches when CanSwitch, otherwise only the default branch.
    public IReadOnlyList<BranchDto> Branches { get; set; } = Array.Empty<BranchDto>();
}
