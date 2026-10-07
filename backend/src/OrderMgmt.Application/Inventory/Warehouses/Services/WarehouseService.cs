using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Interfaces;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Entities.Organization;

namespace OrderMgmt.Application.Inventory.Warehouses.Services;

public class WarehouseService : IWarehouseService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;

    public WarehouseService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch,
        ITransactionRunner transaction, IInventoryLock inventoryLock)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
    }

    public async Task<IReadOnlyList<WarehouseDto>> ListAsync(WarehouseListRequest request, CancellationToken ct = default)
    {
        var branchId = request.BranchId.HasValue && _currentUser.HasPermission(Permissions.Branches.AccessAll)
            ? request.BranchId.Value
            : await _currentBranch.GetIdAsync(ct);

        var query = _db.Warehouses.AsNoTracking().Where(w => w.BranchId == branchId);
        if (request.IsActive.HasValue)
            query = query.Where(w => w.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(w => EF.Functions.ILike(w.Code, pattern) || EF.Functions.ILike(w.Name, pattern));
        }

        return await query.OrderBy(w => w.Code)
            .Select(w => new WarehouseDto
            {
                Id = w.Id,
                Code = w.Code,
                Name = w.Name,
                BranchId = w.BranchId,
                BranchCode = w.Branch!.Code,
                BranchName = w.Branch!.Name,
                IsActive = w.IsActive,
            })
            .ToListAsync(ct);
    }

    public async Task<WarehouseDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var warehouse = await _db.Warehouses.AsNoTracking().Include(w => w.Branch)
            .FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException(nameof(Warehouse), id);
        await EnsureAccessibleAsync(warehouse, ct);
        return ToDto(warehouse);
    }

    public async Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken ct = default)
    {
        var branchId = await ResolveTargetBranchAsync(request.BranchId, ct);

        var code = request.Code.Trim();
        if (await _db.Warehouses.AnyAsync(w => w.Code == code, ct))
            throw new ConflictException($"Mã kho '{code}' đã tồn tại.");

        var warehouse = new Warehouse
        {
            Code = code,
            Name = request.Name.Trim(),
            BranchId = branchId,
            IsActive = request.IsActive,
        };
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(warehouse.Id, ct);
    }

    public async Task<WarehouseDto> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, c)
                ?? throw new NotFoundException(nameof(Warehouse), id);
            await EnsureAccessibleAsync(warehouse, c);

            var branchId = await ResolveTargetBranchAsync(request.BranchId, c);
            if (branchId != warehouse.BranchId)
            {
                // The exclusive gate of the current branch waits for in-flight postings, so the activity check
                // cannot race a first posting into this warehouse (D30, review finding).
                await _inventoryLock.AcquireBranchGateAsync(new[] { warehouse.BranchId }, exclusive: true, c);
                if (await _db.InventoryLedger.AnyAsync(e => e.WarehouseId == id, c))
                    throw new ConflictException("Kho đã phát sinh nhập xuất, không được chuyển sang chi nhánh khác.");
            }

            warehouse.BranchId = branchId;
            warehouse.Name = request.Name.Trim();
            warehouse.IsActive = request.IsActive;
            await _db.SaveChangesAsync(c);
        }, ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, c)
                ?? throw new NotFoundException(nameof(Warehouse), id);
            await EnsureAccessibleAsync(warehouse, c);

            // As in UpdateAsync: the exclusive gate waits for in-flight voucher and opening-stock saves of the branch,
            // so the checks below cannot race a first posting into this warehouse (review finding).
            await _inventoryLock.AcquireBranchGateAsync(new[] { warehouse.BranchId }, exclusive: true, c);
            if (await _db.InventoryLedger.AnyAsync(e => e.WarehouseId == id, c)
                || await _db.StockVouchers.AnyAsync(v => v.WarehouseId == id, c)
                || await _db.StockVoucherLines.AnyAsync(l => l.WarehouseId == id, c)
                || await _db.OpeningStocks.AnyAsync(o => o.WarehouseId == id, c))
                throw new ConflictException("Kho đã phát sinh dữ liệu kho, không thể xóa.");

            warehouse.IsDeleted = true;
            warehouse.DeletedAt = _clock.UtcNow;
            warehouse.DeletedBy = _currentUser.UserId;
            await _db.SaveChangesAsync(c);
        }, ct);
    }

    /// A warehouse of another branch is visible and editable only with branches.access_all (review finding).
    private async Task EnsureAccessibleAsync(Warehouse warehouse, CancellationToken ct)
    {
        if (warehouse.BranchId != await _currentBranch.GetIdAsync(ct)
            && !_currentUser.HasPermission(Permissions.Branches.AccessAll))
            throw new ForbiddenException("Bạn không có quyền quản lý kho của chi nhánh khác.");
    }

    /// A branch other than the working branch requires branches.access_all.
    private async Task<Guid> ResolveTargetBranchAsync(Guid? requested, CancellationToken ct)
    {
        var working = await _currentBranch.GetIdAsync(ct);
        var branchId = requested ?? working;

        if (branchId != working && !_currentUser.HasPermission(Permissions.Branches.AccessAll))
            throw new ForbiddenException("Bạn không có quyền quản lý kho của chi nhánh khác.");
        if (!await _db.Branches.AnyAsync(b => b.Id == branchId, ct))
            throw new NotFoundException(nameof(Branch), branchId);

        return branchId;
    }

    private static WarehouseDto ToDto(Warehouse w) => new()
    {
        Id = w.Id,
        Code = w.Code,
        Name = w.Name,
        BranchId = w.BranchId,
        BranchCode = w.Branch?.Code ?? string.Empty,
        BranchName = w.Branch?.Name ?? string.Empty,
        IsActive = w.IsActive,
    };

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
