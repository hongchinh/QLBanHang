using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.DebtOffsets.Interfaces;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Organization.Branches;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.DebtOffsets.Services;

/// Debt offset vouchers (P23): a partner's open receivable documents offset against their open payable documents.
/// The voucher posts R− and P− of the amount and settles every item against its side's entry (origin Offset, batch =
/// the voucher id). Every write runs in one transaction under the branch gate and the partner key (P14).
public class DebtOffsetService : IDebtOffsetService
{
    private const string EntryDescription = "Bù trừ công nợ";

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;
    private readonly ICashDebtLock _cashDebtLock;
    private readonly IDocumentCodeAllocator _codes;
    private readonly IDebtLedgerService _ledger;
    private readonly IDebtQueryService _debtQuery;

    public DebtOffsetService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock,
        ITransactionRunner transaction, IInventoryLock inventoryLock, ICashDebtLock cashDebtLock,
        IDocumentCodeAllocator codes, IDebtLedgerService ledger, IDebtQueryService debtQuery)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
        _cashDebtLock = cashDebtLock;
        _codes = codes;
        _ledger = ledger;
        _debtQuery = debtQuery;
    }

    public async Task<DebtOffsetListResult> ListAsync(DebtOffsetListRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        // IgnoreQueryFilters keeps offsets whose partner or owner was soft-deleted later (as in LoadDtoAsync); the
        // offsets' own soft-delete filter is applied by hand.
        var query = _db.DebtOffsetVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(o => !o.IsDeleted && o.BranchId == branchId);

        // VN dates as UTC ranges (D27).
        if (request.From is { } from)
        {
            var start = VnTime.StartOfDay(from);
            query = query.Where(o => o.VoucherAt >= start);
        }
        if (request.To is { } to)
        {
            var end = VnTime.StartOfNextDay(to);
            query = query.Where(o => o.VoucherAt < end);
        }
        if (request.PartnerId is { } partnerId)
            query = query.Where(o => o.PartnerId == partnerId);
        if (request.Status != DocumentStatusFilter.All)
        {
            var status = (DocumentStatus)request.Status;
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{LikePattern.Escape(request.Search.Trim())}%";
            query = query.Where(o => EF.Functions.ILike(o.Code, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(o.PartnerName, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(o.Note!, pattern, LikePattern.EscapeCharacter));
        }

        var totalItems = await query.CountAsync(ct);
        var totalAmount = await query.SumAsync(o => o.Amount, ct);
        var items = await query
            .OrderByDescending(o => o.VoucherAt)
            .ThenByDescending(o => o.Code)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new DebtOffsetListItemDto
            {
                Id = o.Id,
                Code = o.Code,
                VoucherAt = o.VoucherAt,
                PartnerCode = o.Partner != null ? o.Partner.Code : null,
                PartnerName = o.PartnerName,
                Amount = o.Amount,
                Note = o.Note,
                Status = o.Status,
                OwnerName = o.Owner != null ? o.Owner.FullName : null,
            })
            .ToListAsync(ct);

        return new DebtOffsetListResult
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
            TotalAmount = totalAmount,
        };
    }

    /// Form defaults: NextCode is a peek and does not use a number.
    public async Task<DebtOffsetDefaultsDto> GetDefaultsAsync(CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var now = _clock.UtcNow;
        return new DebtOffsetDefaultsDto
        {
            NextCode = await NextCodeAsync(branchId, now, peek: true, ct),
            VoucherAt = now,
        };
    }

    /// Newest first.
    public async Task<IReadOnlyList<CashDocumentActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await _db.DebtOffsetVouchers.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => (Guid?)o.BranchId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(DebtOffsetVoucher), id);
        await EnsureWorkingBranchAsync(branchId, ct);

        return await CashDocuments.ListActivitiesAsync(_db, DocumentType.DebtOffset, id, ct);
    }

    public Task<DebtOffsetDto> GetAsync(Guid id, CancellationToken ct = default) =>
        LoadDtoAsync(id, ct, checkBranch: true);

    public async Task<DebtOffsetDto> CreateAsync(CreateDebtOffsetRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        var id = await _transaction.RunAsync(async c =>
        {
            await AcquireLocksAsync(branchId, request.PartnerId, c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil, new[] { voucherAt }.Where(IsValidVoucherAt));
            var partner = await ValidateAsync(request, branchId, voucherAt, c);
            await OpeningDateGuard.EnsureOnOrAfterAsync(_db, branchId, voucherAt, c);

            var offset = new DebtOffsetVoucher
            {
                Code = await NextCodeAsync(branchId, voucherAt, peek: false, c),
                VoucherAt = voucherAt,
                BranchId = branchId,
                PartnerId = partner.Id,
                PartnerName = partner.Name,
                // Never from the client: both sides are equal (validated).
                Amount = request.Receivables.Sum(a => a.Amount),
                Note = request.Note,
                OwnerUserId = _currentUser.UserId ?? throw new UnauthorizedAccessException(),
            };
            _db.DebtOffsetVouchers.Add(offset);
            await _db.SaveChangesAsync(c);

            var entries = await _ledger.PostAsync(DebtSourceType.Offset, offset.Id,
                new[] { DraftOf(offset, DebtSide.Receivable), DraftOf(offset, DebtSide.Payable) },
                confirmRemoveSettlements: false, ct: c);
            await SettleAsync(offset, entries.Single(e => e.Side == DebtSide.Receivable), request.Receivables,
                "receivables", c);
            await SettleAsync(offset, entries.Single(e => e.Side == DebtSide.Payable), request.Payables, "payables", c);

            AddActivity(offset, CashDocumentActivityAction.Created, "Tạo chứng từ");
            await _db.SaveChangesAsync(c);
            return offset.Id;
        }, ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<DebtOffsetDto> CancelAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default)
    {
        await ChangeLifecycleAsync(id, request, DocumentStatus.Active, CashDocumentActivityAction.Cancelled,
            "Hủy chứng từ", async (o, c) =>
            {
                await RemoveDebtAsync(o, request.ConfirmRemoveSettlements, c);
                o.Status = DocumentStatus.Cancelled;
                o.CancelledAt = _clock.UtcNow;
                o.CancelledBy = _currentUser.UserId;
            }, ct);
        return await LoadDtoAsync(id, ct);
    }

    public Task DeleteAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default) =>
        ChangeLifecycleAsync(id, request, requiredStatus: null, CashDocumentActivityAction.Deleted, "Xóa chứng từ",
            async (o, c) =>
            {
                // Only an Active offset still has its entries and settlements.
                if (o.Status == DocumentStatus.Active)
                    await RemoveDebtAsync(o, request.ConfirmRemoveSettlements, c);
                o.IsDeleted = true;
            }, ct);

    /// Cancel / delete: locks, the period lock on the stored date, the operation's own steps (`apply`), save, then the
    /// activity.
    private Task ChangeLifecycleAsync(Guid id, CashDocumentActionRequest request, DocumentStatus? requiredStatus,
        CashDocumentActivityAction action, string description, Func<DebtOffsetVoucher, CancellationToken, Task> apply,
        CancellationToken ct) =>
        _transaction.RunAsync(async c =>
        {
            var offset = await LoadForWriteAsync(id, request.Version, requiredStatus, c);
            await AcquireLocksAsync(offset.BranchId, offset.PartnerId, c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == offset.BranchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil, new[] { offset.VoucherAt });

            await apply(offset, c);
            await _db.SaveChangesAsync(c);
            AddActivity(offset, action, description);
            await _db.SaveChangesAsync(c);
        }, ct);

    /// Removes the offset's Offset settlements (its own: no confirmation, P23) and its two entries. Any other
    /// settlement on its entries (an entry freed when a settled document was cancelled or shrunk, brainstorm §5, then
    /// matched manually or allocated by a voucher) belongs to another document, so it asks first (P10, P21). A stock
    /// voucher among the entries those settlements joined gets its Auto settlement recomputed before the commit
    /// (AutoRecomputeHook), since the freed room goes back to its paid amount (Phase 07 rule, 2026-10-09).
    private async Task RemoveDebtAsync(DebtOffsetVoucher offset, bool confirmRemoveSettlements, CancellationToken ct)
    {
        await _ledger.RemoveOwnSettlementsAsync(DebtSourceType.Offset, offset.Id, SettlementOrigin.Offset, ct);
        await _ledger.RemoveSourceAsync(DebtSourceType.Offset, offset.Id, confirmRemoveSettlements, ct: ct);
    }

    /// Cancel and delete (the controller checks their permission): working branch, status and version. There is no
    /// debt_offset.edit_all: any holder of the permission may act on any offset of the branch. `requiredStatus` null
    /// accepts any status. The header is always marked modified so the xmin check runs (D29).
    private async Task<DebtOffsetVoucher> LoadForWriteAsync(Guid id, uint? version, DocumentStatus? requiredStatus,
        CancellationToken ct)
    {
        var offset = await _db.DebtOffsetVouchers.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException(nameof(DebtOffsetVoucher), id);

        await EnsureWorkingBranchAsync(offset.BranchId, ct);
        if (requiredStatus is { } required && offset.Status != required)
            throw new ConflictException("Chứng từ đã hủy, không thể thay đổi.");
        if (version is null)
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["version"] = new[] { "Thiếu phiên bản dữ liệu của chứng từ." } }, null);

        _db.Entry(offset).Property(o => o.Version).OriginalValue = version.Value;
        offset.UpdatedAt = _clock.UtcNow;
        offset.UpdatedBy = _currentUser.UserId;
        return offset;
    }

    /// Write operations return the saved offset without re-checking the branch: the write already did.
    private async Task<DebtOffsetDto> LoadDtoAsync(Guid id, CancellationToken ct, bool checkBranch = false)
    {
        // IgnoreQueryFilters keeps the offset visible when its partner or owner was soft-deleted later; the offset's
        // own soft-delete filter is applied by hand.
        var row = await _db.DebtOffsetVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(o => o.Id == id && !o.IsDeleted)
            .Select(o => new
            {
                Offset = o,
                PartnerCode = o.Partner != null ? o.Partner.Code : null,
                OwnerName = o.Owner != null ? o.Owner.FullName : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(DebtOffsetVoucher), id);
        var offset = row.Offset;

        if (checkBranch)
            await EnsureWorkingBranchAsync(offset.BranchId, ct);

        // Every write is rejected inside a locked period.
        var lockedUntil = await _db.Branches.Where(b => b.Id == offset.BranchId).Select(b => b.LockedUntil).FirstOrDefaultAsync(ct);
        var mayAct = !(lockedUntil is { } locked && VnTime.ToVnDate(offset.VoucherAt) <= locked);

        return new DebtOffsetDto
        {
            Id = offset.Id,
            Code = offset.Code,
            VoucherAt = offset.VoucherAt,
            PartnerId = offset.PartnerId,
            PartnerCode = row.PartnerCode,
            PartnerName = offset.PartnerName,
            Amount = offset.Amount,
            Note = offset.Note,
            Status = offset.Status,
            CancelledAt = offset.CancelledAt,
            OwnerUserId = offset.OwnerUserId,
            OwnerName = row.OwnerName,
            Version = offset.Version,
            CanCancel = mayAct && offset.Status == DocumentStatus.Active
                        && _currentUser.HasPermission(Permissions.DebtOffsets.Cancel),
            CanDelete = mayAct && _currentUser.HasPermission(Permissions.DebtOffsets.Delete),
            Items = await ItemsOfAsync(offset.Id, ct),
        };
    }

    /// The documents the offset settles (its own Offset settlements, batch = the offset), receivables first. Other
    /// settlements that later use an entry the offset no longer covers are not items of the offset.
    private async Task<List<DebtOffsetItemDto>> ItemsOfAsync(Guid offsetId, CancellationToken ct)
    {
        var own = _db.DebtEntries
            .Where(e => e.SourceType == DebtSourceType.Offset && e.SourceId == offsetId)
            .Select(e => e.Id);
        var items = await (from s in _db.DebtSettlements.AsNoTracking()
                           join i in _db.DebtEntries.AsNoTracking() on s.IncreaseEntryId equals i.Id
                           where own.Contains(s.DecreaseEntryId)
                                 && s.Origin == SettlementOrigin.Offset && s.BatchId == offsetId
                           select new DebtOffsetItemDto
                           {
                               Side = i.Side,
                               EntryId = i.Id,
                               SourceType = i.SourceType,
                               SourceId = i.SourceId,
                               DocCode = i.DocCode,
                               DocDate = i.DocDate,
                               Amount = s.Amount,
                           })
            .ToListAsync(ct);
        return items
            .OrderBy(i => i.Side)
            .ThenBy(i => i.DocDate)
            .ThenBy(i => i.DocCode, StringComparer.Ordinal)
            .ToList();
    }

    // ---- Writes -----------------------------------------------------------------------------

    /// The shared branch gate, then the partner key (P14). An offset moves no money: no fund key.
    private async Task AcquireLocksAsync(Guid branchId, Guid partnerId, CancellationToken ct)
    {
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        await _cashDebtLock.AcquireAsync(branchId, new[] { partnerId }, Array.Empty<Guid>(), ct);
    }

    /// Business rules collected into one 400; runs after the locks, so the open amounts cannot change before the
    /// save commits. Each side is validated like a voucher's allocations whose own entry is a Decrease of Σ that side:
    /// only open Increase entries of the partner on that side are accepted.
    private async Task<Customer> ValidateAsync(CreateDebtOffsetRequest request, Guid branchId, DateTimeOffset voucherAt,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors.TryAdd(key, new[] { message });

        if (!IsValidVoucherAt(voucherAt))
            Add("voucherAt", "Ngày chứng từ không hợp lệ.");

        var partner = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PartnerId, ct);
        if (partner is null)
            Add("partnerId", "Đối tượng không tồn tại.");
        else if (partner.Status != CustomerStatus.Active)
            Add("partnerId", "Đối tượng đã ngừng hoạt động.");
        else if (!partner.IsCustomer || !partner.IsSupplier)
            Add("partnerId", "Đối tượng phải vừa là khách hàng vừa là nhà cung cấp.");

        if (request.Receivables.Count == 0)
            Add("receivables", "Chưa chọn chứng từ phải thu.");
        if (request.Payables.Count == 0)
            Add("payables", "Chưa chọn chứng từ phải trả.");

        // Open entries of both sides in one map, so an entry of the other side reads "not of this side".
        var open = new Dictionary<Guid, OpenEntry>();
        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            var items = await _debtQuery.OpenItemsAsync(new OpenItemsQuery
            {
                BranchId = branchId,
                PartnerId = request.PartnerId,
                Side = side,
            }, ct);
            foreach (var i in items)
                open[i.EntryId] = new OpenEntry(i.EntryId, branchId, request.PartnerId, side, i.Direction, i.Open, i.PostedAt);
        }

        void ValidateSide(DebtSide side, IReadOnlyList<AllocationRequest> items, string key)
        {
            var sideErrors = AllocationValidator.Validate(branchId, request.PartnerId, side, DebtDirection.Decrease,
                capacity: items.Sum(a => a.Amount), items.Select(a => new AllocationInput(a.EntryId, a.Amount)).ToList(),
                open, key);
            foreach (var (k, v) in sideErrors)
                errors.TryAdd(k, v);
        }

        ValidateSide(DebtSide.Receivable, request.Receivables, "receivables");
        ValidateSide(DebtSide.Payable, request.Payables, "payables");

        var receivableTotal = request.Receivables.Sum(a => a.Amount);
        var payableTotal = request.Payables.Sum(a => a.Amount);
        if (receivableTotal != payableTotal || receivableTotal <= 0)
            Add("payables", "Số tiền hai bên phải bằng nhau.");
        if (receivableTotal >= MaxMoney)
            Add("receivables", "Tổng tiền bù trừ quá lớn.");

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
        return partner!;
    }

    /// The offset's own entry of `side`: a Decrease of the amount at the voucher date, owned by the voucher's owner.
    private static DebtEntryDraft DraftOf(DebtOffsetVoucher offset, DebtSide side)
    {
        var docDate = VnTime.ToVnDate(offset.VoucherAt);
        return new DebtEntryDraft(offset.BranchId, offset.PartnerId, side, DebtDirection.Decrease, offset.Amount,
            offset.VoucherAt, docDate, DebtDates.EntryDueDate(DebtDirection.Decrease, docDate, null), offset.OwnerUserId,
            offset.Code, EntryDescription);
    }

    /// Settles each item (an Increase entry) against the offset's own entry of that side.
    private Task SettleAsync(DebtOffsetVoucher offset, DebtEntry own, IReadOnlyList<AllocationRequest> items,
        string errorKeyPrefix, CancellationToken ct) =>
        _ledger.AddSettlementsAsync(
            items.Select(a => new SettlementDraft(a.EntryId, own.Id, a.Amount)).ToList(),
            SettlementOrigin.Offset, offset.Id, errorKeyPrefix, ct);

    // Exclusive upper bound of numeric(18,2).
    private const decimal MaxMoney = 10_000_000_000_000_000m;

    /// Same range as cash vouchers and fund transfers. An out-of-range date is reported by the rules (400
    /// `voucherAt`), so the period lock check skips it: VnTime cannot convert dates near DateTimeOffset.MaxValue.
    private bool IsValidVoucherAt(DateTimeOffset voucherAt) =>
        CashDocuments.IsValidVoucherAt(voucherAt, _clock.UtcNow);

    /// `peek` reads the next number without using it; otherwise the counter is incremented (the counter row is the
    /// last lock of a save, P14). Codes already used in the branch are skipped (see DocumentCodeAllocator).
    private Task<string> NextCodeAsync(Guid branchId, DateTimeOffset voucherAt, bool peek, CancellationToken ct) =>
        _codes.NextAsync(DocumentType.DebtOffset, branchId, voucherAt, peek, async (codes, c) =>
            // Soft-deleted rows count too: a deleted document's number is never issued again, even when a reset-policy
            // change restarts the counter on codes of the old period (the unique index only covers live rows).
            (await _db.DebtOffsetVouchers.IgnoreQueryFilters()
                .Where(o => o.BranchId == branchId && codes.Contains(o.Code))
                .Select(o => o.Code)
                .ToListAsync(c)).ToHashSet(), ct);

    private void AddActivity(DebtOffsetVoucher offset, CashDocumentActivityAction action, string description) =>
        _db.CashDocumentActivities.Add(CashDocuments.NewActivity(
            DocumentType.DebtOffset, offset.Id, action, _currentUser.UserId, _clock.UtcNow, description));

    // ---- Guards -----------------------------------------------------------------------------

    private Task EnsureWorkingBranchAsync(Guid branchId, CancellationToken ct) =>
        CashDocuments.EnsureWorkingBranchAsync(_currentBranch, branchId, "Chứng từ thuộc chi nhánh khác.", ct);
}
