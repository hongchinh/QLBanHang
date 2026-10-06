namespace OrderMgmt.Application.Identity.Admin.Models;

public class AdminUserListItemDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? RoleCode { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public Guid DefaultBranchId { get; set; }
    public string? DefaultBranchName { get; set; }
}
