using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Application.Organization.SampleData.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Organization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Services;

public sealed class DemoBranchService : IDemoBranchService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _inventoryLock;

    public DemoBranchService(IAppDbContext db, IDateTime clock, ITransactionRunner transaction,
        IInventoryLock inventoryLock)
    {
        _db = db;
        _clock = clock;
        _transaction = transaction;
        _inventoryLock = inventoryLock;
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
            // 0. The exclusive gate waits for in-flight writes of the branch and keeps new ones out (D30); the guards
            // are read behind it.
            await _inventoryLock.AcquireBranchGateAsync(new[] { branchId }, exclusive: true, c);
            await LoadDeletableBranchAsync(branchId, c);

            // Product keys are valid for every branch: the real products whose cost price follows the branch's
            // stock-ins, and the sample catalog.
            var realStockInProducts = await _db.StockVoucherLines.IgnoreQueryFilters()
                .Where(l => l.StockVoucher!.BranchId == branchId && l.StockVoucher.Type == StockDirection.In
                            && !l.Product!.IsSample)
                .Select(l => l.ProductId)
                .Distinct()
                .ToListAsync(c);
            var sampleProducts = await _db.Products.IgnoreQueryFilters()
                .Where(p => p.IsSample)
                .Select(p => p.Id)
                .ToListAsync(c);
            await _inventoryLock.AcquireProductsAsync(realStockInProducts.Union(sampleProducts), c);

            var warehouses = _db.Warehouses.IgnoreQueryFilters().Where(w => w.BranchId == branchId).Select(w => w.Id);
            if (await _db.StockVouchers.IgnoreQueryFilters()
                    .AnyAsync(v => v.BranchId != branchId && warehouses.Contains(v.WarehouseId), c)
                || await _db.StockVoucherLines.IgnoreQueryFilters()
                    .AnyAsync(l => l.StockVoucher!.BranchId != branchId && warehouses.Contains(l.WarehouseId), c)
                || await _db.OpeningStocks.IgnoreQueryFilters()
                    .AnyAsync(o => o.BranchId != branchId && warehouses.Contains(o.WarehouseId), c))
                throw new ConflictException("DEMO_WAREHOUSE_IN_USE", "Kho của chi nhánh demo đang được chi nhánh khác dùng.");

            // Id sets as subqueries, read by each statement before its parents go. Every foreign key is Restrict, so
            // children are deleted before their parents.
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

            // 14. Users (soft-deleted ones too) move to the main branch.
            var now = _clock.UtcNow;
            var reassignedUserCount = await _db.Users.IgnoreQueryFilters()
                .Where(u => u.DefaultBranchId == branchId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.DefaultBranchId, BranchDefaults.MainBranchId)
                    .SetProperty(u => u.UpdatedAt, (DateTimeOffset?)now), c);

            // 15. The branch itself.
            await _db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).ExecuteDeleteAsync(c);

            return new DemoBranchDeletionResultDto { ReassignedUserCount = reassignedUserCount };
        }, ct);

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
