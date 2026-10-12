using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Interfaces;
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
    private readonly ITransactionRunner _transaction;
    private readonly ICashDebtLock _cashDebtLock;

    public StockReasonService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ITransactionRunner transaction,
        ICashDebtLock cashDebtLock)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _transaction = transaction;
        _cashDebtLock = cashDebtLock;
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
        var saved = await _transaction.RunAsync(async c =>
        {
            var reason = await LockAsync(id, c);

            if (reason.IsSystem && (reason.Direction != request.Direction || reason.PartnerType != request.PartnerType))
                throw new ConflictException("Lý do hệ thống không được đổi chiều nhập/xuất hoặc loại đối tượng.");

            EnsureCombination(request.Direction, request.PartnerType, request.DebtEffect);

            // Vouchers already saved with this reason were validated and posted against these fields (P19).
            var directionChanged = reason.Direction != request.Direction;
            var partnerTypeChanged = reason.PartnerType != request.PartnerType;
            var debtEffectChanged = reason.DebtEffect != request.DebtEffect;
            if ((directionChanged || partnerTypeChanged || debtEffectChanged) && await IsUsedAsync(id, c))
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
            await _db.SaveChangesAsync(c);
            return reason;
        }, ct);
        return ToDto(saved);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var reason = await LockAsync(id, c);
            if (reason.IsSystem)
                throw new ConflictException("Không thể xóa lý do hệ thống.");
            if (await IsUsedAsync(id, c))
                throw new ConflictException("Lý do đã được dùng trên phiếu kho, không thể xóa.");

            reason.IsDeleted = true;
            reason.DeletedAt = _clock.UtcNow;
            reason.DeletedBy = _currentUser.UserId;
            await _db.SaveChangesAsync(c);
        }, ct);
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

    /// Used by a non-deleted stock voucher (any status); the query filter skips deleted ones.
    private Task<bool> IsUsedAsync(Guid id, CancellationToken ct) =>
        _db.StockVouchers.AnyAsync(v => v.ReasonId == id, ct);

    /// The exclusive stock reason key, then the tracked reason. A stock voucher save holds the key shared from before it
    /// reads the reason until it commits, so the in-use check sees every voucher saved with the reason, and a save that
    /// starts meanwhile reads the reason only after this edit commits.
    private async Task<StockReason> LockAsync(Guid id, CancellationToken ct)
    {
        await _cashDebtLock.AcquireStockReasonAsync(id, exclusive: true, ct);
        return await FindAsync(id, ct);
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
