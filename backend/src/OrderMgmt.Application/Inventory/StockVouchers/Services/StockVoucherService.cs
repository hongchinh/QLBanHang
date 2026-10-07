using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Interfaces;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.Numbering;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Sales.Quotations.Helpers;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

/// Stock-in / stock-out vouchers. Every write follows the section-03 §5 save rules in one transaction.
public class StockVoucherService : IStockVoucherService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryPostingService _posting;
    private readonly IDocumentCounter _counter;
    private readonly ICustomerService _customers;

    public StockVoucherService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch,
        IDateTime clock, ITransactionRunner transaction, IInventoryPostingService posting, IDocumentCounter counter,
        ICustomerService customers)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
        _posting = posting;
        _counter = counter;
        _customers = customers;
    }

    public async Task<StockVoucherListResult> ListAsync(StockVoucherListRequest request, CancellationToken ct = default)
    {
        EnsurePermission(StockVoucherPermissions.View(request.Type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        var query = _db.StockVouchers.AsNoTracking()
            .Where(v => v.BranchId == branchId && v.Type == request.Type);

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
        if (request.WarehouseId is { } warehouseId)
            query = query.Where(v => v.WarehouseId == warehouseId || v.Lines.Any(l => l.WarehouseId == warehouseId));
        if (request.PartnerId is { } partnerId)
            query = query.Where(v => v.PartnerId == partnerId);
        if (request.ReasonId is { } reasonId)
            query = query.Where(v => v.ReasonId == reasonId);
        if (request.Status is { } status)
            query = query.Where(v => v.Status == status);

        var ownerIds = OwnerIdListParser.Parse(request.OwnerUserIds);
        if (ownerIds.Count > 0)
            query = query.Where(v => ownerIds.Contains(v.OwnerUserId));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(v => EF.Functions.ILike(v.Code, pattern) || EF.Functions.ILike(v.PartnerName!, pattern));
        }

        // Cancelled vouchers are excluded from the totals unless a status is requested explicitly.
        var aggregates = await (request.Status is null ? query.Where(v => v.Status != StockVoucherStatus.Cancelled) : query)
            .GroupBy(_ => 1)
            .Select(g => new StockVoucherListAggregates
            {
                GoodsAmount = g.Sum(v => v.GoodsAmount),
                DiscountTotal = g.Sum(v => v.LineDiscountTotal + v.OrderDiscount),
                VatTotal = g.Sum(v => v.VatTotal),
                Freight = g.Sum(v => v.Freight),
                Total = g.Sum(v => v.Total),
                PaidAmount = g.Sum(v => v.PaidAmount),
            })
            .FirstOrDefaultAsync(ct) ?? new StockVoucherListAggregates();

        query = (request.SortBy?.ToLowerInvariant(), request.SortDirection?.ToLowerInvariant()) switch
        {
            ("code", "desc") => query.OrderByDescending(v => v.Code),
            ("code", _) => query.OrderBy(v => v.Code),
            ("date", "desc") => query.OrderByDescending(v => v.VoucherAt),
            ("date", _) => query.OrderBy(v => v.VoucherAt),
            ("total", "desc") => query.OrderByDescending(v => v.Total),
            ("total", _) => query.OrderBy(v => v.Total),
            _ => query.OrderByDescending(v => v.VoucherAt),
        };

        var totalItems = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(v => new StockVoucherListItemDto
            {
                Id = v.Id,
                Type = v.Type,
                Code = v.Code,
                VoucherAt = v.VoucherAt,
                WarehouseName = v.Warehouse!.Name,
                PartnerName = v.PartnerName,
                ReasonName = v.Reason!.Name,
                PaymentMethodName = v.PaymentMethod != null ? v.PaymentMethod.Name : null,
                GoodsAmount = v.GoodsAmount,
                DiscountTotal = v.LineDiscountTotal + v.OrderDiscount,
                VatTotal = v.VatTotal,
                Freight = v.Freight,
                Total = v.Total,
                PaidAmount = v.PaidAmount,
                Status = v.Status,
                OwnerUserId = v.OwnerUserId,
                CreatedAt = v.CreatedAt,
            })
            .ToListAsync(ct);

        // Separate query: IgnoreQueryFilters anywhere in a query also switches off the vouchers' soft-delete filter.
        var pageOwnerIds = items.Select(i => i.OwnerUserId).Distinct().ToList();
        var ownerNames = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => pageOwnerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        foreach (var item in items)
            item.OwnerName = ownerNames.GetValueOrDefault(item.OwnerUserId);

        return new StockVoucherListResult
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalItems = totalItems,
            Aggregates = aggregates,
        };
    }

    /// Creators of the type's vouchers in the working branch (deleted users included, so old vouchers stay filterable).
    public async Task<IReadOnlyList<StockVoucherOwnerDto>> ListOwnersAsync(StockDirection type, CancellationToken ct = default)
    {
        EnsurePermission(StockVoucherPermissions.View(type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        // Materialized first: composed into the IgnoreQueryFilters query below, deleted vouchers would count.
        var ownerIds = await _db.StockVouchers
            .Where(v => v.BranchId == branchId && v.Type == type)
            .Select(v => v.OwnerUserId)
            .Distinct()
            .ToListAsync(ct);

        var owners = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .Select(u => new StockVoucherOwnerDto { Id = u.Id, FullName = u.FullName })
            .ToListAsync(ct);

        var vietnameseComparer = StringComparer.Create(new CultureInfo("vi-VN"), ignoreCase: true);
        return owners.OrderBy(o => o.FullName, vietnameseComparer).ToList();
    }

    /// Form defaults: selections from the current user's latest-created voucher of the type, else the fallbacks;
    /// NextCode is a peek and does not use a number.
    public async Task<StockVoucherDefaultsDto> GetDefaultsAsync(StockDirection type, DateTimeOffset? voucherAt, CancellationToken ct = default)
    {
        EnsurePermission(StockVoucherPermissions.Create(type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        var userId = _currentUser.UserId;
        var previous = await _db.StockVouchers.AsNoTracking()
            .Where(v => v.BranchId == branchId && v.Type == type && v.OwnerUserId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new { v.VoucherAt, v.WarehouseId, v.ReasonId, v.PaymentMethodId })
            .FirstOrDefaultAsync(ct);
        var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);

        // Never IDateTime.Now (D27).
        var at = voucherAt is { } requested ? VnTime.ToUtc(requested)
            : settings.DefaultDateMode == DefaultDateMode.PreviousVoucher && previous is not null ? previous.VoucherAt
            : _clock.UtcNow;

        return new StockVoucherDefaultsDto
        {
            NextCode = await NextCodeAsync(type, branchId, at, peek: true, ct),
            VoucherAt = at,
            WarehouseId = previous?.WarehouseId ?? await _db.Warehouses.AsNoTracking()
                .Where(w => w.BranchId == branchId && w.IsActive)
                .OrderBy(w => w.Code)
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync(ct),
            ReasonId = previous?.ReasonId ?? await _db.StockReasons.AsNoTracking()
                .Where(r => r.Direction == type)
                .OrderByDescending(r => r.IsSystem)
                .ThenBy(r => r.Code)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct),
            PaymentMethodId = previous?.PaymentMethodId,
        };
    }

    /// Quantity per requested (product, warehouse) from the working branch's ledger up to `At` (inclusive),
    /// without the rows of the voucher being edited; pairs without rows return 0.
    public async Task<IReadOnlyList<StockAtResult>> GetStockAtAsync(StockAtRequest request, CancellationToken ct = default)
    {
        EnsurePermission(StockVoucherPermissions.View(request.Type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        var at = VnTime.ToUtc(request.At);
        var pairs = request.Items.Select(i => (i.ProductId, i.WarehouseId)).Distinct().ToList();
        var productIds = pairs.Select(p => p.ProductId).Distinct().ToList();
        var warehouseIds = pairs.Select(p => p.WarehouseId).Distinct().ToList();

        var rows = _db.InventoryLedger.AsNoTracking()
            .Where(e => e.BranchId == branchId && e.PostedAt <= at
                && productIds.Contains(e.ProductId) && warehouseIds.Contains(e.WarehouseId));
        if (request.ExcludeVoucherId is { } excluded)
            rows = rows.Where(e => e.SourceId != excluded);

        var quantities = await rows
            .GroupBy(e => new { e.ProductId, e.WarehouseId })
            .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Quantity = g.Sum(e => e.QtyIn - e.QtyOut) })
            .ToDictionaryAsync(x => (x.ProductId, x.WarehouseId), x => x.Quantity, ct);

        return pairs.Select(p => new StockAtResult
        {
            ProductId = p.ProductId,
            WarehouseId = p.WarehouseId,
            Quantity = quantities.GetValueOrDefault(p),
        }).ToList();
    }

    /// Partner picker (D3). With a reason (form): its partner type, active partners only.
    /// Without one (list filter): any role, inactive partners included.
    public async Task<List<CustomerSearchItemDto>> SearchPartnersAsync(StockDirection type, string? keyword, int limit,
        Guid? reasonId, CancellationToken ct = default)
    {
        EnsurePermission(StockVoucherPermissions.View(type));
        var partnerType = PartnerType.Any;
        if (reasonId is { } id)
        {
            var reason = await _db.StockReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (reason is null || reason.Direction != type)
                throw new ValidationDomainException(new Dictionary<string, string[]>
                {
                    ["reasonId"] = new[] { reason is null ? "Lý do không tồn tại." : "Lý do không đúng loại phiếu." },
                }, null);
            partnerType = reason.PartnerType;
        }

        return await _customers.SearchAsync(new CustomerSearchRequest
        {
            Keyword = keyword ?? string.Empty,
            Limit = limit,
            ActiveOnly = reasonId.HasValue,
        }, partnerType, ct);
    }

    public Task<StockVoucherDto> GetAsync(Guid id, CancellationToken ct = default) =>
        LoadDtoAsync(id, ct, checkAccess: true);

    /// Write operations return the saved voucher with the permission they were authorized by (create / edit /
    /// cancel), not view, so a create-only user never gets 403 for a voucher that was saved (review finding).
    private async Task<StockVoucherDto> LoadDtoAsync(Guid id, CancellationToken ct, bool checkAccess = false)
    {
        var voucher = await _db.StockVouchers.AsNoTracking()
            .Include(v => v.Warehouse)
            .Include(v => v.Partner)
            .Include(v => v.Reason)
            .Include(v => v.PaymentMethod)
            .Include(v => v.Lines).ThenInclude(l => l.Warehouse)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(StockVoucher), id);

        if (checkAccess)
        {
            EnsurePermission(StockVoucherPermissions.View(voucher.Type));
            await EnsureWorkingBranchAsync(voucher.BranchId, ct);
        }

        var ownerName = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == voucher.OwnerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        var dto = ToDto(voucher, ownerName);
        var mayAct = voucher.OwnerUserId == _currentUser.UserId
            || _currentUser.HasPermission(StockVoucherPermissions.EditAll(voucher.Type));
        var active = voucher.Status != StockVoucherStatus.Cancelled;
        dto.CanEdit = mayAct && active && _currentUser.HasPermission(StockVoucherPermissions.Edit(voucher.Type));
        dto.CanDelete = mayAct && active && _currentUser.HasPermission(StockVoucherPermissions.Delete(voucher.Type));
        // Also true for a Cancelled voucher: restore uses the same permission.
        dto.CanCancel = mayAct && _currentUser.HasPermission(StockVoucherPermissions.Cancel(voucher.Type));
        return dto;
    }

    public async Task<IReadOnlyList<StockVoucherActivityDto>> ListActivitiesAsync(Guid id, CancellationToken ct = default)
    {
        var voucher = await _db.StockVouchers.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new { v.Type, v.BranchId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(StockVoucher), id);

        EnsurePermission(StockVoucherPermissions.View(voucher.Type));
        await EnsureWorkingBranchAsync(voucher.BranchId, ct);

        return await _db.StockVoucherActivities.AsNoTracking()
            .Where(a => a.StockVoucherId == id)
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new StockVoucherActivityDto
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

    public async Task<StockVoucherDto> CreateAsync(UpsertStockVoucherRequest request, CancellationToken ct = default)
    {
        EnsurePermission(StockVoucherPermissions.Create(request.Type));
        var branchId = await _currentBranch.GetIdAsync(ct);
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        var id = await _transaction.RunAsync(async c =>
        {
            await ValidateAsync(request, branchId, null, c);

            var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
            await _posting.AcquireLocksAsync(branchId, productIds, c);
            // Read after the shared branch gate, so a concurrent period lock change is serialized (review finding).
            await EnsureNotLockedAsync(branchId, new[] { voucherAt }, c);
            var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, c);

            var voucher = new StockVoucher
            {
                Type = request.Type,
                Code = await NextCodeAsync(request.Type, branchId, voucherAt, peek: false, c),
                BranchId = branchId,
                OwnerUserId = _currentUser.UserId ?? throw new UnauthorizedAccessException(),
            };
            _db.StockVouchers.Add(voucher);
            await ApplyRequestAsync(voucher, request, voucherAt, settings, c);
            await _db.SaveChangesAsync(c);

            await PostLinesAsync(voucher, settings, request.AcknowledgeNegativeStock, c);
            if (voucher.Type == StockDirection.In)
                await RecomputeCostPricesAsync(productIds, c);
            AddActivity(voucher, StockVoucherActivityAction.Created, "Tạo phiếu");
            await _db.SaveChangesAsync(c);
            return voucher.Id;
        }, ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<StockVoucherDto> UpdateAsync(Guid id, UpsertStockVoucherRequest request, CancellationToken ct = default)
    {
        var voucherAt = VnTime.ToUtc(request.VoucherAt);

        await _transaction.RunAsync(async c =>
        {
            var voucher = await LoadForWriteAsync(id, StockVoucherPermissions.Edit, request.Version, cancelled: false, c);
            await ValidateAsync(request, voucher.BranchId, voucher, c);

            var productIds = voucher.Lines.Select(l => l.ProductId)
                .Concat(request.Lines.Select(l => l.ProductId)).Distinct().ToList();
            await _posting.AcquireLocksAsync(voucher.BranchId, productIds, c);
            await EnsureNotLockedAsync(voucher.BranchId, new[] { voucher.VoucherAt, voucherAt }, c);
            var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, c);

            await ApplyRequestAsync(voucher, request, voucherAt, settings, c);
            await _db.SaveChangesAsync(c);

            await PostLinesAsync(voucher, settings, request.AcknowledgeNegativeStock, c);
            if (voucher.Type == StockDirection.In)
                await RecomputeCostPricesAsync(productIds, c);
            AddActivity(voucher, StockVoucherActivityAction.Updated, "Cập nhật phiếu");
            await _db.SaveChangesAsync(c);
        }, ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<StockVoucherDto> CancelAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default)
    {
        await ChangeLifecycleAsync(id, request, StockVoucherPermissions.Cancel, StockVoucherActivityAction.Cancelled, "Hủy phiếu",
            v =>
            {
                v.Status = StockVoucherStatus.Cancelled;
                v.CancelledAt = _clock.UtcNow;
                v.CancelledBy = _currentUser.UserId;
            }, ct);
        return await LoadDtoAsync(id, ct);
    }

    public async Task<StockVoucherDto> RestoreAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default)
    {
        await ChangeLifecycleAsync(id, request, StockVoucherPermissions.Cancel, StockVoucherActivityAction.Restored, "Khôi phục phiếu",
            v =>
            {
                v.Status = StockVoucherStatus.Active;
                v.CancelledAt = null;
                v.CancelledBy = null;
            }, ct);
        return await LoadDtoAsync(id, ct);
    }

    // Soft delete; AppDbContext cascades IsDeleted to the lines and activities.
    public Task DeleteAsync(Guid id, StockVoucherActionRequest request, CancellationToken ct = default) =>
        ChangeLifecycleAsync(id, request, StockVoucherPermissions.Delete, StockVoucherActivityAction.Deleted, "Xóa phiếu",
            v => v.IsDeleted = true, ct);

    /// Cancel / delete: steps 1, 2 (stored date), 4, 7 (empty list), 8, 9.
    /// Restore: the same steps, reposting the stored lines as they are (no re-validation, no re-snapshot).
    private Task ChangeLifecycleAsync(Guid id, StockVoucherActionRequest request, Func<StockDirection, string> permission,
        StockVoucherActivityAction action, string description, Action<StockVoucher> apply, CancellationToken ct) =>
        _transaction.RunAsync(async c =>
        {
            var restore = action == StockVoucherActivityAction.Restored;
            var voucher = await LoadForWriteAsync(id, permission, request.Version, cancelled: restore, c);
            var productIds = voucher.Lines.Select(l => l.ProductId).Distinct().ToList();
            await _posting.AcquireLocksAsync(voucher.BranchId, productIds, c);
            await EnsureNotLockedAsync(voucher.BranchId, new[] { voucher.VoucherAt }, c);
            if (restore)
                await EnsureRestorableAsync(voucher, c);
            var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, c);

            apply(voucher);
            await _db.SaveChangesAsync(c);

            await PostLinesAsync(voucher, settings, request.AcknowledgeNegativeStock, c, removeAll: !restore);
            if (voucher.Type == StockDirection.In)
                await RecomputeCostPricesAsync(productIds, c);
            AddActivity(voucher, action, description);
            await _db.SaveChangesAsync(c);
        }, ct);

    // ---- Save rules 1 to 3 -------------------------------------------------------------------

    /// Save rule 1 for update, cancel, restore and delete. `cancelled` is the status the operation requires.
    /// The header is always marked modified so the xmin check runs even when only lines change (D29).
    private async Task<StockVoucher> LoadForWriteAsync(Guid id, Func<StockDirection, string> permission, uint? version,
        bool cancelled, CancellationToken ct)
    {
        var voucher = await _db.StockVouchers.Include(v => v.Lines).FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(StockVoucher), id);

        EnsurePermission(permission(voucher.Type));
        await EnsureWorkingBranchAsync(voucher.BranchId, ct);
        if (voucher.OwnerUserId != _currentUser.UserId
            && !_currentUser.HasPermission(StockVoucherPermissions.EditAll(voucher.Type)))
            throw new ForbiddenException("Bạn chỉ được thao tác trên phiếu do mình tạo.");
        if (cancelled && voucher.Status != StockVoucherStatus.Cancelled)
            throw new ConflictException("Phiếu chưa bị hủy.");
        if (!cancelled && voucher.Status == StockVoucherStatus.Cancelled)
            throw new ConflictException("Phiếu đã hủy, không thể thay đổi.");
        if (version is null)
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["version"] = new[] { "Thiếu phiên bản dữ liệu của phiếu." } }, null);

        _db.Entry(voucher).Property(v => v.Version).OriginalValue = version.Value;
        voucher.UpdatedAt = _clock.UtcNow;
        voucher.UpdatedBy = _currentUser.UserId;
        return voucher;
    }

    private static readonly DateTimeOffset MinVoucherAt = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// Restore reposts the stored lines unchanged, so their snapshot must still describe the stock: the warehouse is
    /// still in the voucher's branch and the product's stock fields have not changed since the cancel (review finding).
    private async Task EnsureRestorableAsync(StockVoucher voucher, CancellationToken ct)
    {
        var lines = voucher.Lines.Where(l => !l.IsDeleted).ToList();
        var products = await LoadProductsAsync(lines.Select(l => l.ProductId), ct);
        var warehouseIds = lines.Select(l => l.WarehouseId).Distinct().ToList();
        var warehouseBranches = await _db.Warehouses.AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.BranchId, ct);

        foreach (var line in lines)
        {
            if (!warehouseBranches.TryGetValue(line.WarehouseId, out var branchId) || branchId != voucher.BranchId)
                throw new ConflictException($"Không thể khôi phục: kho của dòng {line.ProductCode} không còn thuộc chi nhánh của phiếu.");
            if (!products.TryGetValue(line.ProductId, out var product)
                || product.TrackInventory != line.TrackInventory
                || product.PricingMode != line.PricingMode
                || StockUnit.NameFor(product.PricingMode, product.Unit?.Name) != line.UnitName)
                throw new ConflictException($"Không thể khôi phục: hàng hóa {line.ProductCode} đã đổi cách tính, ĐVT hoặc theo dõi tồn.");
        }
    }

    private async Task EnsureNotLockedAsync(Guid branchId, IEnumerable<DateTimeOffset> instants, CancellationToken ct)
    {
        var lockedUntil = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.LockedUntil).SingleAsync(ct);
        if (lockedUntil is { } locked && instants.Any(at => VnTime.ToVnDate(at) <= locked))
            throw new DomainException("PERIOD_LOCKED",
                string.Create(CultureInfo.InvariantCulture, $"Ngày chứng từ đã khóa sổ (đến {locked:dd/MM/yyyy})."));
    }

    /// Reference and amount rules (save rule 3), collected into one 400. `existing` is null on create.
    private async Task ValidateAsync(UpsertStockVoucherRequest request, Guid branchId, StockVoucher? existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors.TryAdd(key, new[] { message });

        if (existing is not null && existing.Type != request.Type)
            Add("type", "Không được đổi loại phiếu.");

        var reason = await _db.StockReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReasonId, ct);
        if (reason is null)
            Add("reasonId", "Lý do không tồn tại.");
        else if (reason.Direction != request.Type)
            Add("reasonId", "Lý do không đúng loại phiếu.");

        Customer? partner = null;
        if (request.PartnerId.HasValue)
        {
            partner = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.PartnerId.Value, ct);
            if (partner is null)
                Add("partnerId", "Đối tượng không tồn tại.");
        }
        switch (reason?.PartnerType)
        {
            case PartnerType.None when request.PartnerId.HasValue:
                Add("partnerId", "Lý do này không dùng đối tượng.");
                break;
            case PartnerType.Customer when request.PartnerId is null:
                Add("partnerId", "Lý do này yêu cầu chọn khách hàng.");
                break;
            case PartnerType.Customer when partner is { IsCustomer: false }:
                Add("partnerId", "Đối tượng không phải khách hàng.");
                break;
            case PartnerType.Supplier when request.PartnerId is null:
                Add("partnerId", "Lý do này yêu cầu chọn nhà cung cấp.");
                break;
            case PartnerType.Supplier when partner is { IsSupplier: false }:
                Add("partnerId", "Đối tượng không phải nhà cung cấp.");
                break;
        }

        var warehouseIds = request.Lines.Select(l => l.WarehouseId ?? request.WarehouseId)
            .Append(request.WarehouseId).Distinct().ToList();
        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, ct);
        string? WarehouseError(Guid warehouseId, bool requireActive) =>
            !warehouses.TryGetValue(warehouseId, out var w) || w.BranchId != branchId
                ? "Kho không thuộc chi nhánh đang làm việc."
                : requireActive && !w.IsActive ? "Kho đã ngừng sử dụng." : null;

        // "Active" is required only for new or changed references (review m15).
        if (WarehouseError(request.WarehouseId, existing is null || existing.WarehouseId != request.WarehouseId) is { } headerError)
            Add("warehouseId", headerError);

        if (request.PaymentMethodId.HasValue
            && !await _db.PaymentMethods.AnyAsync(m => m.Id == request.PaymentMethodId.Value, ct))
            Add("paymentMethodId", "Phương thức thanh toán không tồn tại.");

        if (request.Lines.Count == 0)
            Add("lines", "Phiếu phải có ít nhất 1 dòng hàng.");

        var voucherAt = VnTime.ToUtc(request.VoucherAt);
        if (voucherAt < MinVoucherAt || voucherAt > _clock.UtcNow.AddYears(1))
            Add("voucherAt", "Ngày chứng từ không hợp lệ.");

        var products = await LoadProductsAsync(request.Lines.Select(l => l.ProductId), ct);
        var storedLines = existing?.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id)
            ?? new Dictionary<Guid, StockVoucherLine>();
        var priced = new List<(int Index, UpsertStockVoucherLineRequest Line, LineSnapshot Snapshot)>();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var key = $"lines[{i}]";
            if (line.Id is { } repeatedId && request.Lines.Take(i).Any(l => l.Id == repeatedId))
                Add($"{key}.id", "Dòng hàng bị trùng.");
            var stored = line.Id is { } lineId && storedLines.TryGetValue(lineId, out var s) ? s : null;
            var productChanged = stored is null || stored.ProductId != line.ProductId;
            var warehouseChanged = stored is null || productChanged || stored.WarehouseId != (line.WarehouseId ?? request.WarehouseId);

            if (WarehouseError(line.WarehouseId ?? request.WarehouseId, warehouseChanged) is { } lineWarehouseError)
                Add($"{key}.warehouseId", lineWarehouseError);

            if (!products.TryGetValue(line.ProductId, out var product))
            {
                Add($"{key}.productId", "Hàng hóa không tồn tại.");
                continue;
            }
            if (productChanged && product.Status != ProductStatus.Active)
                Add($"{key}.productId", "Hàng hóa đã ngừng kinh doanh.");
            var snapshot = SnapshotOf(productChanged ? null : stored, product);

            var dimensionsValid = true;
            void RequirePositive(decimal? value, string field, string label)
            {
                if (value is null or <= 0)
                {
                    Add($"{key}.{field}", $"{label} phải lớn hơn 0.");
                    dimensionsValid = false;
                }
            }
            if (snapshot.PricingMode == PricingMode.PerUnit)
            {
                RequirePositive(line.Quantity, "quantity", "Số lượng");
            }
            else
            {
                RequirePositive(line.SheetCount, "sheetCount", "Số tấm");
                RequirePositive(line.Length, "length", "Chiều dài");
                if (snapshot.PricingMode is PricingMode.PerSquareMeter or PricingMode.PerCubicMeter)
                    RequirePositive(line.Width, "width", "Chiều rộng");
                if (snapshot.PricingMode == PricingMode.PerCubicMeter)
                    RequirePositive(line.Thickness, "thickness", "Chiều dày");
            }

            if (line.UnitPrice < 0)
                Add($"{key}.unitPrice", "Đơn giá không được âm.");
            if (line.DiscountRate is < 0 or > 100)
                Add($"{key}.discountRate", "Tỷ lệ chiết khấu phải từ 0 đến 100.");
            if (line.VatRate is < 0 or > 100)
                Add($"{key}.vatRate", "Thuế suất phải từ 0 đến 100.");
            if (dimensionsValid)
                priced.Add((i, line, snapshot));
        }

        // Amount-based rules use the calculator's line amounts (allocations do not matter here).
        var computed = StockVoucherCalculator.Compute(new StockHeaderInput(request.Type, 0m, 0m, false),
            priced.Select(x => ToCalculatorInput(x.Line, x.Snapshot)).ToList()).Lines;
        var netSum = 0m;
        for (var j = 0; j < priced.Count; j++)
        {
            var (index, line, _) = priced[j];
            var result = computed[j];
            if (result.Quantity <= 0)
                Add($"lines[{index}].quantity", "Số lượng phải lớn hơn 0.");
            if (line.DiscountManual && line.DiscountAmount is { } amount && (amount < 0 || amount > result.Amount))
                Add($"lines[{index}].discountAmount", "Tiền chiết khấu phải từ 0 đến thành tiền.");
            netSum += result.Amount - result.DiscountAmount;
        }
        if (request.OrderDiscount < 0 || request.OrderDiscount > netSum)
            Add("orderDiscount", "Chiết khấu phiếu phải từ 0 đến tổng tiền sau chiết khấu dòng.");
        if (request.Freight < 0)
            Add("freight", "Phí vận chuyển không được âm.");
        if (request.PaidAmount < 0)
            Add("paidAmount", "Số tiền thanh toán không được âm.");

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    // ---- Writes -----------------------------------------------------------------------------

    private const int CodeBatchSize = 100;
    private const int MaxCodeBatches = 1000;

    /// `peek` reads the next number without using it (form defaults); otherwise the counter is incremented.
    /// A number whose code is already taken in the (type, branch) is skipped: after a reset-policy change the
    /// counter of the new period can restart on codes issued under the old one (review finding). Candidates are
    /// checked in batches; on create the counter row lock is held, so the chosen code stays free until commit.
    private async Task<string> NextCodeAsync(StockDirection type, Guid branchId, DateTimeOffset voucherAt, bool peek,
        CancellationToken ct)
    {
        var docType = DocTypeOf(type);
        var numbering = await _db.DocumentNumberings.AsNoTracking()
            .SingleOrDefaultAsync(n => n.DocType == docType && n.BranchId == branchId, ct)
            ?? throw new DomainException("NUMBERING_NOT_CONFIGURED", "Chi nhánh chưa cấu hình đánh số chứng từ.");
        var date = VnTime.ToVnDate(voucherAt);
        var periodKey = DocumentNumberFormatter.PeriodKey(numbering.ResetPolicy, date);
        var next = peek
            ? await _counter.PeekNextAsync(docType, branchId, periodKey, ct)
            : await _counter.NextAsync(docType, branchId, periodKey, ct);

        for (var batch = 0; batch < MaxCodeBatches; batch++)
        {
            var candidates = Enumerable.Range(0, CodeBatchSize)
                .Select(k => next + batch * CodeBatchSize + k)
                .Select(value => (Value: value,
                    Code: DocumentNumberFormatter.Format(numbering.Pattern, numbering.Prefix, numbering.Length, value, date)))
                .ToList();
            var codes = candidates.Select(c => c.Code).ToList();
            // Same scope as the unique index (type, branch_id, code) WHERE is_deleted = false: the query filter matches it.
            var taken = (await _db.StockVouchers
                .Where(v => v.Type == type && v.BranchId == branchId && codes.Contains(v.Code))
                .Select(v => v.Code)
                .ToListAsync(ct)).ToHashSet();

            foreach (var (value, code) in candidates)
            {
                if (taken.Contains(code))
                    continue;
                if (!peek && value != next)
                    await _counter.AdvanceToAsync(docType, branchId, periodKey, value, ct);
                return code;
            }
        }
        throw new ConflictException("Không tìm được số chứng từ còn trống. Vui lòng đổi mẫu đánh số.");
    }

    /// Header fields, line upsert and totals from StockVoucherCalculator (never from the client).
    private async Task ApplyRequestAsync(StockVoucher voucher, UpsertStockVoucherRequest request,
        DateTimeOffset voucherAt, InventorySettings settings, CancellationToken ct)
    {
        var partner = request.PartnerId.HasValue
            ? await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.PartnerId.Value, ct)
            : null;

        voucher.VoucherAt = voucherAt;
        voucher.WarehouseId = request.WarehouseId;
        voucher.ReasonId = request.ReasonId;
        voucher.PartnerId = request.PartnerId;
        voucher.PartnerName = request.PartnerName ?? partner?.Name;
        voucher.PartnerAddress = request.PartnerAddress ?? partner?.CompanyAddress;
        voucher.PartnerTaxCode = request.PartnerTaxCode ?? partner?.TaxCode;
        voucher.HandlerName = request.HandlerName;
        voucher.PaymentMethodId = request.PaymentMethodId;
        voucher.Note = request.Note;
        voucher.Freight = request.Freight;
        voucher.OrderDiscount = request.OrderDiscount;

        var requested = request.Lines.OrderBy(l => l.SortOrder).ToList();
        var products = await LoadProductsAsync(requested.Select(l => l.ProductId), ct);
        var existing = voucher.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        var snapshots = requested.Select(l => SnapshotOf(
            l.Id is { } lineId && existing.TryGetValue(lineId, out var stored) && stored.ProductId == l.ProductId ? stored : null,
            products[l.ProductId])).ToList();
        var computation = StockVoucherCalculator.Compute(
            new StockHeaderInput(request.Type, request.Freight, request.OrderDiscount, settings.NetExcludesVat),
            requested.Select((l, i) => ToCalculatorInput(l, snapshots[i])).ToList());

        var kept = new HashSet<Guid>();
        for (var i = 0; i < requested.Count; i++)
        {
            var line = requested[i];
            if (line.Id is { } lineId && existing.TryGetValue(lineId, out var entity))
            {
                kept.Add(lineId);
            }
            else
            {
                // New child rows go through the DbSet (see plan "Child rows").
                entity = new StockVoucherLine { StockVoucherId = voucher.Id };
                _db.StockVoucherLines.Add(entity);
            }

            var product = products[line.ProductId];
            var snapshot = snapshots[i];
            var result = computation.Lines[i];
            entity.SortOrder = line.SortOrder;
            entity.ProductId = product.Id;
            entity.ProductCode = product.Code;
            entity.ProductName = product.Name;
            entity.WarehouseId = line.WarehouseId ?? request.WarehouseId;
            entity.TrackInventory = snapshot.TrackInventory;
            entity.PricingMode = snapshot.PricingMode;
            entity.UnitName = snapshot.UnitName;
            entity.PriceIncludesVat = snapshot.PriceIncludesVat;
            entity.SheetCount = line.SheetCount;
            entity.Length = line.Length;
            entity.Width = line.Width;
            entity.Thickness = line.Thickness;
            entity.Quantity = result.Quantity;
            entity.UnitPrice = line.UnitPrice;
            entity.Amount = result.Amount;
            entity.DiscountRate = line.DiscountRate;
            entity.DiscountAmount = result.DiscountAmount;
            entity.DiscountManual = line.DiscountManual;
            entity.OrderDiscountAllocated = result.OrderDiscountAllocated;
            entity.FreightAllocated = result.FreightAllocated;
            entity.VatRate = line.VatRate;
            entity.VatAmount = result.VatAmount;
            entity.NetAmount = result.NetAmount;
            entity.InboundValue = result.InboundValue;
            entity.Note = line.Note;
        }

        foreach (var removed in existing.Values.Where(l => !kept.Contains(l.Id)))
        {
            removed.IsDeleted = true;
            removed.DeletedAt = _clock.UtcNow;
            removed.DeletedBy = _currentUser.UserId;
        }

        voucher.GoodsAmount = computation.Totals.GoodsAmount;
        voucher.LineDiscountTotal = computation.Totals.LineDiscountTotal;
        voucher.VatTotal = computation.Totals.VatTotal;
        voucher.Total = computation.Totals.Total;
        voucher.PaidAmount = request.PaidAmount ?? computation.Totals.Total;
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        return await _db.Products.AsNoTracking()
            .Include(p => p.Unit)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    /// Stock fields a line keeps from the moment its product was chosen.
    private sealed record LineSnapshot(bool TrackInventory, PricingMode PricingMode, string UnitName, bool PriceIncludesVat);

    /// A kept line (same id, same product) keeps its stored snapshot, so a later product change (e.g. TrackInventory
    /// turned on) never re-posts an old voucher differently; new lines and lines whose product changed (`stored`
    /// null) take the current product (review finding).
    private static LineSnapshot SnapshotOf(StockVoucherLine? stored, Product product) => stored is not null
        ? new(stored.TrackInventory, stored.PricingMode, stored.UnitName, stored.PriceIncludesVat)
        : new(product.TrackInventory, product.PricingMode, StockUnit.NameFor(product.PricingMode, product.Unit?.Name),
            product.PriceIncludesVat);

    private static StockLineInput ToCalculatorInput(UpsertStockVoucherLineRequest line, LineSnapshot snapshot) =>
        new(snapshot.TrackInventory, snapshot.PricingMode, snapshot.PriceIncludesVat,
            line.SheetCount, line.Length, line.Width, line.Thickness, line.Quantity,
            line.UnitPrice, line.DiscountRate, line.DiscountAmount, line.DiscountManual, line.VatRate);

    /// Ledger rows of the voucher's tracked lines (an empty list when cancelled or deleted), then costing.
    private async Task PostLinesAsync(StockVoucher voucher, InventorySettings settings, bool acknowledge,
        CancellationToken ct, bool removeAll = false)
    {
        var drafts = removeAll
            ? new List<LedgerEntryDraft>()
            : voucher.Lines
                .Where(l => !l.IsDeleted && l.TrackInventory)
                .Select(l => new LedgerEntryDraft(
                    l.ProductId, l.WarehouseId, voucher.BranchId, voucher.VoucherAt,
                    SourceTypeOf(voucher.Type), voucher.Id, l.Id, voucher.Code, l.SortOrder,
                    voucher.Type == StockDirection.In ? l.Quantity : 0m,
                    voucher.Type == StockDirection.Out ? l.Quantity : 0m,
                    voucher.Type == StockDirection.In
                        ? l.InboundValue + (settings.PurchaseCostIncludesVat ? l.VatAmount : 0m)
                        : 0m))
                .ToList();
        await _posting.PostAsync(SourceTypeOf(voucher.Type), voucher.Id, drafts, acknowledge, ct);
    }

    /// D35: CostPrice follows the latest Active, non-deleted stock-in line (by VoucherAt, then the voucher's
    /// CreatedAt and Code so vouchers with the same VoucherAt resolve deterministically, then SortOrder).
    private async Task RecomputeCostPricesAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        foreach (var productId in productIds.Distinct())
        {
            var latest = await _db.StockVoucherLines
                .Where(l => l.ProductId == productId
                    && l.StockVoucher!.Type == StockDirection.In
                    && l.StockVoucher.Status == StockVoucherStatus.Active)
                .OrderByDescending(l => l.StockVoucher!.VoucherAt)
                .ThenByDescending(l => l.StockVoucher!.CreatedAt)
                .ThenByDescending(l => l.StockVoucher!.Code)
                .ThenByDescending(l => l.SortOrder)
                .Select(l => new { l.UnitPrice, l.StockVoucher!.VoucherAt })
                .FirstOrDefaultAsync(ct);
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
            if (product is null)
                continue;

            if (latest is null)
            {
                product.CostPriceUpdatedOn = null;
            }
            else
            {
                product.CostPrice = latest.UnitPrice;
                product.CostPriceUpdatedOn = latest.VoucherAt;
            }
        }
    }

    private void AddActivity(StockVoucher voucher, StockVoucherActivityAction action, string description) =>
        _db.StockVoucherActivities.Add(new StockVoucherActivity
        {
            StockVoucherId = voucher.Id,
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

    private static DocumentType DocTypeOf(StockDirection type) =>
        type == StockDirection.In ? DocumentType.StockIn : DocumentType.StockOut;

    private static LedgerSourceType SourceTypeOf(StockDirection type) =>
        type == StockDirection.In ? LedgerSourceType.StockIn : LedgerSourceType.StockOut;

    private static StockVoucherDto ToDto(StockVoucher v, string? ownerName) => new()
    {
        Id = v.Id,
        Type = v.Type,
        Code = v.Code,
        VoucherAt = v.VoucherAt,
        BranchId = v.BranchId,
        WarehouseId = v.WarehouseId,
        WarehouseCode = v.Warehouse?.Code,
        WarehouseName = v.Warehouse?.Name,
        PartnerId = v.PartnerId,
        PartnerCode = v.Partner?.Code,
        PartnerName = v.PartnerName,
        PartnerAddress = v.PartnerAddress,
        PartnerTaxCode = v.PartnerTaxCode,
        HandlerName = v.HandlerName,
        ReasonId = v.ReasonId,
        ReasonName = v.Reason?.Name,
        PaymentMethodId = v.PaymentMethodId,
        PaymentMethodName = v.PaymentMethod?.Name,
        Note = v.Note,
        Freight = v.Freight,
        OrderDiscount = v.OrderDiscount,
        GoodsAmount = v.GoodsAmount,
        LineDiscountTotal = v.LineDiscountTotal,
        DiscountTotal = v.LineDiscountTotal + v.OrderDiscount,
        VatTotal = v.VatTotal,
        Total = v.Total,
        PaidAmount = v.PaidAmount,
        Status = v.Status,
        CancelledAt = v.CancelledAt,
        CancelledBy = v.CancelledBy,
        OwnerUserId = v.OwnerUserId,
        OwnerName = ownerName,
        CreatedAt = v.CreatedAt,
        Version = v.Version,
        Lines = v.Lines.OrderBy(l => l.SortOrder).Select(l => new StockVoucherLineDto
        {
            Id = l.Id,
            SortOrder = l.SortOrder,
            ProductId = l.ProductId,
            ProductCode = l.ProductCode,
            ProductName = l.ProductName,
            WarehouseId = l.WarehouseId,
            WarehouseCode = l.Warehouse?.Code,
            TrackInventory = l.TrackInventory,
            PricingMode = l.PricingMode,
            UnitName = l.UnitName,
            PriceIncludesVat = l.PriceIncludesVat,
            SheetCount = l.SheetCount,
            Length = l.Length,
            Width = l.Width,
            Thickness = l.Thickness,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            Amount = l.Amount,
            DiscountRate = l.DiscountRate,
            DiscountAmount = l.DiscountAmount,
            DiscountManual = l.DiscountManual,
            OrderDiscountAllocated = l.OrderDiscountAllocated,
            FreightAllocated = l.FreightAllocated,
            VatRate = l.VatRate,
            VatAmount = l.VatAmount,
            NetAmount = l.NetAmount,
            Note = l.Note,
        }).ToList(),
    };
}
