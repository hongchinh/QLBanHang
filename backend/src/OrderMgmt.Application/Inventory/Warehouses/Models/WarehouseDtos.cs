namespace OrderMgmt.Application.Inventory.Warehouses.Models;

public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = default!;
    public string BranchName { get; set; } = default!;
    public bool IsActive { get; set; }
}

public class WarehouseListRequest
{
    /// Honoured only for users with branches.access_all; otherwise the working branch is used.
    public Guid? BranchId { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}

public class CreateWarehouseRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    /// Null → the working branch.
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateWarehouseRequest
{
    public string Name { get; set; } = default!;
    public Guid BranchId { get; set; }
    public bool IsActive { get; set; }
}
