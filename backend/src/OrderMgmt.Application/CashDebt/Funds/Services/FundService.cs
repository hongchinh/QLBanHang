using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Funds.Interfaces;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Organization.Branches;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Funds.Services;

public class FundService : IFundService
{
    private const string DefaultInUseMessage =
        "Quỹ mặc định không thể xóa hoặc đổi loại khi còn quỹ khác cùng loại. Chọn quỹ mặc định khác trước.";

    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;
    private readonly ICashDebtLock _cashDebtLock;
    private readonly IFundBalanceGuard _fundGuard;

    public FundService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch,
        ITransactionRunner transaction, IInventoryLock inventoryLock, ICashDebtLock cashDebtLock,
        IFundBalanceGuard fundGuard)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
        _cashDebtLock = cashDebtLock;
        _fundGuard = fundGuard;
    }

    public async Task<IReadOnlyList<FundDto>> ListAsync(FundListRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var query = _db.Funds.AsNoTracking().Where(f => f.BranchId == branchId);
        if (request.Kind.HasValue)
            query = query.Where(f => f.Kind == request.Kind.Value);
        if (request.IsActive.HasValue)
            query = query.Where(f => f.IsActive == request.IsActive.Value);

        return await query.OrderBy(f => f.Kind).ThenBy(f => f.Code)
            .Select(f => ToDto(f))
            .ToListAsync(ct);
    }

    public async Task<FundDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var fund = await _db.Funds.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.BranchId == branchId, ct)
            ?? throw new NotFoundException(nameof(Fund), id);
        return ToDto(fund);
    }

    public async Task<FundDto> CreateAsync(CreateFundRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var code = request.Code.Trim();

        var id = await _transaction.RunAsync(async c =>
        {
            if (await _db.Funds.AnyAsync(f => f.BranchId == branchId && f.Code == code, c))
                throw new ConflictException($"Mã quỹ '{code}' đã tồn tại.");

            // The first fund of a kind is the default even when the request says otherwise (P18).
            var makeDefault = request.IsDefault
                || !await _db.Funds.AnyAsync(f => f.BranchId == branchId && f.Kind == request.Kind, c);
            EnsureDefaultIsActive(makeDefault, request.IsActive);
            if (makeDefault)
                await ClearDefaultsAsync(branchId, request.Kind, null, c);

            var fund = new Fund
            {
                BranchId = branchId,
                Code = code,
                Name = request.Name.Trim(),
                Kind = request.Kind,
                BankName = Normalize(request.BankName),
                AccountNumber = Normalize(request.AccountNumber),
                IsDefault = makeDefault,
                IsActive = request.IsActive,
            };
            _db.Funds.Add(fund);
            await _db.SaveChangesAsync(c);
            return fund.Id;
        }, ct);
        return await GetAsync(id, ct);
    }

    public async Task<FundDto> UpdateAsync(Guid id, UpdateFundRequest request, CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var fund = await LockAsync(id, c);
            var kindChanged = fund.Kind != request.Kind;
            if (kindChanged)
            {
                await EnsureNotUsedAsync(fund, c);
                if (fund.IsDefault
                    && await _db.Funds.AnyAsync(f => f.BranchId == fund.BranchId && f.Kind == fund.Kind && f.Id != id, c))
                    throw new ConflictException(DefaultInUseMessage);
            }
            else if (fund.IsDefault && !request.IsDefault)
                throw Invalid("isDefault", "Chọn quỹ mặc định khác trước.");

            // Alone in its (new) kind → default, whatever the request says.
            var makeDefault = request.IsDefault
                || !await _db.Funds.AnyAsync(f => f.BranchId == fund.BranchId && f.Kind == request.Kind && f.Id != id, c);
            EnsureDefaultIsActive(makeDefault, request.IsActive);

            if (makeDefault)
                await ClearDefaultsAsync(fund.BranchId, request.Kind, id, c);

            fund.Name = request.Name.Trim();
            fund.Kind = request.Kind;
            fund.BankName = Normalize(request.BankName);
            fund.AccountNumber = Normalize(request.AccountNumber);
            fund.IsDefault = makeDefault;
            fund.IsActive = request.IsActive;
            await _db.SaveChangesAsync(c);
        }, ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var fund = await LockAsync(id, c);
            if (fund.IsDefault
                && await _db.Funds.AnyAsync(f => f.BranchId == fund.BranchId && f.Kind == fund.Kind && f.Id != id, c))
                throw new ConflictException(DefaultInUseMessage);
            await EnsureNotUsedAsync(fund, c);

            fund.IsDeleted = true;
            fund.DeletedAt = _clock.UtcNow;
            fund.DeletedBy = _currentUser.UserId;
            await _db.SaveChangesAsync(c);
        }, ct);
    }

    public async Task<FundDto> SetOpeningBalanceAsync(Guid id, SetFundOpeningBalanceRequest request,
        CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var fund = await LockAsync(id, c);

            // Read after the locks (P14): the opening balance sits at the branch opening date.
            var branch = await _db.Branches.AsNoTracking().Where(b => b.Id == fund.BranchId)
                .Select(b => new { b.OpeningDate, b.LockedUntil }).SingleAsync(c);
            var openingDate = branch.OpeningDate
                ?? throw new DomainException("OPENING_DATE_NOT_SET", "Chưa đặt ngày đầu kỳ của chi nhánh.");
            PeriodLockGuard.EnsureNotLocked(branch.LockedUntil, new[] { openingDate });

            // The opening amount precedes every movement, so the whole history is affected.
            var capture = await _fundGuard.CaptureAsync(new[] { (fund.Id, DateTimeOffset.MinValue) }, c);
            fund.OpeningAmount = request.Amount;
            await _db.SaveChangesAsync(c);
            await _fundGuard.CheckAsync(capture, request.AcknowledgeNegativeFund, c);
        }, ct);
        return await GetAsync(id, ct);
    }

    /// A fund that has opening money or is used by a document cannot be deleted or change kind.
    /// Phases 06 and 07 add fund transfers and stock vouchers here.
    private async Task EnsureNotUsedAsync(Fund fund, CancellationToken ct)
    {
        if (fund.OpeningAmount != 0)
            throw new ConflictException("Quỹ đã có số dư đầu kỳ, không thể xóa hoặc đổi loại.");
        if (await _db.CashVouchers.AnyAsync(v => v.FundId == fund.Id, ct))
            throw new ConflictException("Quỹ đã có phiếu thu/chi, không thể xóa hoặc đổi loại.");
    }

    /// Shared branch gate, then the fund key (P14), so the in-use and opening checks see every committed voucher.
    /// Returns the tracked fund of the working branch.
    private async Task<Fund> LockAsync(Guid id, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        await _cashDebtLock.AcquireAsync(branchId, Array.Empty<Guid>(), new[] { id }, ct);
        return await FindAsync(id, ct);
    }

    private static void EnsureDefaultIsActive(bool isDefault, bool isActive)
    {
        if (isDefault && !isActive)
            throw Invalid("isActive", "Quỹ mặc định không được ngừng sử dụng. Chọn quỹ mặc định khác trước.");
    }

    private async Task ClearDefaultsAsync(Guid branchId, FundKind kind, Guid? exceptId, CancellationToken ct)
    {
        var others = await _db.Funds
            .Where(f => f.BranchId == branchId && f.Kind == kind && f.IsDefault && f.Id != exceptId)
            .ToListAsync(ct);
        foreach (var other in others)
            other.IsDefault = false;
        // Saved before the new default is set, so the unique index never sees two defaults in one save.
        if (others.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private async Task<Fund> FindAsync(Guid id, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        return await _db.Funds.FirstOrDefaultAsync(f => f.Id == id && f.BranchId == branchId, ct)
            ?? throw new NotFoundException(nameof(Fund), id);
    }

    private static ValidationDomainException Invalid(string key, string message)
        => new(new Dictionary<string, string[]> { [key] = new[] { message } }, null);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FundDto ToDto(Fund f) => new()
    {
        Id = f.Id,
        BranchId = f.BranchId,
        Code = f.Code,
        Name = f.Name,
        Kind = f.Kind,
        BankName = f.BankName,
        AccountNumber = f.AccountNumber,
        IsDefault = f.IsDefault,
        OpeningAmount = f.OpeningAmount,
        IsActive = f.IsActive,
    };
}
