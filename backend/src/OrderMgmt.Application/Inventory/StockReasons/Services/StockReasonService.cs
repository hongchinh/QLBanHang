using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.StockReasons.Interfaces;
using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockReasons.Services;

public class StockReasonService : IStockReasonService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;

    public StockReasonService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<StockReasonDto>> ListAsync(StockDirection? direction, CancellationToken ct = default)
    {
        var query = _db.StockReasons.AsNoTracking();
        if (direction.HasValue)
            query = query.Where(r => r.Direction == direction.Value);

        return await query.OrderBy(r => r.Direction).ThenBy(r => r.Code)
            .Select(r => ToDto(r))
            .ToListAsync(ct);
    }

    public async Task<StockReasonDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await FindAsync(id, ct));

    public async Task<StockReasonDto> CreateAsync(CreateStockReasonRequest request, CancellationToken ct = default)
    {
        EnsureCombination(request.Direction, request.PartnerType, request.DebtEffect);

        var code = request.Code.Trim();
        if (await _db.StockReasons.AnyAsync(r => r.Code == code, ct))
            throw new ConflictException($"Mã lý do '{code}' đã tồn tại.");

        var reason = new StockReason
        {
            Code = code,
            Name = request.Name.Trim(),
            Direction = request.Direction,
            PartnerType = request.PartnerType,
            DebtEffect = request.DebtEffect,
        };
        _db.StockReasons.Add(reason);
        await _db.SaveChangesAsync(ct);
        return ToDto(reason);
    }

    public async Task<StockReasonDto> UpdateAsync(Guid id, UpdateStockReasonRequest request, CancellationToken ct = default)
    {
        var reason = await FindAsync(id, ct);

        if (reason.IsSystem && (reason.Direction != request.Direction || reason.PartnerType != request.PartnerType))
            throw new ConflictException("Lý do hệ thống không được đổi chiều nhập/xuất hoặc loại đối tượng.");

        EnsureCombination(request.Direction, request.PartnerType, request.DebtEffect);

        // Vouchers already saved with this reason were validated against its direction and partner type.
        var directionChanged = reason.Direction != request.Direction;
        var partnerTypeChanged = reason.PartnerType != request.PartnerType;
        var debtEffectChanged = reason.DebtEffect != request.DebtEffect;
        if ((directionChanged || partnerTypeChanged || debtEffectChanged) && await _db.StockVouchers.AnyAsync(v => v.ReasonId == id, ct))
        {
            var errors = new Dictionary<string, string[]>();
            if (directionChanged)
                errors["direction"] = new[] { "Lý do đã được dùng trên phiếu kho, không được đổi chiều nhập/xuất." };
            if (partnerTypeChanged)
                errors["partnerType"] = new[] { "Lý do đã được dùng trên phiếu kho, không được đổi loại đối tượng." };
            if (debtEffectChanged)
                errors["debtEffect"] = new[] { "Lý do đã được dùng trên phiếu kho, không được đổi ảnh hưởng công nợ." };
            throw new ValidationDomainException(errors, null);
        }

        reason.Name = request.Name.Trim();
        reason.Direction = request.Direction;
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
        if (await _db.StockVouchers.AnyAsync(v => v.ReasonId == id, ct))
            throw new ConflictException("Lý do đã được dùng trên phiếu kho, không thể xóa.");

        reason.IsDeleted = true;
        reason.DeletedAt = _clock.UtcNow;
        reason.DeletedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
    }

    /// Collects every broken combination rule (P19) into one validation error.
    private static void EnsureCombination(StockDirection direction, PartnerType partnerType, DebtEffect effect)
    {
        var errors = new Dictionary<string, string[]>();
        if (!DebtEffectRules.IsAllowedForStock(direction, effect))
            errors["debtEffect"] = new[] { "Ảnh hưởng công nợ không phù hợp với chiều nhập/xuất." };
        if (!DebtEffectRules.IsAllowedPartnerType(effect, partnerType))
            errors["partnerType"] = new[] { "Loại đối tượng không phù hợp với ảnh hưởng công nợ." };
        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    private async Task<StockReason> FindAsync(Guid id, CancellationToken ct) =>
        await _db.StockReasons.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException(nameof(StockReason), id);

    private static StockReasonDto ToDto(StockReason r) => new()
    {
        Id = r.Id,
        Code = r.Code,
        Name = r.Name,
        Direction = r.Direction,
        PartnerType = r.PartnerType,
        DebtEffect = r.DebtEffect,
        IsSystem = r.IsSystem,
    };
}
