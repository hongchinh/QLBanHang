using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Identity.Admin.Models;

public class AdminUserDetailDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? RoleCode { get; set; }
    public UserStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid DefaultBranchId { get; set; }
    public string? DefaultBranchName { get; set; }
}
