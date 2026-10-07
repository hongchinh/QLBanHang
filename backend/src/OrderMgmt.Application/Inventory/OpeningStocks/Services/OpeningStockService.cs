using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.OpeningStocks.Interfaces;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.OpeningStocks.Services;

/// Opening stock grid of a warehouse in the working branch, posted as Opening ledger rows (D20).
/// Values are part of inventory.opening_stock (D36), so the grid always carries Amount.
public class OpeningStockService : IOpeningStockService
{
    public const string SourceCode = "TDK";

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryPostingService _posting;

    public OpeningStockService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch,
        ITransactionRunner transaction, IInventoryPostingService posting)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _posting = posting;
    }

    public async Task<OpeningStockGridDto> GetAsync(Guid warehouseId, CancellationToken ct = default)
    {
        await EnsureWarehouseAsync(warehouseId, ct);

        var rows = await _db.OpeningStocks.AsNoTracking()
            .Where(o => o.WarehouseId == warehouseId)
            .OrderBy(o => o.Product!.Code)
            .Select(o => new
            {
                o.ProductId,
                o.Product!.Code,
                o.Product.Name,
                o.Product.PricingMode,
                UnitName = o.Product.Unit != null ? o.Product.Unit.Name : null,
                o.OpeningDate,
                o.Quantity,
                o.Amount,
            })
            .ToListAsync(ct);

        return new OpeningStockGridDto
        {
            WarehouseId = warehouseId,
            OpeningDate = rows.Count > 0 ? rows[0].OpeningDate : null,
            Lines = rows.Select(r => new OpeningStockLineDto
            {
                ProductId = r.ProductId,
                ProductCode = r.Code,
                ProductName = r.Name,
                UnitName = StockUnit.NameFor(r.PricingMode, r.UnitName),
                Quantity = r.Quantity,
                Amount = r.Amount,
            }).ToList(),
        };
    }

    public async Task<OpeningStockGridDto> SaveAsync(SaveOpeningStockRequest request, CancellationToken ct = default)
    {
        var branchId = await EnsureWarehouseAsync(request.WarehouseId, ct);

        await _transaction.RunAsync(async c =>
        {
            await ValidateProductsAsync(request, c);

            var lockedProductIds = (await _db.OpeningStocks.AsNoTracking()
                    .Where(o => o.WarehouseId == request.WarehouseId)
                    .Select(o => o.ProductId)
                    .ToListAsync(c))
                .Concat(request.Lines.Select(l => l.ProductId))
                .Distinct()
                .ToList();
            await _posting.AcquireLocksAsync(branchId, lockedProductIds, c);

            var existing = await _db.OpeningStocks.Where(o => o.WarehouseId == request.WarehouseId).ToListAsync(c);
            // A concurrent save added a product between the read and the locks: its pair is not locked.
            if (existing.Any(o => !lockedProductIds.Contains(o.ProductId)))
                throw new ConflictException("Tồn đầu kỳ vừa được người khác cập nhật. Vui lòng tải lại.");

            // Read after the shared branch gate, so a concurrent period lock change is serialized.
            var dates = existing.Select(o => o.OpeningDate).Distinct().ToList();
            if (request.Lines.Count > 0)
                dates.Add(request.OpeningDate);
            await EnsureNotLockedAsync(branchId, dates, c);

            var byProduct = existing.ToDictionary(o => o.ProductId);
            var rows = new List<OpeningStock>(request.Lines.Count);
            foreach (var line in request.Lines)
            {
                if (!byProduct.Remove(line.ProductId, out var row))
                {
                    // New child rows go through the DbSet (see plan "Child rows").
                    row = new OpeningStock { BranchId = branchId, WarehouseId = request.WarehouseId, ProductId = line.ProductId };
                    _db.OpeningStocks.Add(row);
                }
                row.OpeningDate = request.OpeningDate;
                row.Quantity = line.Quantity;
                row.Amount = line.Amount;
                rows.Add(row);
            }
            foreach (var removed in byProduct.Values)
            {
                removed.IsDeleted = true;
                removed.DeletedBy = _currentUser.UserId;
            }
            await _db.SaveChangesAsync(c);

            var postedAt = VnTime.StartOfDay(request.OpeningDate);
            var drafts = rows.Select((row, index) => new LedgerEntryDraft(
                    row.ProductId, row.WarehouseId, branchId, postedAt,
                    LedgerSourceType.Opening, request.WarehouseId, row.Id, SourceCode, index,
                    row.Quantity, 0m, row.Amount))
                .ToList();
            await _posting.PostAsync(LedgerSourceType.Opening, request.WarehouseId, drafts, request.AcknowledgeNegativeStock, c);
        }, ct);

        return await GetAsync(request.WarehouseId, ct);
    }

    /// The warehouse must exist and belong to the working branch; returns that branch.
    private async Task<Guid> EnsureWarehouseAsync(Guid warehouseId, CancellationToken ct)
    {
        var warehouseBranchId = await _db.Warehouses.AsNoTracking()
            .Where(w => w.Id == warehouseId)
            .Select(w => (Guid?)w.BranchId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Warehouse), warehouseId);
        var branchId = await _currentBranch.GetIdAsync(ct);
        if (warehouseBranchId != branchId)
            throw new ForbiddenException("Kho không thuộc chi nhánh đang làm việc.");
        return branchId;
    }

    /// Products are distinct, exist and are tracked.
    private async Task ValidateProductsAsync(SaveOpeningStockRequest request, CancellationToken ct)
    {
        var ids = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var tracked = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.TrackInventory, ct);

        var errors = new Dictionary<string, string[]>();
        var seen = new HashSet<Guid>();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var productId = request.Lines[i].ProductId;
            var error = !seen.Add(productId) ? "Hàng hóa bị trùng."
                : !tracked.TryGetValue(productId, out var track) ? "Hàng hóa không tồn tại."
                : !track ? "Hàng hóa không theo dõi tồn kho."
                : null;
            if (error is not null)
                errors[$"lines[{i}].productId"] = new[] { error };
        }
        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);
    }

    private async Task EnsureNotLockedAsync(Guid branchId, IEnumerable<DateOnly> dates, CancellationToken ct)
    {
        var lockedUntil = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.LockedUntil).SingleAsync(ct);
        if (lockedUntil is { } locked && dates.Any(d => d <= locked))
            throw new DomainException("PERIOD_LOCKED",
                string.Create(CultureInfo.InvariantCulture, $"Ngày tồn đầu kỳ đã khóa sổ (đến {locked:dd/MM/yyyy})."));
    }
}
