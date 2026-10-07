using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Costing;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Posting;

public class InventoryPostingService : IInventoryPostingService
{
    private readonly IInventoryLock _lock;
    private readonly IInventoryLedgerService _ledger;
    private readonly IInventoryCostingService _costing;
    private readonly IAppDbContext _db;

    public InventoryPostingService(IInventoryLock inventoryLock, IInventoryLedgerService ledger,
        IInventoryCostingService costing, IAppDbContext db)
    {
        _lock = inventoryLock;
        _ledger = ledger;
        _costing = costing;
        _db = db;
    }

    public async Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        await _lock.AcquireBranchGateAsync(new[] { branchId }, exclusive: false, ct);
        // One branch-independent key per product: it covers the product's pairs in every branch and serializes
        // product-wide writes (D35 cost price, catalog guards), so no (product, branch) key is needed (review finding).
        await _lock.AcquireProductsAsync(productIds, ct);
    }

    public async Task<LedgerChangeResult> PostAsync(LedgerSourceType sourceType, Guid sourceId,
        IReadOnlyList<LedgerEntryDraft> entries, bool acknowledgeNegativeStock, CancellationToken ct = default)
    {
        var change = await _ledger.ReplaceSourceAsync(sourceType, sourceId, entries, ct);

        // Read after the locks are held (D30).
        var policy = (await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct)).NegativeStockPolicy;
        if (policy != NegativeStockPolicy.Allow)
        {
            var shortages = change.Pairs.Where(IsShortage).ToList();
            if (shortages.Count > 0 && (policy == NegativeStockPolicy.Block || !acknowledgeNegativeStock))
                throw new NegativeStockException(policy == NegativeStockPolicy.Warn, await DescribeAsync(shortages, ct));
        }

        await _costing.RecalculateAsync(await _costing.ScopesForAsync(change.Pairs, ct), ct);
        return change;
    }

    /// Only a change that makes the pair worse counts (D31): a new or deeper minimum, or an earlier first negative point.
    /// Both windows start from the quantity carried in from before From, so a receipt that only shrinks an existing
    /// deficit never counts (review finding).
    private static bool IsShortage(PairChange p)
    {
        var newMin = MinOf(p.MinRunningQty, p.BaseRunningQty);
        if (newMin is not < 0)
            return false;
        var oldMin = MinOf(p.OldMinRunningQty, p.BaseRunningQty);
        if (oldMin is null || newMin < oldMin)
            return true;
        // Already negative before the window: the first negative point lies before From and does not move.
        if (p.BaseRunningQty < 0)
            return false;
        return p.FirstNegativeMovedEarlier;
    }

    private static decimal? MinOf(decimal? a, decimal? b) =>
        a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    private async Task<IDictionary<string, string[]>> DescribeAsync(IReadOnlyList<PairChange> shortages, CancellationToken ct)
    {
        var productIds = shortages.Select(s => s.ProductId).Distinct().ToList();
        var warehouseIds = shortages.Select(s => s.WarehouseId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.PricingMode, UnitName = p.Unit != null ? p.Unit.Name : null })
            .ToDictionaryAsync(p => p.Id, ct);
        var warehouseCodes = await _db.Warehouses
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Code, ct);

        return shortages.ToDictionary(
            s => $"{products[s.ProductId].Code}@{warehouseCodes[s.WarehouseId]}",
            s =>
            {
                var product = products[s.ProductId];
                var unit = StockUnit.NameFor(product.PricingMode, product.UnitName);
                // The deepest point with its own time, not the time of the first negative row (review finding).
                var at = s.MinAt!.Value.ToOffset(VnTime.Offset);
                return new[]
                {
                    string.Create(CultureInfo.InvariantCulture,
                        $"Âm {Math.Abs(s.MinRunningQty!.Value):0.######} {unit} tại {at:dd/MM/yyyy HH:mm}"),
                };
            });
    }
}
