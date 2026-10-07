using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Identity.Admin.Interfaces;
using OrderMgmt.Application.Identity.Admin.Models;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Identity.Admin.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ICurrentUser _currentUser;

    public AdminUserService(
        IAppDbContext db,
        IPasswordHasher hasher,
        IRefreshTokenService refreshTokens,
        ICurrentUser currentUser)
    {
        _db = db;
        _hasher = hasher;
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AdminUserListItemDto>> ListAsync(AdminUserListQuery query, CancellationToken ct = default)
    {
        var queryable = _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (query.ActiveOnly)
            queryable = queryable.Where(u => !u.IsDeleted && u.Status == UserStatus.Active);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            queryable = queryable.Where(u =>
                EF.Functions.ILike(u.Username, pattern)
                || EF.Functions.ILike(EF.Functions.Unaccent(u.FullName), EF.Functions.Unaccent(pattern)));
        }

        return await queryable
            .OrderBy(u => u.Username)
            .Select(u => new AdminUserListItemDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                RoleCode = u.UserRoles
                    .OrderBy(ur => ur.Role.Code)
                    .Select(ur => ur.Role.Code)
                    .FirstOrDefault(),
                IsActive = !u.IsDeleted && u.Status == UserStatus.Active,
                LastLoginAt = u.LastLoginAt,
                DefaultBranchId = u.DefaultBranchId,
                DefaultBranchName = u.DefaultBranch != null ? u.DefaultBranch.Name : null,
            })
            .ToListAsync(ct);
    }

    public async Task<AdminUserDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.DefaultBranch)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(User), id);

        return MapToDetail(user);
    }

    public async Task<AdminUserDetailDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == req.Username, ct))
            throw new ConflictException($"Username '{req.Username}' đã tồn tại.");

        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == req.Email, ct))
            throw new ConflictException($"Email '{req.Email}' đã tồn tại.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == req.RoleCode, ct)
            ?? throw new ConflictException($"Role '{req.RoleCode}' không tồn tại.");

        if (req.DefaultBranchId.HasValue)
            await EnsureBranchExistsAsync(req.DefaultBranchId.Value, ct);

        var user = new User
        {
            Username = req.Username,
            Email = req.Email,
            FullName = req.FullName,
            PhoneNumber = string.IsNullOrWhiteSpace(req.PhoneNumber) ? null : req.PhoneNumber,
            PasswordHash = _hasher.Hash(req.Password),
            Status = req.Status,
            DefaultBranchId = req.DefaultBranchId ?? BranchDefaults.MainBranchId,
            UserRoles = new List<UserRole> { new() { RoleId = role.Id } },
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(user.Id, ct);
    }

    public async Task<AdminUserDetailDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(User), id);

        if (!string.Equals(user.Email, req.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _db.Users.AnyAsync(u => u.Id != id && u.Email == req.Email, ct))
                throw new ConflictException($"Email '{req.Email}' đã tồn tại.");
        }

        var wasAdmin = user.UserRoles.Any(ur => ur.Role.Code == RoleCodes.Admin);
        var wasActive = user.Status == UserStatus.Active;
        var currentRoleCode = user.UserRoles
            .OrderBy(ur => ur.Role.Code)
            .Select(ur => ur.Role.Code)
            .FirstOrDefault();

        user.FullName = req.FullName;
        user.Email = req.Email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(req.PhoneNumber) ? null : req.PhoneNumber;
        user.Status = req.Status;

        if (req.DefaultBranchId.HasValue)
        {
            await EnsureBranchExistsAsync(req.DefaultBranchId.Value, ct);
            user.DefaultBranchId = req.DefaultBranchId.Value;
        }

        if (!string.Equals(currentRoleCode, req.RoleCode, StringComparison.Ordinal))
        {
            var newRole = await _db.Roles.FirstOrDefaultAsync(r => r.Code == req.RoleCode, ct)
                ?? throw new ConflictException($"Role '{req.RoleCode}' không tồn tại.");

            _db.UserRoles.RemoveRange(user.UserRoles);
            user.UserRoles = new List<UserRole> { new() { UserId = user.Id, RoleId = newRole.Id } };
        }

        var roleChangedAwayFromAdmin = wasAdmin && req.RoleCode != RoleCodes.Admin;
        var disabledViaUpdate = wasActive && req.Status == UserStatus.Disabled;
        if (wasAdmin && wasActive && (roleChangedAwayFromAdmin || disabledViaUpdate))
            await EnsureNotLastActiveAdminAsync(excludedId: id, ct);

        if (disabledViaUpdate)
            await _refreshTokens.RevokeAllActiveForUserAsync(id, "USER_DISABLED", ct);

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest req, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(User), id);

        user.PasswordHash = _hasher.Hash(req.NewPassword);

        await _refreshTokens.RevokeAllActiveForUserAsync(id, "PASSWORD_RESET", ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(Guid id, SetUserStatusRequest req, CancellationToken ct = default)
    {
        if (id == _currentUser.UserId && req.Status == UserStatus.Disabled)
            throw new ForbiddenException("Không thể tự khoá tài khoản đang đăng nhập.");

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(User), id);

        var wasActive = user.Status == UserStatus.Active;
        var isAdmin = user.UserRoles.Any(ur => ur.Role.Code == RoleCodes.Admin);

        if (wasActive && req.Status == UserStatus.Disabled && isAdmin)
            await EnsureNotLastActiveAdminAsync(excludedId: id, ct);

        user.Status = req.Status;

        if (req.Status == UserStatus.Disabled)
            await _refreshTokens.RevokeAllActiveForUserAsync(id, "USER_DISABLED", ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (id == _currentUser.UserId)
            throw new ForbiddenException("Không thể tự xoá tài khoản đang đăng nhập.");

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(User), id);

        var ownedCount = await _db.Quotations
            .CountAsync(q => q.OwnerUserId == id && q.Status != QuotationStatus.Cancelled, ct);
        if (ownedCount > 0)
            throw new ConflictException(
                $"Người dùng còn {ownedCount} báo giá đang sở hữu, vui lòng chuyển nhượng trước khi xoá.");

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Code == RoleCodes.Admin);
        if (isAdmin && user.Status == UserStatus.Active)
            await EnsureNotLastActiveAdminAsync(excludedId: id, ct);

        // UserQuotationSettings has no nav from User → cascade soft-delete in AppDbContext won't
        // touch it. Mark it deleted manually here.
        var uqs = await _db.UserQuotationSettings.FirstOrDefaultAsync(s => s.UserId == id, ct);
        if (uqs is not null)
            uqs.IsDeleted = true;

        // Revoke active refresh tokens with explicit reason for audit. The cascade soft-delete
        // would set IsDeleted on RefreshToken rows but not RevokedAt/RevokedReason.
        await _refreshTokens.RevokeAllActiveForUserAsync(id, "USER_DELETED", ct);

        user.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureNotLastActiveAdminAsync(Guid excludedId, CancellationToken ct)
    {
        var otherActiveAdmins = await _db.Users
            .CountAsync(u => u.Id != excludedId
                && u.Status == UserStatus.Active
                && u.UserRoles.Any(ur => ur.Role.Code == RoleCodes.Admin), ct);
        if (otherActiveAdmins == 0)
            throw new ConflictException("Không thể thao tác — đây là quản trị viên Active duy nhất của hệ thống.");
    }

    private static AdminUserDetailDto MapToDetail(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        RoleCode = u.UserRoles
            .OrderBy(ur => ur.Role.Code)
            .Select(ur => ur.Role.Code)
            .FirstOrDefault(),
        Status = u.Status,
        IsDeleted = u.IsDeleted,
        LastLoginAt = u.LastLoginAt,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
        DefaultBranchId = u.DefaultBranchId,
        DefaultBranchName = u.DefaultBranch?.Name,
    };

    private async Task EnsureBranchExistsAsync(Guid branchId, CancellationToken ct)
    {
        if (!await _db.Branches.AnyAsync(b => b.Id == branchId, ct))
            throw new DomainException("BRANCH_NOT_FOUND", "Chi nhánh không tồn tại.");
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
