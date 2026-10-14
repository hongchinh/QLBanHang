using System.Security.Claims;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Infrastructure.Identity;

namespace OrderMgmt.WebApi.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IActingIdentity _acting;

    public CurrentUser(IHttpContextAccessor accessor, IActingIdentity acting)
    {
        _accessor = accessor;
        _acting = acting;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            if (_acting.Current is { } a) return a.UserId;
            var raw = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? Principal?.FindFirst("sub")?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Username => _acting.Current?.Username ?? Principal?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Email => _acting.Current is not null ? null : Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public string? FullName => _acting.Current?.FullName ?? Principal?.FindFirst("full_name")?.Value;

    public bool IsAuthenticated => _acting.Current is not null || (Principal?.Identity?.IsAuthenticated ?? false);

    public IReadOnlyList<string> Roles =>
        _acting.Current is not null ? Array.Empty<string>() :
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public IReadOnlyList<string> Permissions =>
        _acting.Current?.Permissions ??
        Principal?.FindAll(JwtTokenGenerator.PermissionClaimType).Select(c => c.Value).ToArray()
        ?? Array.Empty<string>();

    public bool HasPermission(string permission) =>
        _acting.Current is { } a ? a.Permissions.Contains(permission) :
        Principal?.HasClaim(JwtTokenGenerator.PermissionClaimType, permission) ?? false;

    public bool IsInRole(string role) => _acting.Current is null && (Principal?.IsInRole(role) ?? false);
}
