using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

public sealed class DemoBranchService : IDemoBranchService
{
    private const string SampleGroupCode = "DM";
    private const string SampleGroupName = "Dữ liệu mẫu";
    // 23503 foreign_key_violation; 23001 restrict_violation, which a RESTRICT foreign key raises when its parent goes.
    private static readonly string[] ForeignKeyViolations = { "23503", "23001" };

    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;
    private readonly ILogger<DemoBranchService> _logger;

    public DemoBranchService(IAppDbContext db, IDateTime clock, ICurrentUser currentUser, ITransactionRunner transaction,
        IInventoryLock inventoryLock, ILogger<DemoBranchService> logger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
        _logger = logger;
    }

    public async Task<DemoBranchDeletionPreviewDto> PreviewDeletionAsync(Guid branchId, CancellationToken ct = default)
    {
        await LoadDeletableBranchAsync(branchId, ct);

        // Automatic, cancelled and soft-deleted vouchers count too: all of them go with the branch.
        var documentCount =
            await _db.StockVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct)
            + await _db.CashVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct)
            + await _db.FundTransfers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct)
            + await _db.DebtOffsetVouchers.IgnoreQueryFilters().CountAsync(v => v.BranchId == branchId, ct);
        var reassignedUserCount = await _db.Users.IgnoreQueryFilters()
            .CountAsync(u => u.DefaultBranchId == branchId, ct);
        return new DemoBranchDeletionPreviewDto
        {
            DocumentCount = documentCount,
            ReassignedUserCount = reassignedUserCount,
        };
    }

    /// Hard-deletes a demo branch with every row that belongs to it (the documented exception to soft delete, S14).
    /// Bulk statements that ignore the query filters, so soft-deleted rows go too.
    public Task<DemoBranchDeletionResultDto> DeleteAsync(Guid branchId, CancellationToken ct = default) =>
        _transaction.RunAsync(async c =>
        {
            // 0. The exclusive gate, the guards and the product locks.
            var stockInProducts = await AcquireLocksAsync(branchId, c);
            await EnsureWarehousesNotSharedAsync(branchId, c);

            // 1–13. Every row of the branch.
            await DeleteBranchRowsAsync(branchId, c);

            // 14. Users (soft-deleted ones too) move to the main branch.
            var reassignedUserCount = await ReassignUsersAsync(branchId, c);

            // 15. The branch. A write that does not take the branch gate (a new fund, warehouse, credit limit or a
            // user's default branch) may have landed since its table was cleared: the only place a race hits here, so
            // the only foreign key error mapped to "busy". Nothing is deleted: the transaction rolls back.
            await MapBusyAsync(() => _db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).ExecuteDeleteAsync(c));

            // 16. The products priced by the branch's stock-ins follow their latest remaining stock-in.
            await ProductCostPrices.RecomputeAsync(_db, stockInProducts, c);
            await _db.SaveChangesAsync(c);

            // 17. The last demo branch takes the sample catalog it no longer needs along (soft-deleted branches do
            // not count). The rows still used by something stay, and are counted.
            var keptProducts = 0;
            var keptPartners = 0;
            if (!await _db.Branches.AnyAsync(b => b.IsDemo, c))
                (keptProducts, keptPartners) = await CleanSampleCatalogAsync(c);

            return new DemoBranchDeletionResultDto
            {
                ReassignedUserCount = reassignedUserCount,
                KeptSampleProductCount = keptProducts,
                KeptSamplePartnerCount = keptPartners,
            };
        }, ct);

    /// Runs a statement a concurrent write can make fail on a foreign key: logs the original error and maps it to the
    /// retryable 409. A foreign key error anywhere else in the delete is a bug and surfaces as it is.
    private async Task MapBusyAsync(Func<Task> statement)
    {
        try
        {
            await statement();
        }
        catch (Exception ex) when (IsForeignKeyViolation(ex))
        {
            var pg = ex as PostgresException ?? ex.InnerException as PostgresException;
            _logger.LogWarning(ex, "Demo branch delete hit a foreign key (constraint {Constraint}, table {Table}): {Message}",
                pg?.ConstraintName, pg?.TableName, ex.Message);
            throw new ConflictException("DEMO_BRANCH_BUSY", "Chi nhánh demo vừa có dữ liệu mới. Hãy thử xóa lại.");
        }
    }

    /// The exclusive gate waits for in-flight writes of the branch and keeps new ones out (D30); the guards are read
    /// behind it. Locks the products (keys are valid for every branch): the sample catalog and the products with a
    /// stock-in in the branch, which are returned for the cost price recomputation.
    private async Task<List<Guid>> AcquireLocksAsync(Guid branchId, CancellationToken ct)
    {
        await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: true, ct);
        await LoadDeletableBranchAsync(branchId, ct);

        var stockInProducts = await _db.StockVoucherLines.IgnoreQueryFilters()
            .Where(l => l.StockVoucher!.BranchId == branchId && l.StockVoucher.Type == StockDirection.In)
            .Select(l => l.ProductId)
            .Distinct()
            .ToListAsync(ct);
        var sampleProducts = await _db.Products.IgnoreQueryFilters()
            .Where(p => p.IsSample)
            .Select(p => p.Id)
            .ToListAsync(ct);
        await _inventoryLock.AcquireProductsAsync(stockInProducts.Union(sampleProducts), ct);
        return stockInProducts;
    }

    private async Task EnsureWarehousesNotSharedAsync(Guid branchId, CancellationToken ct)
    {
        var warehouses = _db.Warehouses.IgnoreQueryFilters().Where(w => w.BranchId == branchId).Select(w => w.Id);
        if (await _db.StockVouchers.IgnoreQueryFilters()
                .AnyAsync(v => v.BranchId != branchId && warehouses.Contains(v.WarehouseId), ct)
            || await _db.StockVoucherLines.IgnoreQueryFilters()
                .AnyAsync(l => l.StockVoucher!.BranchId != branchId && warehouses.Contains(l.WarehouseId), ct)
            || await _db.OpeningStocks.IgnoreQueryFilters()
                .AnyAsync(o => o.BranchId != branchId && warehouses.Contains(o.WarehouseId), ct))
            throw new ConflictException("DEMO_WAREHOUSE_IN_USE", "Kho của chi nhánh demo đang được chi nhánh khác dùng.");
    }

    /// Steps 1–13: every row that belongs to the branch, children before parents (every foreign key is Restrict). The
    /// id sets are subqueries, read by each statement before its parents go.
    private async Task DeleteBranchRowsAsync(Guid branchId, CancellationToken c)
    {
        var entries = _db.DebtEntries.IgnoreQueryFilters().Where(e => e.BranchId == branchId).Select(e => e.Id);
        var cashVouchers = _db.CashVouchers.IgnoreQueryFilters().Where(v => v.BranchId == branchId).Select(v => v.Id);
        var fundTransfers = _db.FundTransfers.IgnoreQueryFilters().Where(t => t.BranchId == branchId).Select(t => t.Id);
        var debtOffsets = _db.DebtOffsetVouchers.IgnoreQueryFilters()
            .Where(o => o.BranchId == branchId).Select(o => o.Id);
        var stockVouchers = _db.StockVouchers.IgnoreQueryFilters().Where(v => v.BranchId == branchId).Select(v => v.Id);

        // 1–2. Settlements, then the debt entries they join.
        await _db.DebtSettlements.IgnoreQueryFilters()
            .Where(s => entries.Contains(s.IncreaseEntryId) || entries.Contains(s.DecreaseEntryId))
            .ExecuteDeleteAsync(c);
        await _db.DebtEntries.IgnoreQueryFilters().Where(e => e.BranchId == branchId).ExecuteDeleteAsync(c);

        // 3–6. Cash documents after their activities (no foreign key) and lines. Cash vouchers go before the stock
        // vouchers that automatic ones follow (source_stock_voucher_id).
        await _db.CashDocumentActivities.IgnoreQueryFilters()
            .Where(a => cashVouchers.Contains(a.DocumentId) || fundTransfers.Contains(a.DocumentId)
                        || debtOffsets.Contains(a.DocumentId))
            .ExecuteDeleteAsync(c);
        await _db.CashVoucherLines.IgnoreQueryFilters()
            .Where(l => cashVouchers.Contains(l.CashVoucherId))
            .ExecuteDeleteAsync(c);
        await _db.CashVouchers.IgnoreQueryFilters().Where(v => v.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.FundTransfers.IgnoreQueryFilters().Where(t => t.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.DebtOffsetVouchers.IgnoreQueryFilters().Where(o => o.BranchId == branchId).ExecuteDeleteAsync(c);

        // 7–8. Stock vouchers after their activities and lines.
        await _db.StockVoucherActivities.IgnoreQueryFilters()
            .Where(a => stockVouchers.Contains(a.StockVoucherId))
            .ExecuteDeleteAsync(c);
        await _db.StockVoucherLines.IgnoreQueryFilters()
            .Where(l => stockVouchers.Contains(l.StockVoucherId))
            .ExecuteDeleteAsync(c);
        await _db.StockVouchers.IgnoreQueryFilters().Where(v => v.BranchId == branchId).ExecuteDeleteAsync(c);

        // 9. Derived stock data (no foreign keys).
        await _db.InventoryLedger.IgnoreQueryFilters().Where(e => e.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.InventoryCostPeriods.IgnoreQueryFilters().Where(p => p.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.StockBalances.IgnoreQueryFilters().Where(b => b.BranchId == branchId).ExecuteDeleteAsync(c);

        // 10. Opening data and credit limits.
        await _db.OpeningStocks.IgnoreQueryFilters().Where(o => o.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.OpeningDebts.IgnoreQueryFilters().Where(o => o.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.CreditLimits.IgnoreQueryFilters().Where(l => l.BranchId == branchId).ExecuteDeleteAsync(c);

        // 11–12. Funds and warehouses, once no document or opening row uses them.
        await _db.Funds.IgnoreQueryFilters().Where(f => f.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.Warehouses.IgnoreQueryFilters().Where(w => w.BranchId == branchId).ExecuteDeleteAsync(c);

        // 13. Numbering.
        await _db.DocumentCounters.IgnoreQueryFilters().Where(n => n.BranchId == branchId).ExecuteDeleteAsync(c);
        await _db.DocumentNumberings.IgnoreQueryFilters().Where(n => n.BranchId == branchId).ExecuteDeleteAsync(c);
    }

    private Task<int> ReassignUsersAsync(Guid branchId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var userId = _currentUser.UserId;
        return _db.Users.IgnoreQueryFilters()
            .Where(u => u.DefaultBranchId == branchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.DefaultBranchId, BranchDefaults.MainBranchId)
                .SetProperty(u => u.UpdatedAt, (DateTimeOffset?)now)
                .SetProperty(u => u.UpdatedBy, userId), ct);
    }

    /// Hard-deletes the sample products and partners nothing refers to, then the sample group when no product is
    /// left in it. Returns how many sample products and partners remain. Every check ignores the query filters.
    private async Task<(int KeptProducts, int KeptPartners)> CleanSampleCatalogAsync(CancellationToken ct)
    {
        await _db.Products.IgnoreQueryFilters()
            .Where(p => p.IsSample
                        && !_db.StockVoucherLines.IgnoreQueryFilters().Any(x => x.ProductId == p.Id)
                        && !_db.OpeningStocks.IgnoreQueryFilters().Any(x => x.ProductId == p.Id)
                        && !_db.QuotationLines.IgnoreQueryFilters().Any(x => x.ProductId == p.Id)
                        && !_db.InventoryLedger.IgnoreQueryFilters().Any(x => x.ProductId == p.Id)
                        && !_db.StockBalances.IgnoreQueryFilters().Any(x => x.ProductId == p.Id)
                        && !_db.InventoryCostPeriods.IgnoreQueryFilters().Any(x => x.ProductId == p.Id))
            .ExecuteDeleteAsync(ct);

        // The partners nothing refers to; their addresses belong to them alone and go first.
        var unusedPartnerIds = await _db.Customers.IgnoreQueryFilters()
            .Where(p => p.IsSample
                        && !_db.StockVouchers.IgnoreQueryFilters().Any(x => x.PartnerId == p.Id)
                        && !_db.CashVouchers.IgnoreQueryFilters().Any(x => x.PartnerId == p.Id)
                        && !_db.DebtEntries.IgnoreQueryFilters().Any(x => x.PartnerId == p.Id)
                        && !_db.DebtOffsetVouchers.IgnoreQueryFilters().Any(x => x.PartnerId == p.Id)
                        && !_db.OpeningDebts.IgnoreQueryFilters().Any(x => x.PartnerId == p.Id)
                        && !_db.CreditLimits.IgnoreQueryFilters().Any(x => x.CustomerId == p.Id)
                        && !_db.Quotations.IgnoreQueryFilters().Any(x => x.CustomerId == p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        await _db.CustomerAddresses.IgnoreQueryFilters()
            .Where(a => unusedPartnerIds.Contains(a.CustomerId))
            .ExecuteDeleteAsync(ct);
        // A real-branch document naming one of these partners since the list was read (it takes no branch gate) fails
        // here: the same retryable 409.
        await MapBusyAsync(() => _db.Customers.IgnoreQueryFilters()
            .Where(p => unusedPartnerIds.Contains(p.Id))
            .ExecuteDeleteAsync(ct));

        // Only the sample group: a real group may use the code DM too.
        await _db.ProductGroups.IgnoreQueryFilters()
            .Where(g => g.Code == SampleGroupCode && g.Name == SampleGroupName
                        && !_db.Products.IgnoreQueryFilters().Any(p => p.ProductGroupId == g.Id))
            .ExecuteDeleteAsync(ct);

        return (await _db.Products.IgnoreQueryFilters().CountAsync(p => p.IsSample, ct),
            await _db.Customers.IgnoreQueryFilters().CountAsync(p => p.IsSample, ct));
    }

    /// A PostgreSQL foreign key violation (23503 or 23001), thrown by a bulk statement directly or wrapped by a save.
    private static bool IsForeignKeyViolation(Exception ex) =>
        (ex as DbException ?? ex.InnerException as DbException)?.SqlState is { } state && ForeignKeyViolations.Contains(state);

    /// The guards of the preview and the delete: the branch exists, is a demo one and is not being filled. Not tracked,
    /// so a read behind a lock sees the current row, not an instance the scope already holds.
    private async Task<Branch> LoadDeletableBranchAsync(Guid branchId, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, ct)
            ?? throw new NotFoundException(nameof(Branch), branchId);
        if (!branch.IsDemo)
            throw new ConflictException("NOT_A_DEMO_BRANCH", "Chỉ xóa được chi nhánh demo bằng chức năng này.");
        if (branch.SampleDataStatus == SampleDataStatus.Running)
            throw new ConflictException("SAMPLE_DATA_RUNNING", "Dữ liệu mẫu đang được tạo. Hãy chờ xong rồi xóa.");
        return branch;
    }
}
