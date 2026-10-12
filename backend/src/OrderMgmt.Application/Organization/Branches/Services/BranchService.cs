using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.AutoVouchers;
using OrderMgmt.Application.CashDebt.OpeningDebts.Interfaces;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.References;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Inventory.OpeningStocks.Interfaces;
using OrderMgmt.Application.Organization.Branches.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.Branches.Services;

public class BranchService : IBranchService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;
    private readonly IOpeningStockService _openingStock;
    private readonly IOpeningDebtService _openingDebt;
    private readonly IAutoCashVoucherService _autoVouchers;
    private readonly IReferenceUsage _usage;

    public BranchService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch,
        ITransactionRunner transaction, IInventoryLock inventoryLock, IOpeningStockService openingStock,
        IOpeningDebtService openingDebt, IAutoCashVoucherService autoVouchers, IReferenceUsage usage)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
        _openingStock = openingStock;
        _openingDebt = openingDebt;
        _autoVouchers = autoVouchers;
        _usage = usage;
    }

    public async Task<MyBranchesDto> GetMyBranchesAsync(CancellationToken ct = default)
    {
        var workingBranchId = await _currentBranch.GetIdAsync(ct);
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var defaultBranchId = await _db.Users.Where(u => u.Id == userId)
            .Select(u => u.DefaultBranchId)
            .SingleAsync(ct);
        var canSwitch = _currentUser.HasPermission(Permissions.Branches.AccessAll);

        var branches = await _db.Branches.AsNoTracking()
            .Where(b => canSwitch || b.Id == defaultBranchId)
            .OrderBy(b => b.Code)
            .Select(b => ToDto(b))
            .ToListAsync(ct);

        return new MyBranchesDto
        {
            DefaultBranchId = defaultBranchId,
            WorkingBranchId = workingBranchId,
            CanSwitch = canSwitch,
            Branches = branches,
        };
    }

    public async Task<IReadOnlyList<BranchDto>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Branches.AsNoTracking()
            .OrderBy(b => b.Code)
            .Select(b => ToDto(b))
            .ToListAsync(ct);
    }

    public async Task<BranchDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(Branch), id);
        return ToDto(branch);
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim();
        if (await _db.Branches.AnyAsync(b => b.Code == code, ct))
            throw new ConflictException($"Mã chi nhánh '{code}' đã tồn tại.");

        var branch = new Branch
        {
            Code = code,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
        };
        _db.Branches.Add(branch);
        // A new branch starts with the default document numbering (D13), so vouchers can be numbered at once.
        foreach (var docType in DocumentNumberingDefaults.AllTypes)
            _db.DocumentNumberings.Add(DocumentNumberingDefaults.Create(docType, branch.Id, _clock.UtcNow));
        // Every branch has a default cash fund (P18).
        _db.Funds.Add(new Fund { BranchId = branch.Id, Code = "QTM", Name = "Quỹ tiền mặt", Kind = FundKind.Cash, IsDefault = true });
        await _db.SaveChangesAsync(ct);
        return ToDto(branch);
    }

    public async Task<BranchDto> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken ct = default)
    {
        var branch = await FindAsync(id, ct);
        branch.Name = request.Name.Trim();
        branch.Address = request.Address?.Trim();
        await _db.SaveChangesAsync(ct);
        return ToDto(branch);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var branch = await FindAsync(id, ct);

        if (branch.Id == BranchDefaults.MainBranchId)
            throw new ConflictException("Không thể xóa chi nhánh chính.");
        // Soft-deleted users count too: their rows still reference the branch (review finding).
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.DefaultBranchId == id, ct))
            throw new ConflictException("Chi nhánh đang là chi nhánh mặc định của người dùng, không thể xóa.");
        if (await _db.Warehouses.AnyAsync(w => w.BranchId == id, ct))
            throw new ConflictException("Chi nhánh đang có kho, không thể xóa.");
        // Cash documents need no warehouse: without this the branch's cash book and debt ledger become unreachable.
        if (await _usage.BranchHasDocumentsAsync(id, ct) || await _usage.BranchHasOpeningDataAsync(id, ct))
            throw new ConflictException("Chi nhánh đã có chứng từ hoặc số dư đầu kỳ, không thể xóa.");

        branch.IsDeleted = true;
        branch.DeletedAt = _clock.UtcNow;
        branch.DeletedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BranchDto> SetLockAsync(Guid id, SetPeriodLockRequest request, CancellationToken ct = default)
    {
        // Year 9999 has no usable "next day" (VnTime.StartOfNextDay of DateOnly.MaxValue overflows), so such a lock
        // could never be moved earlier or cleared again.
        if (request.LockedUntil is { Year: 9999 })
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                ["lockedUntil"] = new[] { "Ngày khóa sổ không hợp lệ." },
            }, null);

        // The exclusive gate waits for in-flight postings of the branch, and postings read LockedUntil after
        // taking the shared gate, so no voucher lands in a period that was just locked (D30, review finding).
        return await _transaction.RunAsync(async c =>
        {
            await _inventoryLock.AcquireBranchGateAsync(new[] { id }, exclusive: true, c);
            var branch = await FindAsync(id, c);
            var previous = branch.LockedUntil;
            branch.LockedUntil = request.LockedUntil;
            await _db.SaveChangesAsync(c);
            // Auto settlements that open-period writes left unchanged while their days were locked (user decision
            // 2026-10-09) are recomputed once those days reopen, so every open-period pair holds AutoAmount again.
            if (previous is { } old)
                await _autoVouchers.RecomputeReopenedAsync(id, request.LockedUntil, old, c);
            return ToDto(branch);
        }, ct);
    }

    public async Task<BranchDto> SetOpeningDateAsync(Guid id, SetOpeningDateRequest request, CancellationToken ct = default)
    {
        return await _transaction.RunAsync(async c =>
        {
            await _inventoryLock.AcquireBranchGateAsync(new[] { id }, exclusive: true, c);
            var branch = await FindAsync(id, c);

            var oldDate = branch.OpeningDate;
            var newDate = request.OpeningDate;
            PeriodLockGuard.EnsureNotLocked(branch.LockedUntil,
                new[] { oldDate, newDate }.Where(d => d is not null).Select(d => d!.Value));
            if (await _usage.BranchHasDocumentsAsync(id, c))
            {
                // A branch that started without an opening date may still take its first one, on or before its
                // earliest document: it has no opening data to repost, and no document falls before the new date.
                if (oldDate is not null || newDate is not { } first || await _usage.BranchHasOpeningDataAsync(id, c))
                    throw new ConflictException("BRANCH_HAS_DOCUMENTS", "Chi nhánh đã có chứng từ, không đổi được ngày đầu kỳ.");
                var earliest = await _usage.EarliestDocumentDateAsync(id, c);
                if (first > earliest)
                    throw new ValidationDomainException(new Dictionary<string, string[]>
                    {
                        ["openingDate"] = new[]
                        {
                            string.Create(CultureInfo.InvariantCulture,
                                $"Chi nhánh đã có chứng từ từ ngày {earliest:dd/MM/yyyy}, ngày đầu kỳ không được sau ngày này."),
                        },
                    }, null);

                branch.OpeningDate = first;
                await _db.SaveChangesAsync(c);
                return ToDto(branch);
            }
            if (oldDate == newDate)
                return ToDto(branch);

            var hasOpeningStock = await _db.OpeningStocks.AnyAsync(o => o.BranchId == id, c);
            if (newDate is null && hasOpeningStock)
                throw new ConflictException("OPENING_DATA_EXISTS", "Chi nhánh đã có tồn đầu kỳ, không xóa được ngày đầu kỳ.");
            var hasOpeningDebts = await _db.OpeningDebts.AnyAsync(o => o.BranchId == id, c);
            if (newDate is null && hasOpeningDebts)
                throw new ConflictException("OPENING_DATA_EXISTS", "Chi nhánh đã có công nợ đầu kỳ, không xóa được ngày đầu kỳ.");
            // The fund opening balance has no date of its own: it sits at the branch opening date (P8), so a move
            // needs no repost.
            if (newDate is null && await _db.Funds.AnyAsync(f => f.BranchId == id && f.OpeningAmount != 0, c))
                throw new ConflictException("OPENING_DATA_EXISTS", "Chi nhánh đã có số dư đầu kỳ của quỹ, không xóa được ngày đầu kỳ.");

            branch.OpeningDate = newDate;
            await _db.SaveChangesAsync(c);
            if (newDate is { } date && hasOpeningStock)
                await _openingStock.RepostBranchAsync(id, date, c);
            // Opening debt entries follow the new date, and the ledger moves their settlements' EffectiveAt (P8).
            if (newDate is not null && hasOpeningDebts)
                await _openingDebt.RepostBranchAsync(id, c);
            return ToDto(branch);
        }, ct);
    }

    private async Task<Branch> FindAsync(Guid id, CancellationToken ct) =>
        await _db.Branches.FirstOrDefaultAsync(b => b.Id == id, ct)
        ?? throw new NotFoundException(nameof(Branch), id);

    private static BranchDto ToDto(Branch b) => new()
    {
        Id = b.Id,
        Code = b.Code,
        Name = b.Name,
        Address = b.Address,
        LockedUntil = b.LockedUntil,
        OpeningDate = b.OpeningDate,
    };
}
