using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.FundTransfers.Interfaces;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Organization.Branches;
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

    /// Write operations return the saved transfer without re-checking the branch: the write already did.
    private async Task<FundTransferDto> LoadDtoAsync(Guid id, CancellationToken ct, bool checkBranch = false)
    {
        // IgnoreQueryFilters keeps the transfer visible when a fund or the owner was soft-deleted later; the
        // transfer's own soft-delete filter is applied by hand.
        var row = await _db.FundTransfers.IgnoreQueryFilters().AsNoTracking()
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

    private static readonly DateTimeOffset MinVoucherAt = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// Same range as cash and stock vouchers. An out-of-range date is reported by the rules (400 `voucherAt`), so the
    /// period lock check skips it: VnTime cannot convert dates near DateTimeOffset.MaxValue.
    private bool IsValidVoucherAt(DateTimeOffset voucherAt) =>
        voucherAt >= MinVoucherAt && voucherAt <= _clock.UtcNow.AddYears(1);

    /// `peek` reads the next number without using it; otherwise the counter is incremented (the counter row is the
    /// last lock of a save, P14). Codes already used in the branch are skipped (see DocumentCodeAllocator).
    private Task<string> NextCodeAsync(Guid branchId, DateTimeOffset voucherAt, bool peek, CancellationToken ct) =>
        _codes.NextAsync(DocumentType.FundTransfer, branchId, voucherAt, peek, async (codes, c) =>
            // Same scope as the unique index (branch_id, code) WHERE is_deleted = false: the query filter matches it.
            (await _db.FundTransfers
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
        _db.CashDocumentActivities.Add(new CashDocumentActivity
        {
            DocumentType = DocumentType.FundTransfer,
            DocumentId = transfer.Id,
            Action = action,
            ActorUserId = _currentUser.UserId,
            OccurredAt = _clock.UtcNow,
            Description = description,
        });

    // ---- Guards -----------------------------------------------------------------------------

    private async Task EnsureWorkingBranchAsync(Guid branchId, CancellationToken ct)
    {
        if (branchId != await _currentBranch.GetIdAsync(ct))
            throw new ForbiddenException("Phiếu thuộc chi nhánh khác.");
    }
}
