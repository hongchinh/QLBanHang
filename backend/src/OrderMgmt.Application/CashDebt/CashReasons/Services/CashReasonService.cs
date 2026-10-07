using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashReasons.Interfaces;
using OrderMgmt.Application.CashDebt.CashReasons.Models;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.CashReasons.Services;

public class CashReasonService : ICashReasonService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public CashReasonService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CashReasonDto>> ListAsync(CashVoucherType? type, bool includeAuto, CancellationToken ct = default)
    {
        var query = _db.CashReasons.AsNoTracking();
        if (type.HasValue)
            query = query.Where(r => r.Type == type.Value);
        if (!includeAuto)
            query = query.Where(r => !r.IsAuto);

        return await query.OrderBy(r => r.Type).ThenBy(r => r.Code)
            .Select(r => ToDto(r))
            .ToListAsync(ct);
    }

    public async Task<CashReasonDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await FindAsync(id, ct));

    public async Task<CashReasonDto> CreateAsync(CreateCashReasonRequest request, CancellationToken ct = default)
    {
        EnsureCombination(request.Type, request.PartnerType, request.DebtEffect);

        var code = request.Code.Trim();
        if (await _db.CashReasons.AnyAsync(r => r.Code == code, ct))
            throw new ConflictException($"Mã lý do '{code}' đã tồn tại.");

        var reason = new CashReason
        {
            Code = code,
            Name = request.Name.Trim(),
            Type = request.Type,
            PartnerType = request.PartnerType,
            DebtEffect = request.DebtEffect,
            IsSystem = false,
            IsAuto = false,
        };
        _db.CashReasons.Add(reason);
        await _db.SaveChangesAsync(ct);
        return ToDto(reason);
    }

    public async Task<CashReasonDto> UpdateAsync(Guid id, UpdateCashReasonRequest request, CancellationToken ct = default)
    {
        var reason = await FindAsync(id, ct);

        if (reason.IsAuto)
            throw new ConflictException("Lý do tự động không được sửa.");
        if (reason.IsSystem && (reason.Type != request.Type || reason.PartnerType != request.PartnerType))
            throw new ConflictException("Lý do hệ thống không được đổi loại phiếu hoặc loại đối tượng.");

        EnsureCombination(request.Type, request.PartnerType, request.DebtEffect);

        reason.Name = request.Name.Trim();
        reason.Type = request.Type;
        reason.PartnerType = request.PartnerType;
        reason.DebtEffect = request.DebtEffect;
        await _db.SaveChangesAsync(ct);
        return ToDto(reason);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var reason = await FindAsync(id, ct);
        if (reason.IsSystem)
            throw new ConflictException("Không thể xóa lý do hệ thống.");

        reason.IsDeleted = true;
        reason.DeletedAt = _clock.UtcNow;
        reason.DeletedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
    }

    /// Collects every broken combination rule (P19) into one validation error.
    private static void EnsureCombination(CashVoucherType type, PartnerType partnerType, DebtEffect effect)
    {
        var errors = new Dictionary<string, string[]>();
        if (!DebtEffectRules.IsAllowedForCash(type, effect))
            errors["debtEffect"] = new[] { "Ảnh hưởng công nợ không phù hợp với loại phiếu." };
        if (!DebtEffectRules.IsAllowedPartnerType(effect, partnerType))
            errors["partnerType"] = new[] { "Loại đối tượng không phù hợp với ảnh hưởng công nợ." };
        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    private async Task<CashReason> FindAsync(Guid id, CancellationToken ct) =>
        await _db.CashReasons.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException(nameof(CashReason), id);

    private static CashReasonDto ToDto(CashReason r) => new()
    {
        Id = r.Id,
        Code = r.Code,
        Name = r.Name,
        Type = r.Type,
        PartnerType = r.PartnerType,
        DebtEffect = r.DebtEffect,
        IsSystem = r.IsSystem,
        IsAuto = r.IsAuto,
    };
}
