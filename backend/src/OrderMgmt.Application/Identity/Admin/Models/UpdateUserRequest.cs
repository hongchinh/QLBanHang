using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Identity.Admin.Models;

public class UpdateUserRequest
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string RoleCode { get; set; } = default!;
    public UserStatus Status { get; set; }

    /// Null → unchanged.
    public Guid? DefaultBranchId { get; set; }
}
