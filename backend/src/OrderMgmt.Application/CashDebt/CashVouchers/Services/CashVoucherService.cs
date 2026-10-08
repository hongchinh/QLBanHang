using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Interfaces;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Numbering;
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

    public CashVoucherService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock,
        ITransactionRunner transaction, IInventoryLock inventoryLock, ICashDebtLock cashDebtLock,
        IDocumentCodeAllocator codes, IDebtLedgerService ledger)
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

            var reason = await _db.CashReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReasonId, c)
                ?? throw new NotFoundException(nameof(CashReason), request.ReasonId);
            var partner = request.PartnerId is { } requestedPartnerId
                ? await _db.Customers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == requestedPartnerId, c)
                    ?? throw new NotFoundException(nameof(Customer), requestedPartnerId)
                : null;

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

            await _ledger.PostAsync(SourceTypeOf(voucher.Type), voucher.Id, DraftsOf(voucher, reason),
                request.ConfirmRemoveSettlements, c);
            AddActivity(voucher, CashDocumentActivityAction.Created, "Tạo phiếu");
            await _db.SaveChangesAsync(c);
            return voucher.Id;
        }, ct);

        return await LoadDtoAsync(id, ct);
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
        };
    }

    // ---- Writes -----------------------------------------------------------------------------

    /// The shared branch gate, then the partner and fund keys (P14).
    private async Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> partnerIds, IEnumerable<Guid> fundIds,
        CancellationToken ct)
    {
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        await _cashDebtLock.AcquireAsync(branchId, partnerIds.Distinct(), fundIds.Distinct(), ct);
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

    /// Header fields, partner snapshot, lines (through their DbSet) and Total = Σ lines (never from the client).
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

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            _db.CashVoucherLines.Add(new CashVoucherLine
            {
                CashVoucherId = voucher.Id,
                SortOrder = i,
                Description = line.Description,
                Amount = line.Amount,
                Note = line.Note,
            });
        }
        voucher.Total = request.Lines.Sum(l => l.Amount);
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

    private static DocumentType DocTypeOf(CashVoucherType type) =>
        type == CashVoucherType.Receipt ? DocumentType.Receipt : DocumentType.Payment;

    private static DebtSourceType SourceTypeOf(CashVoucherType type) =>
        type == CashVoucherType.Receipt ? DebtSourceType.Receipt : DebtSourceType.Payment;
}
