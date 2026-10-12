using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.CashDebt.OpeningDebts.Interfaces;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Organization.Branches;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.OpeningDebts.Services;

public class OpeningDebtService : IOpeningDebtService
{
    public const string DefaultDescription = "Nợ đầu kỳ";

    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;
    private readonly ICashDebtLock _cashDebtLock;
    private readonly IDebtLedgerService _ledger;

    public OpeningDebtService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ICurrentBranch currentBranch,
        ITransactionRunner transaction, IInventoryLock inventoryLock, ICashDebtLock cashDebtLock, IDebtLedgerService ledger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
        _cashDebtLock = cashDebtLock;
        _ledger = ledger;
    }

    public async Task<OpeningDebtListResult> ListAsync(OpeningDebtListRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var openingDate = await _db.Branches.AsNoTracking().Where(b => b.Id == branchId)
            .Select(b => b.OpeningDate).SingleAsync(ct);

        var query = _db.OpeningDebts.AsNoTracking().Where(o => o.BranchId == branchId);
        if (request.PartnerId is { } partnerId)
            query = query.Where(o => o.PartnerId == partnerId);
        if (request.Side is { } side)
            query = query.Where(o => o.Side == side);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(o => EF.Functions.ILike(o.DocNo, pattern)
                || EF.Functions.ILike(o.Partner!.Code, pattern)
                || EF.Functions.ILike(o.Partner.Name, pattern));
        }

        var totalItems = await query.CountAsync(ct);
        var items = await Project(query
                .OrderBy(o => o.Partner!.Code)
                .ThenBy(o => o.DocDate)
                .ThenBy(o => o.DocNo)
                .ThenBy(o => o.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize))
            .ToListAsync(ct);
        await FillSettledAsync(items, ct);

        return new OpeningDebtListResult
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
            OpeningDate = openingDate,
        };
    }

    public async Task<OpeningDebtDto> CreateAsync(UpsertOpeningDebtRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var id = await _transaction.RunAsync(async c =>
        {
            await AcquireLocksAsync(branchId, new[] { request.PartnerId }, c);
            var openingDate = await OpeningDateAsync(branchId, c);
            await ValidateAsync(request, openingDate, c);

            var row = new OpeningDebt { BranchId = branchId };
            Apply(row, request);
            _db.OpeningDebts.Add(row);
            await _db.SaveChangesAsync(c);

            await _ledger.PostAsync(DebtSourceType.Opening, row.Id, new[] { Draft(row, openingDate) },
                request.ConfirmRemoveSettlements, c);
            return row.Id;
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task<OpeningDebtDto> UpdateAsync(Guid id, UpsertOpeningDebtRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        await _transaction.RunAsync(async c =>
        {
            var row = await LockRowAsync(branchId, id, request.PartnerId, request.Version, c);
            var openingDate = await OpeningDateAsync(branchId, c);
            await ValidateAsync(request, openingDate, c);

            Apply(row, request);
            await _db.SaveChangesAsync(c);

            // A confirmed shrink drops the row's settlements: a stock voucher among the entries they join gets its Auto
            // settlement recomputed before the commit (AutoRecomputeHook).
            await _ledger.PostAsync(DebtSourceType.Opening, row.Id, new[] { Draft(row, openingDate) },
                request.ConfirmRemoveSettlements, c);
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, uint? version, bool confirmRemoveSettlements, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        await _transaction.RunAsync(async c =>
        {
            var row = await LockRowAsync(branchId, id, newPartnerId: null, version, c);
            await OpeningDateAsync(branchId, c);

            await _ledger.RemoveSourceAsync(DebtSourceType.Opening, row.Id, confirmRemoveSettlements, c);
            row.IsDeleted = true;
            row.DeletedAt = _clock.UtcNow;
            row.DeletedBy = _currentUser.UserId;
            await _db.SaveChangesAsync(c);
        }, ct);
    }

    public async Task RepostBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        await _transaction.RunAsync(async c =>
        {
            var rows = await _db.OpeningDebts.Where(o => o.BranchId == branchId)
                .OrderBy(o => o.Id).ToListAsync(c);
            if (rows.Count == 0)
                return;

            // Branch gate held by the caller, then the partner keys (P14).
            await _cashDebtLock.AcquireAsync(branchId, rows.Select(o => o.PartnerId).Distinct(), Array.Empty<Guid>(), c);
            var openingDate = await _db.Branches.AsNoTracking().Where(b => b.Id == branchId)
                .Select(b => b.OpeningDate).SingleAsync(c)
                ?? throw new InvalidOperationException("Opening debts cannot be re-posted without a branch opening date.");

            // Amounts do not change, so no settlement is dropped; the ledger moves the settlements' EffectiveAt.
            foreach (var row in rows)
                await _ledger.PostAsync(DebtSourceType.Opening, row.Id, new[] { Draft(row, openingDate) },
                    confirmRemoveSettlements: false, c);
        }, ct);
    }

    /// Shared branch gate, then the partner keys (P14).
    private async Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> partnerIds, CancellationToken ct)
    {
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        await _cashDebtLock.AcquireAsync(branchId, partnerIds.Distinct(), Array.Empty<Guid>(), ct);
    }

    /// Locks the row's current partner (and the new one) and returns the tracked row of the working branch. The row is
    /// always marked modified so the xmin check runs even when nothing else changes (D29).
    private async Task<OpeningDebt> LockRowAsync(Guid branchId, Guid id, Guid? newPartnerId, uint? version, CancellationToken ct)
    {
        var oldPartnerId = await _db.OpeningDebts.AsNoTracking()
            .Where(o => o.Id == id && o.BranchId == branchId)
            .Select(o => (Guid?)o.PartnerId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(OpeningDebt), id);

        var partnerIds = newPartnerId is { } newId ? new[] { oldPartnerId, newId } : new[] { oldPartnerId };
        await AcquireLocksAsync(branchId, partnerIds, ct);

        var row = await _db.OpeningDebts.FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct)
            ?? throw new NotFoundException(nameof(OpeningDebt), id);
        // A concurrent save moved the row to a partner whose key this write does not hold.
        if (row.PartnerId != oldPartnerId)
            throw new ConflictException("Công nợ đầu kỳ vừa được người khác cập nhật. Vui lòng tải lại.");
        if (version is null)
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["version"] = new[] { "Thiếu phiên bản dữ liệu của công nợ đầu kỳ." } }, null);

        _db.Entry(row).Property(o => o.Version).OriginalValue = version.Value;
        row.UpdatedAt = _clock.UtcNow;
        row.UpdatedBy = _currentUser.UserId;
        return row;
    }

    /// Read after the locks (P14): the branch opening date, which must be set and outside the locked period.
    private async Task<DateOnly> OpeningDateAsync(Guid branchId, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking().Where(b => b.Id == branchId)
            .Select(b => new { b.OpeningDate, b.LockedUntil }).SingleAsync(ct);
        var openingDate = branch.OpeningDate
            ?? throw new DomainException("OPENING_DATE_NOT_SET", "Chưa đặt ngày đầu kỳ của chi nhánh.");
        PeriodLockGuard.EnsureNotLocked(branch.LockedUntil, new[] { openingDate });
        return openingDate;
    }

    private async Task ValidateAsync(UpsertOpeningDebtRequest request, DateOnly openingDate, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors[key] = new[] { message };

        if (request.DocDate > openingDate)
            Add("docDate", "Ngày chứng từ phải trước hoặc bằng ngày đầu kỳ.");
        if (request.Kind == OpeningDebtKind.Prepayment && request.DueDate is not null)
            Add("dueDate", "Khoản trả trước không có hạn thanh toán.");
        else if (request.DueDate is { } dueDate && dueDate < request.DocDate)
            Add("dueDate", "Hạn thanh toán không được trước ngày chứng từ.");
        if (request.Amount <= 0)
            Add("amount", "Số tiền phải lớn hơn 0.");

        var partner = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == request.PartnerId)
            .Select(c => new { c.IsCustomer, c.IsSupplier })
            .FirstOrDefaultAsync(ct);
        if (partner is null)
            Add("partnerId", "Đối tượng không tồn tại.");
        else if (!DebtEffectRules.PartnerHasSideRole(request.Side, partner.IsCustomer, partner.IsSupplier))
            Add("partnerId", request.Side == DebtSide.Receivable
                ? "Đối tượng phải là khách hàng."
                : "Đối tượng phải là nhà cung cấp.");

        var docNo = request.DocNo?.Trim();
        if (string.IsNullOrEmpty(docNo))
            Add("docNo", "Vui lòng nhập số chứng từ.");
        else if (docNo.Length > 50)
            Add("docNo", "Số chứng từ tối đa 50 ký tự.");

        if (request.OwnerUserId is { } ownerId && !await _db.Users.AnyAsync(u => u.Id == ownerId, ct))
            Add("ownerUserId", "Nhân viên không tồn tại.");

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    private static void Apply(OpeningDebt row, UpsertOpeningDebtRequest request)
    {
        row.PartnerId = request.PartnerId;
        row.Side = request.Side;
        row.Kind = request.Kind;
        row.DocNo = request.DocNo.Trim();
        row.DocDate = request.DocDate;
        row.DueDate = request.DueDate;
        row.Amount = request.Amount;
        row.OwnerUserId = request.OwnerUserId;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
    }

    /// Debt → Increase, Prepayment → Decrease, posted at 00:00 (VN) of the branch opening date.
    private static DebtEntryDraft Draft(OpeningDebt row, DateOnly openingDate)
    {
        var direction = row.Kind == OpeningDebtKind.Debt ? DebtDirection.Increase : DebtDirection.Decrease;
        return new DebtEntryDraft(row.BranchId, row.PartnerId, row.Side, direction, row.Amount,
            VnTime.StartOfDay(openingDate), row.DocDate, DebtDates.EntryDueDate(direction, row.DocDate, row.DueDate),
            row.OwnerUserId, row.DocNo, row.Note ?? DefaultDescription);
    }

    private async Task<OpeningDebtDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await Project(_db.OpeningDebts.AsNoTracking().Where(o => o.Id == id)).SingleAsync(ct);
        await FillSettledAsync(new[] { dto }, ct);
        return dto;
    }

    private IQueryable<OpeningDebtDto> Project(IQueryable<OpeningDebt> query) =>
        query.Select(o => new OpeningDebtDto
        {
            Id = o.Id,
            PartnerId = o.PartnerId,
            PartnerCode = o.Partner!.Code,
            PartnerName = o.Partner.Name,
            Side = o.Side,
            Kind = o.Kind,
            DocNo = o.DocNo,
            DocDate = o.DocDate,
            DueDate = o.DueDate,
            Amount = o.Amount,
            OwnerUserId = o.OwnerUserId,
            OwnerName = _db.Users.Where(u => u.Id == o.OwnerUserId).Select(u => u.FullName).FirstOrDefault(),
            Note = o.Note,
            Version = o.Version,
        });

    /// Settled = Σ settlements of each row's Opening entry.
    private async Task FillSettledAsync(IReadOnlyCollection<OpeningDebtDto> items, CancellationToken ct)
    {
        var ids = items.Select(i => i.Id).ToList();
        var entries = await _db.DebtEntries.AsNoTracking()
            .Where(e => e.SourceType == DebtSourceType.Opening && ids.Contains(e.SourceId))
            .Select(e => new { e.Id, e.SourceId })
            .ToListAsync(ct);
        var settled = await _ledger.SettledAmountsAsync(entries.Select(e => e.Id), ct: ct);
        var bySource = entries.ToDictionary(e => e.SourceId, e => settled[e.Id]);
        foreach (var item in items)
            item.Settled = bySource.GetValueOrDefault(item.Id);
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
