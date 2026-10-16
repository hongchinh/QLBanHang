using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.FundTransfers.Interfaces;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Organization.Branches;
using OrderMgmt.Application.Sales.Quotations.Helpers;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.FundTransfers.Services;

/// Fund transfers: money moves from one fund of the branch to another; debt is never touched. Every write follows the
/// Phase 05 steps table without the debt calls, in one transaction.
public class FundTransferService : IFundTransferService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;
    private readonly ICashDebtLock _cashDebtLock;
    private readonly IDocumentCodeAllocator _codes;
    private readonly IFundBalanceGuard _fundGuard;

    public FundTransferService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock,
        ITransactionRunner transaction, IInventoryLock inventoryLock, ICashDebtLock cashDebtLock,
        IDocumentCodeAllocator codes, IFundBalanceGuard fundGuard)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
        _cashDebtLock = cashDebtLock;
        _codes = codes;
        _fundGuard = fundGuard;
    }

    public async Task<FundTransferListResult> ListAsync(FundTransferListRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        // IgnoreQueryFilters keeps transfers whose fund or owner was soft-deleted later (as in LoadDtoAsync); the
        // transfers' own soft-delete filter is applied by hand.
        var query = _db.FundTransfers.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Where(t => !t.IsDeleted && t.BranchId == branchId);

        // VN dates as UTC ranges (D27).
        if (request.From is { } from)
        {
            var start = VnTime.StartOfDay(from);
            query = query.Where(t => t.VoucherAt >= start);
        }
        if (request.To is { } to)
        {
            var end = VnTime.StartOfNextDay(to);
            query = query.Where(t => t.VoucherAt < end);
        }
        if (request.FundId is { } fundId)
            query = query.Where(t => t.FromFundId == fundId || t.ToFundId == fundId);
        if (request.Status != DocumentStatusFilter.All)
        {
            var status = (DocumentStatus)request.Status;
            query = query.Where(t => t.Status == status);
        }

        var ownerIds = OwnerIdListParser.Parse(request.OwnerIds);
        if (ownerIds.Count > 0)
            query = query.Where(t => ownerIds.Contains(t.OwnerUserId));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{LikePattern.Escape(request.Search.Trim())}%";
            query = query.Where(t => EF.Functions.ILike(t.Code, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(t.Description!, pattern, LikePattern.EscapeCharacter));
        }

        var totalItems = await query.CountAsync(ct);
        var totalAmount = await query.SumAsync(t => t.Amount, ct);
        var items = await query
            .OrderByDescending(t => t.VoucherAt)
            .ThenByDescending(t => t.Code)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new FundTransferListItemDto
            {
                Id = t.Id,
                Code = t.Code,
                VoucherAt = t.VoucherAt,
                FromFundCode = t.FromFund!.Code,
                FromFundName = t.FromFund.Name,
                ToFundCode = t.ToFund!.Code,
                ToFundName = t.ToFund.Name,
                Amount = t.Amount,
                Description = t.Description,
                Status = t.Status,
                OwnerUserId = t.OwnerUserId,
                OwnerName = t.Owner != null ? t.Owner.FullName : null,
            })
            .ToListAsync(ct);

        return new FundTransferListResult
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
            TotalAmount = totalAmount,
        };
    }

    /// Form defaults: NextCode is a peek and does not use a number.
    public async Task<FundTransferDefaultsDto> GetDefaultsAsync(CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var now = _clock.UtcNow;
        return new FundTransferDefaultsDto
        {
            NextCode = await NextCodeAsync(branchId, now, peek: true, ct),
            VoucherAt = now,
        };
    }

    /// Newest first.
    public async Task<IReadOnlyList<CashDocumentActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await _db.FundTransfers.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => (Guid?)t.BranchId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(FundTransfer), id);
        await EnsureWorkingBranchAsync(branchId, ct);

        return await CashDocuments.ListActivitiesAsync(_db, DocumentType.FundTransfer, id, ct);
    }

    public Task<FundTransferDto> GetAsync(Guid id, CancellationToken ct = default) =>
        LoadDtoAsync(id, ct, checkBranch: true);

    public async Task<FundTransferDto> CreateAsync(UpsertFundTransferRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        var id = await _transaction.RunAsync(async c =>
        {
            await AcquireLocksAsync(branchId, new[] { request.FromFundId, request.ToFundId }, c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil, new[] { voucherAt }.Where(IsValidVoucherAt));
            await ValidateAsync(request, branchId, existing: null, c);
            await OpeningDateGuard.EnsureOnOrAfterAsync(_db, branchId, voucherAt, c);
            // Every fund involved: the source loses the amount (the one the check can flag), the destination gains it.
            var fundCapture = await _fundGuard.CaptureAsync(
                new[] { (request.FromFundId, voucherAt), (request.ToFundId, voucherAt) }, c);

            var transfer = new FundTransfer
            {
                Code = await NextCodeAsync(branchId, voucherAt, peek: false, c),
                BranchId = branchId,
                OwnerUserId = _currentUser.UserId ?? throw new UnauthorizedAccessException(),
            };
            _db.FundTransfers.Add(transfer);
            ApplyRequest(transfer, request, voucherAt);
            await _db.SaveChangesAsync(c);

            await _fundGuard.CheckAsync(fundCapture, request.AcknowledgeNegativeFund, c);
            AddActivity(transfer, CashDocumentActivityAction.Created, "Tạo phiếu");
            await _db.SaveChangesAsync(c);
            return transfer.Id;
        }, ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<FundTransferDto> UpdateAsync(Guid id, UpsertFundTransferRequest request, CancellationToken ct = default)
    {
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        await _transaction.RunAsync(async c =>
        {
            var transfer = await LoadForWriteAsync(id, request.Version, DocumentStatus.Active, c);
            var oldFunds = new[] { transfer.FromFundId, transfer.ToFundId };
            var newFunds = new[] { request.FromFundId, request.ToFundId };
            await AcquireLocksAsync(transfer.BranchId, oldFunds.Concat(newFunds), c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == transfer.BranchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil,
                new[] { transfer.VoucherAt }.Concat(new[] { voucherAt }.Where(IsValidVoucherAt)));
            await ValidateAsync(request, transfer.BranchId, transfer, c);
            await OpeningDateGuard.EnsureOnOrAfterAsync(_db, transfer.BranchId, voucherAt, c);
            // Every fund involved, old and new, from the earlier of the two dates: an old destination that loses the
            // transfer is checked as well as a new or changed source.
            var from = transfer.VoucherAt < voucherAt ? transfer.VoucherAt : voucherAt;
            var fundCapture = await _fundGuard.CaptureAsync(oldFunds.Concat(newFunds).Select(f => (f, from)), c);

            ApplyRequest(transfer, request, voucherAt);
            await _db.SaveChangesAsync(c);

            await _fundGuard.CheckAsync(fundCapture, request.AcknowledgeNegativeFund, c);
            AddActivity(transfer, CashDocumentActivityAction.Updated, "Cập nhật phiếu");
            await _db.SaveChangesAsync(c);
        }, ct);

        return await LoadDtoAsync(id, ct);
    }

    /// Cancelling takes the amount back out of the destination fund, so that fund is the one the check can flag.
    public async Task<FundTransferDto> CancelAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default)
    {
        await ChangeLifecycleAsync(id, request, DocumentStatus.Active, CashDocumentActivityAction.Cancelled, "Hủy phiếu",
            (t, _) =>
            {
                t.Status = DocumentStatus.Cancelled;
                t.CancelledAt = _clock.UtcNow;
                t.CancelledBy = _currentUser.UserId;
                return Task.CompletedTask;
            }, ct);
        return await LoadDtoAsync(id, ct);
    }

    /// Uses the cancel permission. Restoring takes the amount out of the source fund again.
    public async Task<FundTransferDto> RestoreAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default)
    {
        await ChangeLifecycleAsync(id, request, DocumentStatus.Cancelled, CashDocumentActivityAction.Restored,
            "Khôi phục phiếu", async (t, c) =>
            {
                await EnsureFundsExistAsync(t, c);
                t.Status = DocumentStatus.Active;
                t.CancelledAt = null;
                t.CancelledBy = null;
            }, ct);
        return await LoadDtoAsync(id, ct);
    }

    /// Soft delete in any status; deleting an Active transfer reduces the destination fund like a cancel.
    public Task DeleteAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default) =>
        ChangeLifecycleAsync(id, request, requiredStatus: null, CashDocumentActivityAction.Deleted, "Xóa phiếu",
            (t, _) =>
            {
                t.IsDeleted = true;
                return Task.CompletedTask;
            }, ct);

    /// Cancel / restore / delete per the steps table without the debt calls: locks, period lock on the stored date,
    /// capture of both funds, the operation's own steps (`apply`), save, the negative-fund check, then the activity.
    private Task ChangeLifecycleAsync(Guid id, CashDocumentActionRequest request, DocumentStatus? requiredStatus,
        CashDocumentActivityAction action, string description, Func<FundTransfer, CancellationToken, Task> apply,
        CancellationToken ct) =>
        _transaction.RunAsync(async c =>
        {
            var transfer = await LoadForWriteAsync(id, request.Version, requiredStatus, c);
            var funds = new[] { transfer.FromFundId, transfer.ToFundId };
            await AcquireLocksAsync(transfer.BranchId, funds, c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == transfer.BranchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil, new[] { transfer.VoucherAt });
            var fundCapture = await _fundGuard.CaptureAsync(funds.Select(f => (f, transfer.VoucherAt)), c);

            await apply(transfer, c);
            await _db.SaveChangesAsync(c);
            await _fundGuard.CheckAsync(fundCapture, request.AcknowledgeNegativeFund, c);
            AddActivity(transfer, action, description);
            await _db.SaveChangesAsync(c);
        }, ct);

    /// Restore: both funds must still exist (not soft-deleted); 400 with their keys.
    private async Task EnsureFundsExistAsync(FundTransfer transfer, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!await _db.Funds.AnyAsync(f => f.Id == transfer.FromFundId, ct))
            errors["fromFundId"] = new[] { "Quỹ đi đã bị xóa." };
        if (!await _db.Funds.AnyAsync(f => f.Id == transfer.ToFundId, ct))
            errors["toFundId"] = new[] { "Quỹ đến đã bị xóa." };

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    /// Update, cancel, restore and delete (the controller checks their permission): working branch, ownership,
    /// status and version. `requiredStatus` null accepts any status. The header is always marked modified so the
    /// xmin check runs (D29).
    private async Task<FundTransfer> LoadForWriteAsync(Guid id, uint? version, DocumentStatus? requiredStatus,
        CancellationToken ct)
    {
        var transfer = await _db.FundTransfers.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException(nameof(FundTransfer), id);

        await EnsureWorkingBranchAsync(transfer.BranchId, ct);
        if (transfer.OwnerUserId != _currentUser.UserId
            && !_currentUser.HasPermission(Permissions.FundTransfers.EditAll))
            throw new ForbiddenException("Bạn chỉ được thao tác trên phiếu do mình tạo.");
        if (requiredStatus is { } required && transfer.Status != required)
            throw new ConflictException(required == DocumentStatus.Active
                ? "Phiếu đã hủy, không thể thay đổi."
                : "Phiếu chưa bị hủy.");
        if (version is null)
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["version"] = new[] { "Thiếu phiên bản dữ liệu của phiếu." } }, null);

        _db.Entry(transfer).Property(t => t.Version).OriginalValue = version.Value;
        transfer.UpdatedAt = _clock.UtcNow;
        transfer.UpdatedBy = _currentUser.UserId;
        return transfer;
    }

    /// Write operations return the saved transfer without re-checking the branch: the write already did.
    private async Task<FundTransferDto> LoadDtoAsync(Guid id, CancellationToken ct, bool checkBranch = false)
    {
        // IgnoreQueryFilters keeps the transfer visible when a fund or the owner was soft-deleted later; the
        // transfer's own soft-delete filter is applied by hand.
        var row = await _db.FundTransfers.IgnoreQueryFilters([QueryFilters.SoftDelete]).AsNoTracking()
            .Where(t => t.Id == id && !t.IsDeleted)
            .Select(t => new
            {
                Transfer = t,
                FromFundCode = t.FromFund!.Code,
                FromFundName = t.FromFund.Name,
                FromFundKind = t.FromFund.Kind,
                ToFundCode = t.ToFund!.Code,
                ToFundName = t.ToFund.Name,
                ToFundKind = t.ToFund.Kind,
                OwnerName = t.Owner != null ? t.Owner.FullName : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(FundTransfer), id);
        var transfer = row.Transfer;

        if (checkBranch)
            await EnsureWorkingBranchAsync(transfer.BranchId, ct);

        // Every write is rejected inside a locked period; another user's transfer needs edit_all.
        var lockedUntil = await _db.Branches.Where(b => b.Id == transfer.BranchId).Select(b => b.LockedUntil).FirstOrDefaultAsync(ct);
        var mayAct = !(lockedUntil is { } locked && VnTime.ToVnDate(transfer.VoucherAt) <= locked)
            && (transfer.OwnerUserId == _currentUser.UserId
                || _currentUser.HasPermission(Permissions.FundTransfers.EditAll));
        var active = transfer.Status == DocumentStatus.Active;

        return new FundTransferDto
        {
            Id = transfer.Id,
            Code = transfer.Code,
            VoucherAt = transfer.VoucherAt,
            BranchId = transfer.BranchId,
            FromFundId = transfer.FromFundId,
            FromFundCode = row.FromFundCode,
            FromFundName = row.FromFundName,
            FromFundKind = row.FromFundKind,
            ToFundId = transfer.ToFundId,
            ToFundCode = row.ToFundCode,
            ToFundName = row.ToFundName,
            ToFundKind = row.ToFundKind,
            Amount = transfer.Amount,
            Description = transfer.Description,
            Note = transfer.Note,
            Status = transfer.Status,
            CancelledAt = transfer.CancelledAt,
            OwnerUserId = transfer.OwnerUserId,
            OwnerName = row.OwnerName,
            Version = transfer.Version,
            CanEdit = mayAct && active && _currentUser.HasPermission(Permissions.FundTransfers.Edit),
            CanDelete = mayAct && _currentUser.HasPermission(Permissions.FundTransfers.Delete),
            // Also true for a Cancelled transfer: restore uses the same permission.
            CanCancel = mayAct && _currentUser.HasPermission(Permissions.FundTransfers.Cancel),
        };
    }

    // ---- Writes -----------------------------------------------------------------------------

    /// The shared branch gate, then the fund keys (P14). A transfer has no partner and no reason.
    private async Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> fundIds, CancellationToken ct)
    {
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        await _cashDebtLock.AcquireAsync(branchId, Array.Empty<Guid>(), fundIds.Distinct(), ct);
    }

    /// Business rules and the voucher date range, collected into one 400. `existing` is null on create. Runs after
    /// the locks: the fund keys keep both funds from being edited or deleted before the save commits.
    private async Task ValidateAsync(UpsertFundTransferRequest request, Guid branchId, FundTransfer? existing,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors.TryAdd(key, new[] { message });

        if (!IsValidVoucherAt(VnTime.ToUtc(request.VoucherAt)))
            Add("voucherAt", "Ngày chứng từ không hợp lệ.");

        if (request.ToFundId == request.FromFundId)
            Add("toFundId", "Quỹ đến phải khác quỹ đi.");

        var fundIds = new[] { request.FromFundId, request.ToFundId };
        var funds = await _db.Funds.AsNoTracking()
            .Where(f => fundIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);

        void CheckFund(string key, Guid fundId, Guid? storedFundId)
        {
            if (!funds.TryGetValue(fundId, out var fund) || fund.BranchId != branchId)
                Add(key, "Quỹ không thuộc chi nhánh đang làm việc.");
            // Only a newly chosen fund must be active, so old transfers stay editable.
            else if (!fund.IsActive && storedFundId != fundId)
                Add(key, "Quỹ đã ngừng sử dụng.");
        }

        CheckFund("fromFundId", request.FromFundId, existing?.FromFundId);
        CheckFund("toFundId", request.ToFundId, existing?.ToFundId);

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    /// Same range as cash and stock vouchers. An out-of-range date is reported by the rules (400 `voucherAt`), so the
    /// period lock check skips it: VnTime cannot convert dates near DateTimeOffset.MaxValue.
    private bool IsValidVoucherAt(DateTimeOffset voucherAt) =>
        CashDocuments.IsValidVoucherAt(voucherAt, _clock.UtcNow);

    /// `peek` reads the next number without using it; otherwise the counter is incremented (the counter row is the
    /// last lock of a save, P14). Codes already used in the branch are skipped (see DocumentCodeAllocator).
    private Task<string> NextCodeAsync(Guid branchId, DateTimeOffset voucherAt, bool peek, CancellationToken ct) =>
        _codes.NextAsync(DocumentType.FundTransfer, branchId, voucherAt, peek, async (codes, c) =>
            // Soft-deleted rows count too: a deleted document's number is never issued again, even when a reset-policy
            // change restarts the counter on codes of the old period (the unique index only covers live rows).
            (await _db.FundTransfers.IgnoreQueryFilters([QueryFilters.SoftDelete])
                .Where(t => t.BranchId == branchId && codes.Contains(t.Code))
                .Select(t => t.Code)
                .ToListAsync(c)).ToHashSet(), ct);

    private static void ApplyRequest(FundTransfer transfer, UpsertFundTransferRequest request, DateTimeOffset voucherAt)
    {
        transfer.VoucherAt = voucherAt;
        transfer.FromFundId = request.FromFundId;
        transfer.ToFundId = request.ToFundId;
        transfer.Amount = request.Amount;
        transfer.Description = request.Description;
        transfer.Note = request.Note;
    }

    private void AddActivity(FundTransfer transfer, CashDocumentActivityAction action, string description) =>
        _db.CashDocumentActivities.Add(CashDocuments.NewActivity(
            DocumentType.FundTransfer, transfer.Id, action, _currentUser.UserId, _clock.UtcNow, description));

    // ---- Guards -----------------------------------------------------------------------------

    private Task EnsureWorkingBranchAsync(Guid branchId, CancellationToken ct) =>
        CashDocuments.EnsureWorkingBranchAsync(_currentBranch, branchId, "Phiếu thuộc chi nhánh khác.", ct);
}
