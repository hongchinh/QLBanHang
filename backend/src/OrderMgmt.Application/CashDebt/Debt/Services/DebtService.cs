using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.AutoVouchers;
using OrderMgmt.Application.CashDebt.Debt.Interfaces;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Organization.Branches;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Debt.Services;

public class DebtService : IDebtService
{
    private readonly IAppDbContext _db;
    private readonly IDebtQueryService _debtQuery;
    private readonly IDebtLedgerService _ledger;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;
    private readonly ICashDebtLock _cashDebtLock;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;

    public DebtService(IAppDbContext db, IDebtQueryService debtQuery, IDebtLedgerService ledger,
        ITransactionRunner transaction, IInventoryLock inventoryLock,
        ICashDebtLock cashDebtLock, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch)
    {
        _db = db;
        _debtQuery = debtQuery;
        _ledger = ledger;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
        _cashDebtLock = cashDebtLock;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
    }

    public async Task<PartnerDebtSummaryDto> GetPartnerSummaryAsync(Guid partnerId, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(Permissions.Debt.PartnerSummary)
            && !_currentUser.HasPermission(Permissions.Reports.Debt))
            throw new ForbiddenException();

        var branchId = await _currentBranch.GetIdAsync(ct);
        var partner = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == partnerId)
            .Select(c => new { c.CreditDays })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), partnerId);

        var balance = await _debtQuery.BalanceAsync(branchId, partnerId, DebtSide.Receivable, null, ct);
        var limit = await _db.CreditLimits.AsNoTracking()
            .Where(l => l.BranchId == branchId && l.CustomerId == partnerId)
            .Select(l => (decimal?)l.Amount)
            .SingleOrDefaultAsync(ct);

        var today = VnTime.ToVnDate(_clock.UtcNow);
        var openIncreases = await _debtQuery.OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = branchId,
            PartnerId = partnerId,
            Side = DebtSide.Receivable,
            Direction = DebtDirection.Increase,
        }, ct);
        var overdue = openIncreases.Where(i => i.DueDate < today).Sum(i => i.Open);

        return new PartnerDebtSummaryDto
        {
            PartnerId = partnerId,
            ReceivableBalance = balance,
            CreditLimit = limit,
            Available = limit - balance,
            OverdueAmount = overdue,
            CreditDays = partner.CreditDays,
        };
    }

    public async Task<IReadOnlyList<OpenItemDto>> GetOpenItemsAsync(OpenItemsRequest request,
        CancellationToken ct = default)
    {
        EnsureAnyPermission(Permissions.Receipts.Create, Permissions.Receipts.Edit, Permissions.Payments.Create,
            Permissions.Payments.Edit, Permissions.Debt.Matching, Permissions.DebtOffsets.Create);

        var branchId = await _currentBranch.GetIdAsync(ct);
        return await _debtQuery.OpenItemsAsync(new OpenItemsQuery
        {
            BranchId = branchId,
            PartnerId = request.PartnerId,
            Side = request.Side,
            Direction = request.Direction,
            ForSourceType = request.ForSourceType,
            ForSourceId = request.ForSourceId,
        }, ct);
    }

    public async Task<List<CustomerSearchItemDto>> SearchPartnersAsync(DebtPartnerSearchRequest request,
        CancellationToken ct = default)
    {
        EnsureAnyPermission(Permissions.Debt.Matching, Permissions.Debt.OpeningBalance, Permissions.DebtOffsets.View,
            Permissions.Reports.Debt, Permissions.CreditLimits.Manage);

        var query = _db.Customers.AsNoTracking().Where(c => !c.IsDeleted);
        if (request.BothRoles)
            query = query.Where(c => c.IsCustomer && c.IsSupplier);
        else if (request.Side == DebtSide.Receivable)
            query = query.Where(c => c.IsCustomer);
        else if (request.Side == DebtSide.Payable)
            query = query.Where(c => c.IsSupplier);

        if (request.ActiveOnly)
            query = query.Where(c => c.Status == CustomerStatus.Active);

        var keyword = request.Keyword?.Trim() ?? string.Empty;
        if (keyword.Length > 0)
        {
            var pattern = $"%{EscapeLike(keyword)}%";
            query = query.Where(c =>
                EF.Functions.ILike(EF.Functions.Unaccent(c.Code), EF.Functions.Unaccent(pattern), LikeEscape)
                || EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern), LikeEscape)
                || (c.TaxCode != null
                    && EF.Functions.ILike(EF.Functions.Unaccent(c.TaxCode), EF.Functions.Unaccent(pattern), LikeEscape)));
        }

        return await query
            .OrderBy(c => c.Code)
            .Take(Math.Clamp(request.Limit, 1, 50))
            .Select(c => new CustomerSearchItemDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                TaxCode = c.TaxCode,
                CompanyAddress = c.CompanyAddress,
                DefaultShippingAddress = c.DefaultShippingAddress,
                ContactPerson = c.ContactPerson,
                PhoneNumber = c.PhoneNumber,
                Status = c.Status,
                IsCustomer = c.IsCustomer,
                IsSupplier = c.IsSupplier,
                CreditDays = c.CreditDays,
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SettlementDto>> ListSettlementsAsync(SettlementListRequest request,
        CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        // Both entries of a settlement share branch, partner and side, so filtering the Increase entry is enough.
        var entryIds = _db.DebtEntries
            .Where(e => e.BranchId == branchId && e.PartnerId == request.PartnerId && e.Side == request.Side)
            .Select(e => e.Id);
        return await LoadSettlementsAsync(_db.DebtSettlements.Where(s => entryIds.Contains(s.IncreaseEntryId)), ct);
    }

    public async Task<SettlementBatchDto> CreateSettlementBatchAsync(CreateSettlementBatchRequest request,
        CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var batchId = Guid.NewGuid();
        await _transaction.RunAsync(async c =>
        {
            await AcquireLocksAsync(branchId, new[] { request.PartnerId }, c);
            await EnsureEntriesOfPartnerAsync(request, branchId, c);
            var drafts = request.Items
                .Select(i => new SettlementDraft(i.IncreaseEntryId, i.DecreaseEntryId, i.Amount))
                .ToList();
            // The ledger checks the pairs, the open amounts (Auto included) and the period lock on EffectiveAt.
            await _ledger.AddSettlementsAsync(drafts, SettlementOrigin.Manual, batchId, "items", c);
            await TouchCashVouchersAsync(drafts.SelectMany(d => new[] { d.IncreaseEntryId, d.DecreaseEntryId }), c);
        }, ct);

        var settlements = await LoadSettlementsAsync(_db.DebtSettlements.Where(s => s.BatchId == batchId), ct);
        return new SettlementBatchDto { BatchId = batchId, Settlements = settlements.ToList() };
    }

    public Task DeleteSettlementAsync(Guid id, CancellationToken ct = default) =>
        RemoveSettlementsAsync(s => s.Id == id, () => new NotFoundException(nameof(DebtSettlement), id), ct);

    public Task DeleteSettlementBatchAsync(Guid batchId, CancellationToken ct = default) =>
        RemoveSettlementsAsync(s => s.BatchId == batchId, () => new NotFoundException("DebtSettlementBatch", batchId), ct);

    /// Unmatching (P22): the settlements matched by `filter` are removed together, or none is.
    private async Task RemoveSettlementsAsync(Expression<Func<DebtSettlement, bool>> filter, Func<Exception> notFound,
        CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        await _transaction.RunAsync(async c =>
        {
            // The partners to lock. A settled entry cannot change partner (P11), but an AtVoucher batch id is its
            // cash voucher's id: a save of that voucher between this read and the locks can move the batch to another
            // partner. The partners are read again after the locks, and a change is a 409.
            var owners = await OwnersAsync(filter, c);
            if (owners.Count == 0)
                throw notFound();
            if (owners.Any(o => o.BranchId != branchId))
                throw new ForbiddenException("Đối trừ thuộc chi nhánh khác.");
            await AcquireLocksAsync(branchId, owners.Select(o => o.PartnerId), c);

            var settlements = await _db.DebtSettlements.AsNoTracking().Where(filter).ToListAsync(c);
            if (settlements.Count == 0)
                throw notFound();
            if (!(await OwnersAsync(filter, c)).ToHashSet().IsSubsetOf(owners))
                throw new ConflictException("CONCURRENCY", "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại.");
            if (settlements.Any(s => s.Origin is not (SettlementOrigin.Manual or SettlementOrigin.AtVoucher)))
                throw new ConflictException("SETTLEMENT_MANAGED", "Đối trừ này do chứng từ quản lý, không bỏ được ở đây.");
            var lockedUntil = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.LockedUntil).SingleAsync(c);
            PeriodLockGuard.EnsureNotLocked(lockedUntil, settlements.Select(s => s.EffectiveAt));

            // Logs SettlementRemoved on both documents. The freed room goes back to the Auto settlement of any stock
            // voucher whose entry (or automatic voucher's entry) was matched here, before the commit
            // (AutoRecomputeHook; Phase 07 rule, 2026-10-09).
            var removed = await _ledger.RemoveSettlementsAsync(settlements.Select(s => s.Id).ToList(), c);
            var entryIds = removed.SelectMany(r => new[] { r.IncreaseEntryId, r.DecreaseEntryId }).ToList();
            await TouchCashVouchersAsync(entryIds, c);
        }, ct);
    }

    /// A cash voucher form resends its own AtVoucher allocations, so matching or unmatching on its entry changes the
    /// voucher's version (D29): a form opened before gets 409 CONCURRENCY instead of silently undoing the change.
    private async Task TouchCashVouchersAsync(IEnumerable<Guid> entryIds, CancellationToken ct)
    {
        var ids = entryIds.Distinct().ToList();
        var voucherIds = await _db.DebtEntries
            .Where(e => ids.Contains(e.Id) && (e.SourceType == DebtSourceType.Receipt || e.SourceType == DebtSourceType.Payment))
            .Select(e => e.SourceId)
            .ToListAsync(ct);
        if (voucherIds.Count == 0)
            return;

        var now = _clock.UtcNow;
        foreach (var voucher in await _db.CashVouchers.Where(v => voucherIds.Contains(v.Id)).ToListAsync(ct))
        {
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = _currentUser.UserId;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// The branch and partner of every settlement matched by `filter` (read through its Increase entry).
    private async Task<List<(Guid BranchId, Guid PartnerId)>> OwnersAsync(
        Expression<Func<DebtSettlement, bool>> filter, CancellationToken ct) =>
        (await (from s in _db.DebtSettlements.Where(filter)
                join e in _db.DebtEntries on s.IncreaseEntryId equals e.Id
                select new { e.BranchId, e.PartnerId })
            .Distinct()
            .ToListAsync(ct))
        .Select(o => (o.BranchId, o.PartnerId))
        .ToList();

    /// The shared branch gate, then the partner keys (P14). Settlements move no money: no fund key.
    private async Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> partnerIds, CancellationToken ct)
    {
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        await _cashDebtLock.AcquireAsync(branchId, partnerIds.Distinct(), Array.Empty<Guid>(), ct);
    }

    /// Every item's two entries belong to the request's partner and side and to the working branch (400 per slot).
    private async Task EnsureEntriesOfPartnerAsync(CreateSettlementBatchRequest request, Guid branchId,
        CancellationToken ct)
    {
        var ids = request.Items.SelectMany(i => new[] { i.IncreaseEntryId, i.DecreaseEntryId }).Distinct().ToList();
        var owned = (await _db.DebtEntries.AsNoTracking()
                .Where(e => ids.Contains(e.Id) && e.BranchId == branchId && e.PartnerId == request.PartnerId
                            && e.Side == request.Side)
                .Select(e => e.Id)
                .ToListAsync(ct))
            .ToHashSet();

        const string message = "Chứng từ không thuộc đối tượng, loại công nợ hoặc chi nhánh đang làm việc.";
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < request.Items.Count; i++)
        {
            if (!owned.Contains(request.Items[i].IncreaseEntryId))
                errors[$"items[{i}].increaseEntryId"] = new[] { message };
            if (!owned.Contains(request.Items[i].DecreaseEntryId))
                errors[$"items[{i}].decreaseEntryId"] = new[] { message };
        }

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    private async Task<IReadOnlyList<SettlementDto>> LoadSettlementsAsync(IQueryable<DebtSettlement> settlements,
        CancellationToken ct) =>
        await (from s in settlements.AsNoTracking()
               join i in _db.DebtEntries.AsNoTracking() on s.IncreaseEntryId equals i.Id
               join d in _db.DebtEntries.AsNoTracking() on s.DecreaseEntryId equals d.Id
               orderby s.EffectiveAt descending, s.CreatedAt descending, i.DocCode
               select new SettlementDto
               {
                   Id = s.Id,
                   BatchId = s.BatchId,
                   Origin = s.Origin,
                   IncreaseEntryId = i.Id,
                   IncreaseDocCode = i.DocCode,
                   IncreaseDocDate = i.DocDate,
                   IncreaseSourceType = i.SourceType,
                   IncreaseSourceId = i.SourceId,
                   DecreaseEntryId = d.Id,
                   DecreaseDocCode = d.DocCode,
                   DecreaseDocDate = d.DocDate,
                   DecreaseSourceType = d.SourceType,
                   DecreaseSourceId = d.SourceId,
                   Amount = s.Amount,
                   EffectiveAt = s.EffectiveAt,
                   CreatedAt = s.CreatedAt,
                   CreatedByName = s.CreatedBy == null
                       ? null
                       : _db.Users.IgnoreQueryFilters()
                           .Where(u => u.Id == s.CreatedBy)
                           .Select(u => u.FullName)
                           .FirstOrDefault(),
               })
            .ToListAsync(ct);

    private void EnsureAnyPermission(params string[] codes)
    {
        if (!codes.Any(_currentUser.HasPermission))
            throw new ForbiddenException();
    }

    // The escape character EscapeLike uses. Every ILike must pass it: without an escape argument Npgsql sends
    // ESCAPE '' (no escape character), so the added backslashes would stay literal.
    private const string LikeEscape = "\\";

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
