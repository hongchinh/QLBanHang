using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Identity.Admin.Models;

public class CreateUserRequest
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string RoleCode { get; set; } = default!;
    public string Password { get; set; } = default!;

    // Explicit default: UserStatus.Disabled = 0, Active = 1. Without this, a JSON body missing
    // the `status` field would deserialize to Disabled.
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// Null → the main branch.
    public Guid? DefaultBranchId { get; set; }
}
