using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Interfaces;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.CashDebt.CashVouchers.Interfaces;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Organization.Branches;
using OrderMgmt.Application.Sales.Quotations.Helpers;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.CashVouchers.Services;

/// Manual receipts and payments. Every write follows the Phase 05 steps table in one transaction.
public class CashVoucherService : ICashVoucherService
{
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
    private readonly IFundBalanceGuard _fundGuard;
    private readonly ICustomerService _customers;

    public CashVoucherService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock,
        ITransactionRunner transaction, IInventoryLock inventoryLock, ICashDebtLock cashDebtLock,
        IDocumentCodeAllocator codes, IDebtLedgerService ledger, IDebtQueryService debtQuery, IFundBalanceGuard fundGuard,
        ICustomerService customers)
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
        _fundGuard = fundGuard;
        _customers = customers;
    }

    public async Task<CashVoucherListResult> ListAsync(CashVoucherListRequest request, CancellationToken ct = default)
    {
        EnsurePermission(CashVoucherPermissions.View(request.Type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        // IgnoreQueryFilters keeps vouchers whose fund, reason, partner or owner was soft-deleted later (as in
        // LoadDtoAsync); the vouchers' own soft-delete filter is applied by hand.
        var query = _db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => !v.IsDeleted && v.BranchId == branchId && v.Type == request.Type);

        // VN dates as UTC ranges (D27).
        if (request.From is { } from)
        {
            var start = VnTime.StartOfDay(from);
            query = query.Where(v => v.VoucherAt >= start);
        }
        if (request.To is { } to)
        {
            var end = VnTime.StartOfNextDay(to);
            query = query.Where(v => v.VoucherAt < end);
        }
        if (request.FundId is { } fundId)
            query = query.Where(v => v.FundId == fundId);
        if (request.ReasonId is { } reasonId)
            query = query.Where(v => v.ReasonId == reasonId);
        if (request.PartnerId is { } partnerId)
            query = query.Where(v => v.PartnerId == partnerId);
        if (request.Status != DocumentStatusFilter.All)
        {
            var status = (DocumentStatus)request.Status;
            query = query.Where(v => v.Status == status);
        }

        var ownerIds = OwnerIdListParser.Parse(request.OwnerIds);
        if (ownerIds.Count > 0)
            query = query.Where(v => ownerIds.Contains(v.OwnerUserId));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(v => EF.Functions.ILike(v.Code, pattern)
                || EF.Functions.ILike(v.PartnerName!, pattern)
                || EF.Functions.ILike(v.PersonName!, pattern)
                || EF.Functions.ILike(v.Description!, pattern));
        }

        var totalItems = await query.CountAsync(ct);
        var totalAmount = await query.SumAsync(v => v.Total, ct);
        var items = await query
            .OrderByDescending(v => v.VoucherAt)
            .ThenByDescending(v => v.Code)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(v => new CashVoucherListItemDto
            {
                Id = v.Id,
                Type = v.Type,
                Code = v.Code,
                VoucherAt = v.VoucherAt,
                FundCode = v.Fund!.Code,
                FundName = v.Fund.Name,
                ReasonName = v.Reason!.Name,
                PartnerCode = v.Partner != null ? v.Partner.Code : null,
                PartnerName = v.PartnerName,
                PersonName = v.PersonName,
                Description = v.Description,
                Total = v.Total,
                Status = v.Status,
                OwnerUserId = v.OwnerUserId,
                OwnerName = v.Owner != null ? v.Owner.FullName : null,
                SourceStockVoucherId = v.SourceStockVoucherId,
                SourceStockVoucherCode = v.SourceStockVoucher != null ? v.SourceStockVoucher.Code : null,
                IsManaged = v.SourceStockVoucherId != null,
            })
            .ToListAsync(ct);

        return new CashVoucherListResult
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
            TotalAmount = totalAmount,
        };
    }

    /// Form defaults: NextCode is a peek and does not use a number; the branch's default cash fund; TKH / TNCC.
    public async Task<CashVoucherDefaultsDto> GetDefaultsAsync(CashVoucherType type, CancellationToken ct = default)
    {
        EnsurePermission(CashVoucherPermissions.Create(type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        var now = _clock.UtcNow;
        var reasonCode = type == CashVoucherType.Receipt ? "TKH" : "TNCC";

        return new CashVoucherDefaultsDto
        {
            NextCode = await NextCodeAsync(type, branchId, now, peek: true, ct),
            VoucherAt = now,
            FundId = await _db.Funds.AsNoTracking()
                .Where(f => f.BranchId == branchId && f.Kind == FundKind.Cash && f.IsDefault && f.IsActive)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync(ct),
            ReasonId = await _db.CashReasons.AsNoTracking()
                .Where(r => r.Code == reasonCode && r.Type == type && !r.IsAuto)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct),
        };
    }

    /// Partner picker. With a reason (form): its partner type narrowed by the side role of its debt effect (P19),
    /// active partners only. Without one (list filter): any role, inactive partners included.
    public async Task<List<CustomerSearchItemDto>> SearchPartnersAsync(CashVoucherType type, string? keyword, int limit,
        Guid? reasonId, CancellationToken ct = default)
    {
        EnsurePermission(CashVoucherPermissions.View(type));
        var partnerType = PartnerType.Any;
        if (reasonId is { } id)
        {
            var reason = await _db.CashReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (reason is null || reason.Type != type)
                throw new ValidationDomainException(new Dictionary<string, string[]>
                {
                    ["reasonId"] = new[] { reason is null ? "Lý do không tồn tại." : "Lý do không đúng loại phiếu." },
                }, null);
            partnerType = reason.PartnerType;
            if (partnerType == PartnerType.Any && DebtEffectRules.Resolve(reason.DebtEffect) is { } effect)
                partnerType = effect.Side == DebtSide.Receivable ? PartnerType.Customer : PartnerType.Supplier;
        }

        return await _customers.SearchAsync(new CustomerSearchRequest
        {
            Keyword = keyword ?? string.Empty,
            Limit = limit,
            ActiveOnly = reasonId.HasValue,
        }, partnerType, ct);
    }

    public async Task<IReadOnlyList<CashDocumentActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default)
    {
        var voucher = await _db.CashVouchers.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new { v.Type, v.BranchId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(CashVoucher), id);

        EnsurePermission(CashVoucherPermissions.View(voucher.Type));
        await EnsureWorkingBranchAsync(voucher.BranchId, ct);

        var documentType = DocTypeOf(voucher.Type);
        return await _db.CashDocumentActivities.AsNoTracking()
            .Where(a => a.DocumentType == documentType && a.DocumentId == id)
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new CashDocumentActivityDto
            {
                Id = a.Id,
                Action = a.Action,
                ActorUserId = a.ActorUserId,
                ActorName = a.ActorUserId == null
                    ? "Hệ thống"
                    : _db.Users.IgnoreQueryFilters()
                        .Where(u => u.Id == a.ActorUserId)
                        .Select(u => u.FullName)
                        .FirstOrDefault() ?? "Người dùng không xác định",
                OccurredAt = a.OccurredAt,
                Description = a.Description,
            })
            .ToListAsync(ct);
    }

    public Task<CashVoucherDto> GetAsync(Guid id, CancellationToken ct = default) =>
        LoadDtoAsync(id, ct, checkAccess: true);

    public async Task<CashVoucherDto> CreateAsync(UpsertCashVoucherRequest request, CancellationToken ct = default)
    {
        EnsurePermission(CashVoucherPermissions.Create(request.Type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        var id = await _transaction.RunAsync(async c =>
        {
            var partnerIds = request.PartnerId is { } partnerId ? new[] { partnerId } : Array.Empty<Guid>();
            await AcquireLocksAsync(branchId, partnerIds, new[] { request.FundId }, c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil, new[] { voucherAt }.Where(IsValidVoucherAt));
            var (reason, partner) = await ValidateAsync(request, branchId, existing: null, c);
            var fundCapture = await _fundGuard.CaptureAsync(new[] { (request.FundId, voucherAt) }, c);

            var voucher = new CashVoucher
            {
                Type = request.Type,
                Code = await NextCodeAsync(request.Type, branchId, voucherAt, peek: false, c),
                BranchId = branchId,
                OwnerUserId = _currentUser.UserId ?? throw new UnauthorizedAccessException(),
            };
            _db.CashVouchers.Add(voucher);
            ApplyRequest(voucher, request, voucherAt, partner);
            await _db.SaveChangesAsync(c);

            var entries = await _ledger.PostAsync(SourceTypeOf(voucher.Type), voucher.Id, DraftsOf(voucher, reason),
                request.ConfirmRemoveSettlements, c);
            await AllocateAsync(entries, request.Allocations, c);
            await _fundGuard.CheckAsync(fundCapture, request.AcknowledgeNegativeFund, c);
            AddActivity(voucher, CashDocumentActivityAction.Created, "Tạo phiếu");
            await _db.SaveChangesAsync(c);
            return voucher.Id;
        }, ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<CashVoucherDto> UpdateAsync(Guid id, UpsertCashVoucherRequest request, CancellationToken ct = default)
    {
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        await _transaction.RunAsync(async c =>
        {
            var voucher = await LoadForWriteAsync(id, CashVoucherPermissions.Edit, request.Version, DocumentStatus.Active, c);
            await AcquireLocksAsync(voucher.BranchId, new[] { voucher.PartnerId, request.PartnerId }.OfType<Guid>(),
                new[] { voucher.FundId, request.FundId }, c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == voucher.BranchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil,
                new[] { voucher.VoucherAt }.Concat(new[] { voucherAt }.Where(IsValidVoucherAt)));
            var (reason, partner) = await ValidateAsync(request, voucher.BranchId, voucher, c);
            // Both funds from the earlier of the two dates: the old fund loses the voucher, the new one gains it.
            var from = voucher.VoucherAt < voucherAt ? voucher.VoucherAt : voucherAt;
            var fundCapture = await _fundGuard.CaptureAsync(new[] { (voucher.FundId, from), (request.FundId, from) }, c);

            // A list replaces the voucher's own AtVoucher settlements (C8); null keeps them. Every other origin stays
            // and is subject to the shrink and partner rules of PostAsync (P10, P11).
            var sourceType = SourceTypeOf(voucher.Type);
            if (request.Allocations is not null)
                await _ledger.RemoveOwnSettlementsAsync(sourceType, voucher.Id, SettlementOrigin.AtVoucher, c);

            ApplyRequest(voucher, request, voucherAt, partner);
            await _db.SaveChangesAsync(c);

            var entries = await _ledger.PostAsync(sourceType, voucher.Id, DraftsOf(voucher, reason),
                request.ConfirmRemoveSettlements, c);
            await AllocateAsync(entries, request.Allocations, c);
            await _fundGuard.CheckAsync(fundCapture, request.AcknowledgeNegativeFund, c);
            AddActivity(voucher, CashDocumentActivityAction.Updated, "Cập nhật phiếu");
            await _db.SaveChangesAsync(c);
        }, ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<CashVoucherDto> CancelAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default)
    {
        await ChangeLifecycleAsync(id, request, CashVoucherPermissions.Cancel, DocumentStatus.Active,
            CashDocumentActivityAction.Cancelled, "Hủy phiếu", async (v, c) =>
            {
                await _ledger.RemoveSourceAsync(SourceTypeOf(v.Type), v.Id, request.ConfirmRemoveSettlements, c);
                v.Status = DocumentStatus.Cancelled;
                v.CancelledAt = _clock.UtcNow;
                v.CancelledBy = _currentUser.UserId;
            }, ct);
        return await LoadDtoAsync(id, ct);
    }

    /// Uses the cancel permission. The debt entry is posted again; its old settlements do not come back.
    public async Task<CashVoucherDto> RestoreAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default)
    {
        await ChangeLifecycleAsync(id, request, CashVoucherPermissions.Cancel, DocumentStatus.Cancelled,
            CashDocumentActivityAction.Restored, "Khôi phục phiếu", async (v, c) =>
            {
                var reason = await EnsureReferencesExistAsync(v, c);
                v.Status = DocumentStatus.Active;
                v.CancelledAt = null;
                v.CancelledBy = null;
                await _db.SaveChangesAsync(c);
                await _ledger.PostAsync(SourceTypeOf(v.Type), v.Id, DraftsOf(v, reason), request.ConfirmRemoveSettlements, c);
            }, ct);
        return await LoadDtoAsync(id, ct);
    }

    // Soft delete in any status; AppDbContext cascades IsDeleted to the lines.
    public Task DeleteAsync(Guid id, CashDocumentActionRequest request, CancellationToken ct = default) =>
        ChangeLifecycleAsync(id, request, CashVoucherPermissions.Delete, requiredStatus: null,
            CashDocumentActivityAction.Deleted, "Xóa phiếu", async (v, c) =>
            {
                // Only an Active voucher has a debt entry; removing its settlements needs confirmation.
                if (v.Status == DocumentStatus.Active)
                    await _ledger.RemoveSourceAsync(SourceTypeOf(v.Type), v.Id, request.ConfirmRemoveSettlements, c);
                v.IsDeleted = true;
            }, ct);

    /// Cancel / restore / delete per the steps table: locks, period lock on the stored date, fund capture, the
    /// operation's own steps (`apply`), save, the negative-fund check on the voucher's fund, then the activity.
    private Task ChangeLifecycleAsync(Guid id, CashDocumentActionRequest request, Func<CashVoucherType, string> permission,
        DocumentStatus? requiredStatus, CashDocumentActivityAction action, string description,
        Func<CashVoucher, CancellationToken, Task> apply, CancellationToken ct) =>
        _transaction.RunAsync(async c =>
        {
            var voucher = await LoadForWriteAsync(id, permission, request.Version, requiredStatus, c);
            await AcquireLocksAsync(voucher.BranchId, new[] { voucher.PartnerId }.OfType<Guid>(), new[] { voucher.FundId }, c);

            var lockedUntil = await _db.Branches.Where(b => b.Id == voucher.BranchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil, new[] { voucher.VoucherAt });
            var fundCapture = await _fundGuard.CaptureAsync(new[] { (voucher.FundId, voucher.VoucherAt) }, c);

            await apply(voucher, c);
            await _db.SaveChangesAsync(c);
            await _fundGuard.CheckAsync(fundCapture, request.AcknowledgeNegativeFund, c);
            AddActivity(voucher, action, description);
            await _db.SaveChangesAsync(c);
        }, ct);

    /// Restore: the reason, fund and partner must still exist (not soft-deleted); 400 with their keys.
    private async Task<CashReason> EnsureReferencesExistAsync(CashVoucher voucher, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var reason = await _db.CashReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == voucher.ReasonId, ct);
        if (reason is null)
            errors["reasonId"] = new[] { "Lý do đã bị xóa." };
        if (!await _db.Funds.AnyAsync(f => f.Id == voucher.FundId, ct))
            errors["fundId"] = new[] { "Quỹ đã bị xóa." };
        if (voucher.PartnerId is { } partnerId && !await _db.Customers.AnyAsync(p => p.Id == partnerId, ct))
            errors["partnerId"] = new[] { "Đối tượng đã bị xóa." };

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
        return reason!;
    }

    /// Update, cancel and delete: permission, working branch, managed check, ownership, status and version.
    /// `requiredStatus` null accepts any status. The header is always marked modified so the xmin check runs even
    /// when only lines change (D29).
    private async Task<CashVoucher> LoadForWriteAsync(Guid id, Func<CashVoucherType, string> permission, uint? version,
        DocumentStatus? requiredStatus, CancellationToken ct)
    {
        var voucher = await _db.CashVouchers.Include(v => v.Lines).FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(CashVoucher), id);

        EnsurePermission(permission(voucher.Type));
        await EnsureWorkingBranchAsync(voucher.BranchId, ct);
        // Automatic vouchers change only through their stock voucher (P20).
        if (voucher.SourceStockVoucherId is not null)
            throw new ConflictException("MANAGED_BY_STOCK_VOUCHER", "Phiếu được tạo tự động theo phiếu kho, hãy thao tác trên phiếu kho.");
        if (voucher.OwnerUserId != _currentUser.UserId
            && !_currentUser.HasPermission(CashVoucherPermissions.EditAll(voucher.Type)))
            throw new ForbiddenException("Bạn chỉ được thao tác trên phiếu do mình tạo.");
        if (requiredStatus is { } required && voucher.Status != required)
            throw new ConflictException(required == DocumentStatus.Active
                ? "Phiếu đã hủy, không thể thay đổi."
                : "Phiếu chưa bị hủy.");
        if (version is null)
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["version"] = new[] { "Thiếu phiên bản dữ liệu của phiếu." } }, null);

        _db.Entry(voucher).Property(v => v.Version).OriginalValue = version.Value;
        voucher.UpdatedAt = _clock.UtcNow;
        voucher.UpdatedBy = _currentUser.UserId;
        return voucher;
    }

    /// Write operations return the saved voucher with the permission they were authorized by, not view, so a
    /// create-only user never gets 403 for a voucher that was saved (Round 1 review finding).
    private async Task<CashVoucherDto> LoadDtoAsync(Guid id, CancellationToken ct, bool checkAccess = false)
    {
        // IgnoreQueryFilters keeps the voucher visible when its fund, reason or partner was soft-deleted later;
        // the voucher's own and its lines' soft-delete filters are applied by hand.
        var row = await _db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.Id == id && !v.IsDeleted)
            .Select(v => new
            {
                Voucher = v,
                FundCode = v.Fund!.Code,
                FundName = v.Fund.Name,
                FundKind = v.Fund.Kind,
                ReasonCode = v.Reason!.Code,
                ReasonName = v.Reason.Name,
                ReasonDebtEffect = v.Reason.DebtEffect,
                PartnerCode = v.Partner != null ? v.Partner.Code : null,
                SourceStockVoucherCode = v.SourceStockVoucher != null ? v.SourceStockVoucher.Code : null,
                SourceStockVoucherType = v.SourceStockVoucher != null ? (StockDirection?)v.SourceStockVoucher.Type : null,
                OwnerName = v.Owner != null ? v.Owner.FullName : null,
                Lines = v.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).ToList(),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(CashVoucher), id);
        var voucher = row.Voucher;

        if (checkAccess)
        {
            EnsurePermission(CashVoucherPermissions.View(voucher.Type));
            await EnsureWorkingBranchAsync(voucher.BranchId, ct);
        }

        // Every write is rejected inside a locked period, and a managed voucher only changes through its stock voucher.
        var lockedUntil = await _db.Branches.Where(b => b.Id == voucher.BranchId).Select(b => b.LockedUntil).FirstOrDefaultAsync(ct);
        var isManaged = voucher.SourceStockVoucherId is not null;
        var mayAct = !isManaged
            && !(lockedUntil is { } locked && VnTime.ToVnDate(voucher.VoucherAt) <= locked)
            && (voucher.OwnerUserId == _currentUser.UserId
                || _currentUser.HasPermission(CashVoucherPermissions.EditAll(voucher.Type)));
        var active = voucher.Status == DocumentStatus.Active;

        return new CashVoucherDto
        {
            Id = voucher.Id,
            Type = voucher.Type,
            Code = voucher.Code,
            VoucherAt = voucher.VoucherAt,
            BranchId = voucher.BranchId,
            FundId = voucher.FundId,
            FundCode = row.FundCode,
            FundName = row.FundName,
            FundKind = row.FundKind,
            ReasonId = voucher.ReasonId,
            ReasonCode = row.ReasonCode,
            ReasonName = row.ReasonName,
            ReasonDebtEffect = row.ReasonDebtEffect,
            PartnerId = voucher.PartnerId,
            PartnerCode = row.PartnerCode,
            PartnerName = voucher.PartnerName,
            PartnerAddress = voucher.PartnerAddress,
            PartnerTaxCode = voucher.PartnerTaxCode,
            PersonName = voucher.PersonName,
            Description = voucher.Description,
            AttachedDocs = voucher.AttachedDocs,
            Note = voucher.Note,
            Total = voucher.Total,
            Status = voucher.Status,
            CancelledAt = voucher.CancelledAt,
            OwnerUserId = voucher.OwnerUserId,
            OwnerName = row.OwnerName,
            Version = voucher.Version,
            SourceStockVoucherId = voucher.SourceStockVoucherId,
            SourceStockVoucherCode = row.SourceStockVoucherCode,
            SourceStockVoucherType = row.SourceStockVoucherType,
            IsManaged = isManaged,
            CanEdit = mayAct && active && _currentUser.HasPermission(CashVoucherPermissions.Edit(voucher.Type)),
            CanDelete = mayAct && _currentUser.HasPermission(CashVoucherPermissions.Delete(voucher.Type)),
            // Also true for a Cancelled voucher: restore uses the same permission.
            CanCancel = mayAct && _currentUser.HasPermission(CashVoucherPermissions.Cancel(voucher.Type)),
            Lines = row.Lines.Select(l => new CashVoucherLineDto
            {
                Id = l.Id,
                SortOrder = l.SortOrder,
                Description = l.Description,
                Amount = l.Amount,
                Note = l.Note,
            }).ToList(),
            Allocations = await AllocationsOfAsync(voucher, ct),
        };
    }

    /// Every settlement of the voucher's own entry (any origin), with the other document's code and date.
    private async Task<List<CashVoucherAllocationDto>> AllocationsOfAsync(CashVoucher voucher, CancellationToken ct)
    {
        var sourceType = SourceTypeOf(voucher.Type);
        var ownIds = await _db.DebtEntries.AsNoTracking()
            .Where(e => e.SourceType == sourceType && e.SourceId == voucher.Id)
            .Select(e => e.Id)
            .ToListAsync(ct);
        if (ownIds.Count == 0)
            return new List<CashVoucherAllocationDto>();

        var settlements = await _db.DebtSettlements.AsNoTracking()
            .Where(s => ownIds.Contains(s.IncreaseEntryId) || ownIds.Contains(s.DecreaseEntryId))
            .Select(s => new { s.Id, s.Amount, s.Origin, OtherId = ownIds.Contains(s.IncreaseEntryId) ? s.DecreaseEntryId : s.IncreaseEntryId })
            .ToListAsync(ct);
        var otherIds = settlements.Select(s => s.OtherId).Distinct().ToList();
        var others = await _db.DebtEntries.AsNoTracking()
            .Where(e => otherIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        return settlements
            .Select(s => (Settlement: s, Entry: others[s.OtherId]))
            .OrderBy(x => x.Entry.DocDate)
            .ThenBy(x => x.Entry.DocCode, StringComparer.Ordinal)
            .Select(x => new CashVoucherAllocationDto
            {
                SettlementId = x.Settlement.Id,
                EntryId = x.Entry.Id,
                SourceType = x.Entry.SourceType,
                SourceId = x.Entry.SourceId,
                DocCode = x.Entry.DocCode,
                DocDate = x.Entry.DocDate,
                Amount = x.Settlement.Amount,
                Origin = x.Settlement.Origin,
            })
            .ToList();
    }

    // ---- Writes -----------------------------------------------------------------------------

    /// The shared branch gate, then the partner and fund keys (P14).
    private async Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> partnerIds, IEnumerable<Guid> fundIds,
        CancellationToken ct)
    {
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        await _cashDebtLock.AcquireAsync(branchId, partnerIds.Distinct(), fundIds.Distinct(), ct);
    }

    /// Business rules 1–3 and 5 (Phase 05 save rules), the voucher date range and the Total limit, collected into
    /// one 400. `existing` is null on create. Runs after the locks, so the references it reads cannot change before
    /// the save.
    private async Task<(CashReason Reason, Customer? Partner)> ValidateAsync(UpsertCashVoucherRequest request,
        Guid branchId, CashVoucher? existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors.TryAdd(key, new[] { message });

        if (existing is not null && existing.Type != request.Type)
            Add("type", "Không được đổi loại phiếu.");

        if (!IsValidVoucherAt(VnTime.ToUtc(request.VoucherAt)))
            Add("voucherAt", "Ngày chứng từ không hợp lệ.");

        // Each line fits numeric(18,2) (shape validator); their sum is the voucher Total and the debt amount.
        if (request.Lines.Sum(l => l.Amount) >= MaxMoney)
            Add("lines", "Tổng tiền phiếu quá lớn.");

        // A line Id sent twice would update one stored line twice (same message as stock vouchers).
        for (var i = 0; i < request.Lines.Count; i++)
        {
            if (request.Lines[i].Id is { } repeatedId && request.Lines.Take(i).Any(l => l.Id == repeatedId))
                Add($"lines[{i}].id", "Dòng hàng bị trùng.");
        }

        var reason = await _db.CashReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReasonId, ct);
        if (reason is null)
            Add("reasonId", "Lý do không tồn tại.");
        else if (reason.Type != request.Type)
            Add("reasonId", "Lý do không đúng loại phiếu.");
        else if (reason.IsAuto)
            Add("reasonId", "Lý do này chỉ dùng cho phiếu tự động.");

        var fund = await _db.Funds.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FundId, ct);
        if (fund is null || fund.BranchId != branchId)
            Add("fundId", "Quỹ không thuộc chi nhánh đang làm việc.");
        // Only a newly chosen fund must be active, so old vouchers stay editable.
        else if (!fund.IsActive && existing?.FundId != request.FundId)
            Add("fundId", "Quỹ đã ngừng sử dụng.");

        Customer? partner = null;
        if (request.PartnerId is { } partnerId)
        {
            partner = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partnerId, ct);
            if (partner is null)
                Add("partnerId", "Đối tượng không tồn tại.");
            else if (partner.Status != CustomerStatus.Active && existing?.PartnerId != request.PartnerId)
                Add("partnerId", "Đối tượng đã ngừng hoạt động.");
        }
        if (reason is not null)
            ValidatePartner(reason, request.PartnerId, partner, Add);

        // Rule 4: an empty list is always accepted (on update it clears the AtVoucher settlements).
        if (reason is not null && request.Allocations is { Count: > 0 } && reason.DebtEffect == DebtEffect.None)
            Add("allocations", "Lý do này không ảnh hưởng công nợ.");

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
        return (reason!, partner);
    }

    private static readonly DateTimeOffset MinVoucherAt = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Exclusive upper bound of numeric(18,2).
    private const decimal MaxMoney = 10_000_000_000_000_000m;

    /// Same range as stock vouchers. An out-of-range date is reported by the rules (400 `voucherAt`), so the period
    /// lock check skips it: VnTime cannot convert dates near DateTimeOffset.MaxValue.
    private bool IsValidVoucherAt(DateTimeOffset voucherAt) =>
        voucherAt >= MinVoucherAt && voucherAt <= _clock.UtcNow.AddYears(1);

    /// Partner rules per reason: PartnerType (Round 1 D4), then the debt side's role when the reason affects debt (P19).
    private static void ValidatePartner(CashReason reason, Guid? partnerId, Customer? partner, Action<string, string> add)
    {
        switch (reason.PartnerType)
        {
            case PartnerType.None when partnerId.HasValue:
                add("partnerId", "Lý do này không dùng đối tượng.");
                return;
            case PartnerType.Customer when partnerId is null:
                add("partnerId", "Lý do này yêu cầu chọn khách hàng.");
                return;
            case PartnerType.Customer when partner is { IsCustomer: false }:
                add("partnerId", "Đối tượng không phải khách hàng.");
                return;
            case PartnerType.Supplier when partnerId is null:
                add("partnerId", "Lý do này yêu cầu chọn nhà cung cấp.");
                return;
            case PartnerType.Supplier when partner is { IsSupplier: false }:
                add("partnerId", "Đối tượng không phải nhà cung cấp.");
                return;
        }

        if (DebtEffectRules.Resolve(reason.DebtEffect) is not { } effect)
            return;
        if (partnerId is null)
            add("partnerId", "Lý do này ảnh hưởng công nợ, vui lòng chọn đối tượng.");
        else if (partner is not null && !DebtEffectRules.PartnerHasSideRole(effect.Side, partner.IsCustomer, partner.IsSupplier))
            add("partnerId", effect.Side == DebtSide.Receivable
                ? "Đối tượng không phải khách hàng."
                : "Đối tượng không phải nhà cung cấp.");
    }

    /// `peek` reads the next number without using it (form defaults); otherwise the counter is incremented.
    /// Codes already used in the (type, branch) are skipped (see DocumentCodeAllocator).
    private Task<string> NextCodeAsync(CashVoucherType type, Guid branchId, DateTimeOffset voucherAt, bool peek,
        CancellationToken ct) =>
        _codes.NextAsync(DocTypeOf(type), branchId, voucherAt, peek, async (codes, c) =>
            // Same scope as the unique index (type, branch_id, code) WHERE is_deleted = false: the query filter matches it.
            (await _db.CashVouchers
                .Where(v => v.Type == type && v.BranchId == branchId && codes.Contains(v.Code))
                .Select(v => v.Code)
                .ToListAsync(c)).ToHashSet(), ct);

    /// Header fields, partner snapshot, lines and Total = Σ lines (never from the client).
    private void ApplyRequest(CashVoucher voucher, UpsertCashVoucherRequest request, DateTimeOffset voucherAt,
        Customer? partner)
    {
        voucher.VoucherAt = voucherAt;
        voucher.FundId = request.FundId;
        voucher.ReasonId = request.ReasonId;
        voucher.PartnerId = request.PartnerId;
        voucher.PartnerName = request.PartnerName ?? partner?.Name;
        voucher.PartnerAddress = request.PartnerAddress ?? partner?.CompanyAddress;
        voucher.PartnerTaxCode = request.PartnerTaxCode ?? partner?.TaxCode;
        voucher.PersonName = request.PersonName;
        voucher.Description = request.Description;
        voucher.AttachedDocs = request.AttachedDocs;
        voucher.Note = request.Note;

        // Upsert by line Id; a stored line left out of the request is soft-deleted.
        var existing = voucher.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        var kept = new HashSet<Guid>();
        var current = new HashSet<CashVoucherLine>();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if (line.Id is { } lineId && existing.TryGetValue(lineId, out var entity))
            {
                kept.Add(lineId);
            }
            else
            {
                // New child rows go through their DbSet.
                entity = new CashVoucherLine { CashVoucherId = voucher.Id };
                _db.CashVoucherLines.Add(entity);
            }

            entity.SortOrder = i;
            entity.Description = line.Description;
            entity.Amount = line.Amount;
            entity.Note = line.Note;
            current.Add(entity);
        }

        foreach (var removed in existing.Values.Where(l => !kept.Contains(l.Id)))
        {
            removed.IsDeleted = true;
            removed.DeletedAt = _clock.UtcNow;
            removed.DeletedBy = _currentUser.UserId;
        }

        // From the line entities (each counted once), so Total always equals the stored lines.
        voucher.Total = current.Sum(l => l.Amount);
    }

    /// One entry when the reason affects debt (save rules, "Debt draft for a manual voucher"); none otherwise.
    private static IReadOnlyList<DebtEntryDraft> DraftsOf(CashVoucher voucher, CashReason reason)
    {
        if (DebtEffectRules.Resolve(reason.DebtEffect) is not { } effect)
            return Array.Empty<DebtEntryDraft>();

        var partnerId = voucher.PartnerId
            ?? throw new InvalidOperationException("A cash voucher whose reason affects debt needs a partner.");
        var docDate = VnTime.ToVnDate(voucher.VoucherAt);
        return new[]
        {
            new DebtEntryDraft(voucher.BranchId, partnerId, effect.Side, effect.Direction, voucher.Total,
                voucher.VoucherAt, docDate, DebtDates.EntryDueDate(effect.Direction, docDate, null),
                voucher.OwnerUserId, voucher.Code, voucher.Description ?? reason.Name),
        };
    }

    /// Allocation at voucher time: validates the allocations of the voucher's own entry against its open items
    /// (P26: this voucher's own settlements count as open room) and saves them as one AtVoucher batch.
    /// Capacity = own entry Amount − its settlements still kept.
    private async Task AllocateAsync(IReadOnlyList<DebtEntry> ownEntries, IReadOnlyList<AllocationRequest>? allocations,
        CancellationToken ct)
    {
        if (allocations is not { Count: > 0 })
            return;

        // Rule 4 guarantees a reason with debt effect, hence exactly one entry.
        var own = ownEntries.Single();
        var capacity = own.Amount - (await _ledger.SettledAmountsAsync(new[] { own.Id }, ct: ct))[own.Id];
        var openItems = await _debtQuery.OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = own.BranchId,
            PartnerId = own.PartnerId,
            Side = own.Side,
            ForSourceType = own.SourceType,
            ForSourceId = own.SourceId,
        }, ct);
        var open = openItems.ToDictionary(i => i.EntryId,
            i => new OpenEntry(i.EntryId, own.BranchId, own.PartnerId, own.Side, i.Direction, i.Open, i.PostedAt));

        var errors = AllocationValidator.Validate(own.BranchId, own.PartnerId, own.Side, own.Direction, capacity,
            allocations.Select(a => new AllocationInput(a.EntryId, a.Amount)).ToList(), open);
        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);

        // The own entry fills the slot of its direction; the allocated document takes the other one.
        var drafts = allocations
            .Select(a => own.Direction == DebtDirection.Increase
                ? new SettlementDraft(own.Id, a.EntryId, a.Amount)
                : new SettlementDraft(a.EntryId, own.Id, a.Amount))
            .ToList();
        await _ledger.AddSettlementsAsync(drafts, SettlementOrigin.AtVoucher, Guid.NewGuid(), "allocations", ct);
    }

    private void AddActivity(CashVoucher voucher, CashDocumentActivityAction action, string description) =>
        _db.CashDocumentActivities.Add(new CashDocumentActivity
        {
            DocumentType = DocTypeOf(voucher.Type),
            DocumentId = voucher.Id,
            Action = action,
            ActorUserId = _currentUser.UserId,
            OccurredAt = _clock.UtcNow,
            Description = description,
        });

    // ---- Guards and mapping -----------------------------------------------------------------

    private void EnsurePermission(string permission)
    {
        if (!_currentUser.HasPermission(permission))
            throw new ForbiddenException();
    }

    private async Task EnsureWorkingBranchAsync(Guid branchId, CancellationToken ct)
    {
        if (branchId != await _currentBranch.GetIdAsync(ct))
            throw new ForbiddenException("Phiếu thuộc chi nhánh khác.");
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static DocumentType DocTypeOf(CashVoucherType type) =>
        type == CashVoucherType.Receipt ? DocumentType.Receipt : DocumentType.Payment;

    private static DebtSourceType SourceTypeOf(CashVoucherType type) =>
        type == CashVoucherType.Receipt ? DebtSourceType.Receipt : DebtSourceType.Payment;
}
