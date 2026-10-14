using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// Inventory of the executive dashboard, read from the stock ledger and the current quantities the stock-on-hand report
/// reads. The stock is valued now: every ledger row counts, whatever the period, and the value of a product in a
/// warehouse is what came in less what went out at cost. Values need inventory.view_cost; counts are always shown.
public class InventoryMetricsService : IInventoryMetricsService
{
    private const int NegativeMaxRows = 50;
    private const int TopProductCount = 10;

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly ExecutiveLookups _lookups;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public InventoryMetricsService(IAppDbContext db, ICurrentUser currentUser, IExecutiveScopeResolver scopes,
        ExecutiveLookups lookups, IDateTime clock, ITransactionRunner transaction)
    {
        _db = db;
        _currentUser = currentUser;
        _scopes = scopes;
        _lookups = lookups;
        _clock = clock;
        _transaction = transaction;
    }

    public Task<InventorySummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildSummaryAsync(query, c), ct);

    private async Task<InventorySummaryDto> BuildSummaryAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);
        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);

        var balances = await LoadBalancesAsync(scope, ct);
        var inStock = balances.Where(b => b.Quantity != 0m).ToList();
        var summary = new InventorySummaryDto
        {
            From = period.From,
            To = period.To,
            CanViewCost = canViewCost,
            ProductCount = inStock.Select(b => b.ProductId).Distinct().Count(),
            WarehouseCount = inStock.Select(b => b.WarehouseId).Distinct().Count(),
            NegativeCount = BelowZero(balances).Count(),
        };
        if (!canViewCost)
            return summary;

        // Values are read only when the user may see them.
        var pairs = await LoadPairsAsync(scope, ct);
        var (inValue, outCost) = await LoadPeriodMovementAsync(scope, period, ct);
        summary.StockValue = pairs.Sum(p => p.Value);
        summary.InValue = inValue;
        summary.OutCost = outCost;
        return summary;
    }

    public Task<InventoryBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildBreakdownAsync(query, c), ct);

    private async Task<InventoryBreakdownDto> BuildBreakdownAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);
        var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);

        var pairs = await LoadPairsAsync(scope, ct);
        var balances = await LoadBalancesAsync(scope, ct);
        var warehouses = await LoadWarehousesAsync(
            pairs.Select(p => p.WarehouseId).Concat(balances.Select(b => b.WarehouseId)).Distinct().ToList(), ct);
        var products = await LoadProductsAsync(
            pairs.Select(p => p.ProductId).Concat(balances.Select(b => b.ProductId)).Distinct().ToList(), ct);
        var branchCodes = await _lookups.BranchCodesAsync(scope.BranchIds, ct);

        // Per product over the whole scope: the value from the ledger, the quantity now from the balances.
        var productValues = pairs.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
        var productQuantities = balances.GroupBy(b => b.ProductId).ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));

        return new InventoryBreakdownDto
        {
            CanViewCost = canViewCost,
            ByWarehouse = WarehouseRows(ValueByWarehouse(pairs, warehouses, settings.CostingScope), balances, warehouses,
                branchCodes, canViewCost),
            ByProductGroup = await ProductGroupRowsAsync(products, productValues, productQuantities, canViewCost, ct),
            Negative = NegativeRows(balances, products, warehouses, branchCodes),
            TopByValue = canViewCost
                ? TopProductRows(products, productValues, productQuantities)
                : new List<StockProductRowDto>(),
        };
    }

    /// The value each warehouse holds. Under the branch costing scope a product has one cost for the whole branch, so
    /// its value is shared out over its warehouses by quantity, in warehouse code order, as the stock-on-hand report
    /// does; under the warehouse scope each warehouse keeps the value of its own ledger rows.
    private static Dictionary<Guid, decimal> ValueByWarehouse(IReadOnlyCollection<Pair> pairs,
        IReadOnlyDictionary<Guid, WarehouseInfo> warehouses, CostingScope costingScope)
    {
        switch (costingScope)
        {
            case CostingScope.Warehouse:
                return pairs.GroupBy(p => p.WarehouseId).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
            case CostingScope.Branch:
                var values = new Dictionary<Guid, decimal>();
                foreach (var product in pairs.GroupBy(p => (p.BranchId, p.ProductId)))
                {
                    var ordered = product
                        .OrderBy(p => warehouses.TryGetValue(p.WarehouseId, out var w) ? w.Code : string.Empty,
                            StringComparer.Ordinal)
                        .ThenBy(p => p.WarehouseId)
                        .ToList();
                    var split = StockValueSplit.ByQuantity(ordered.Sum(p => p.Value),
                        ordered.Select(p => p.Quantity).ToList());
                    for (var i = 0; i < ordered.Count; i++)
                        values[ordered[i].WarehouseId] = values.GetValueOrDefault(ordered[i].WarehouseId) + split[i];
                }

                return values;
            default:
                throw new ArgumentOutOfRangeException(nameof(costingScope), costingScope, "Unknown costing scope.");
        }
    }

    /// One row per warehouse that holds stock or value. Without inventory.view_cost the value is hidden before the
    /// empty rows are dropped, so a row never shows that a warehouse without stock still carries a value.
    private static List<StockGroupDto> WarehouseRows(IReadOnlyDictionary<Guid, decimal> values,
        IReadOnlyCollection<BalanceRow> balances, IReadOnlyDictionary<Guid, WarehouseInfo> warehouses,
        IReadOnlyDictionary<Guid, string> branchCodes, bool canViewCost)
    {
        var productCounts = balances.Where(b => b.Quantity != 0m)
            .GroupBy(b => b.WarehouseId)
            .ToDictionary(g => g.Key, g => g.Count());
        return values.Keys.Concat(productCounts.Keys).Distinct()
            .Select(id =>
            {
                warehouses.TryGetValue(id, out var warehouse);
                return new StockGroupDto
                {
                    Id = id,
                    Code = warehouse?.Code,
                    Name = warehouse?.Name,
                    BranchCode = warehouse is not null && branchCodes.TryGetValue(warehouse.BranchId, out var code)
                        ? code
                        : null,
                    ProductCount = productCounts.GetValueOrDefault(id),
                    Value = canViewCost ? values.GetValueOrDefault(id) : null,
                };
            })
            .Where(r => r.ProductCount != 0 || (r.Value ?? 0m) != 0m)
            .OrderBy(r => r.BranchCode, StringComparer.Ordinal)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .ThenBy(r => r.Id)
            .ToList();
    }

    /// The products by group; products without a group make one row without an id or a name. With values the largest
    /// group comes first, without them the group with the most products in stock.
    private async Task<List<StockGroupDto>> ProductGroupRowsAsync(IReadOnlyDictionary<Guid, ProductInfo> products,
        IReadOnlyDictionary<Guid, decimal> productValues, IReadOnlyDictionary<Guid, decimal> productQuantities,
        bool canViewCost, CancellationToken ct)
    {
        var groupNames = await LoadGroupNamesAsync(
            products.Values.Where(p => p.GroupId is not null).Select(p => p.GroupId!.Value).Distinct().ToList(), ct);
        var rows = products
            .GroupBy(p => p.Value.GroupId)
            .Select(g => new StockGroupDto
            {
                Id = g.Key,
                Name = g.Key is { } groupId && groupNames.TryGetValue(groupId, out var name) ? name : null,
                ProductCount = g.Count(p => productQuantities.GetValueOrDefault(p.Key) != 0m),
                Value = canViewCost ? g.Sum(p => productValues.GetValueOrDefault(p.Key)) : null,
            });
        var ordered = canViewCost
            ? rows.OrderByDescending(r => r.Value)
            : rows.OrderByDescending(r => r.ProductCount);
        return ordered.ThenBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id).ToList();
    }

    /// The product × warehouse pairs below zero, at most 50.
    private static List<NegativeStockRowDto> NegativeRows(IEnumerable<BalanceRow> balances,
        IReadOnlyDictionary<Guid, ProductInfo> products, IReadOnlyDictionary<Guid, WarehouseInfo> warehouses,
        IReadOnlyDictionary<Guid, string> branchCodes) =>
        BelowZero(balances)
            .Select(b =>
            {
                products.TryGetValue(b.ProductId, out var product);
                warehouses.TryGetValue(b.WarehouseId, out var warehouse);
                return new NegativeStockRowDto
                {
                    ProductId = b.ProductId,
                    ProductCode = product?.Code ?? string.Empty,
                    ProductName = product?.Name ?? string.Empty,
                    UnitName = product?.UnitName,
                    WarehouseId = b.WarehouseId,
                    WarehouseCode = warehouse?.Code ?? string.Empty,
                    BranchCode = branchCodes.TryGetValue(b.BranchId, out var code) ? code : string.Empty,
                    Quantity = b.Quantity,
                };
            })
            .OrderBy(r => r.BranchCode, StringComparer.Ordinal)
            .ThenBy(r => r.WarehouseCode, StringComparer.Ordinal)
            .ThenBy(r => r.ProductCode, StringComparer.Ordinal)
            .ThenBy(r => r.WarehouseId)
            .ThenBy(r => r.ProductId)
            .Take(NegativeMaxRows)
            .ToList();

    /// The products worth the most over the scope, the first ten.
    private static List<StockProductRowDto> TopProductRows(IReadOnlyDictionary<Guid, ProductInfo> products,
        IReadOnlyDictionary<Guid, decimal> productValues, IReadOnlyDictionary<Guid, decimal> productQuantities) =>
        products
            .Select(p => new StockProductRowDto
            {
                ProductId = p.Key,
                Code = p.Value.Code,
                Name = p.Value.Name,
                UnitName = p.Value.UnitName,
                Quantity = productQuantities.GetValueOrDefault(p.Key),
                Value = productValues.GetValueOrDefault(p.Key),
            })
            .OrderByDescending(r => r.Value)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .ThenBy(r => r.ProductId)
            .Take(TopProductCount)
            .ToList();

    /// Code, name and branch of each warehouse; a deleted warehouse keeps them (a lookup by id with the filters off).
    private Task<Dictionary<Guid, WarehouseInfo>> LoadWarehousesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, w.Code, w.Name, w.BranchId })
            .ToDictionaryAsync(w => w.Id, w => new WarehouseInfo(w.Code, w.Name, w.BranchId), ct);

    /// Code and name of each product (from the shared lookups), with its group and the unit its stock is counted in.
    /// A deleted product keeps them.
    private async Task<Dictionary<Guid, ProductInfo>> LoadProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var names = await _lookups.ProductsAsync(ids, ct);
        var details = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductGroupId, p.PricingMode, UnitName = p.Unit != null ? p.Unit.Name : null })
            .ToListAsync(ct);
        return details.ToDictionary(p => p.Id, p =>
        {
            var (code, name) = names.TryGetValue(p.Id, out var known) ? known : (string.Empty, string.Empty);
            return new ProductInfo(code, name, p.ProductGroupId, StockUnit.NameFor(p.PricingMode, p.UnitName));
        });
    }

    /// The name of each product group; a deleted group keeps it.
    private Task<Dictionary<Guid, string>> LoadGroupNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        _db.ProductGroups.IgnoreQueryFilters().AsNoTracking()
            .Where(g => ids.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

    /// The stock value now of each branch of the scope. Runs inside the caller's snapshot.
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetValueByBranchAsync(ExecutiveScope scope,
        CancellationToken ct = default) =>
        (await LoadPairsAsync(scope, ct)).GroupBy(p => p.BranchId).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));

    /// The number of product × warehouse pairs of the scope below zero now. Runs inside the caller's snapshot.
    public async Task<int> CountNegativeStockAsync(ExecutiveScope scope, CancellationToken ct = default) =>
        BelowZero(await LoadBalancesAsync(scope, ct)).Count();

    /// The quantities now (kept up to date on every ledger change) of every product and warehouse of the scope.
    private async Task<List<BalanceRow>> LoadBalancesAsync(ExecutiveScope scope, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        return await _db.StockBalances.AsNoTracking()
            .Where(b => branchIds.Contains(b.BranchId))
            .Select(b => new BalanceRow(b.BranchId, b.ProductId, b.WarehouseId, b.Quantity))
            .ToListAsync(ct);
    }

    /// The ledger of the scope summed by branch, product and warehouse: the quantity and the value. No date filter: the
    /// stock is valued now.
    private async Task<List<Pair>> LoadPairsAsync(ExecutiveScope scope, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        return await _db.InventoryLedger.AsNoTracking()
            .Where(e => branchIds.Contains(e.BranchId))
            .GroupBy(e => new { e.BranchId, e.ProductId, e.WarehouseId })
            .Select(g => new Pair(g.Key.BranchId, g.Key.ProductId, g.Key.WarehouseId,
                g.Sum(e => e.QtyIn - e.QtyOut), g.Sum(e => e.InValue - (e.CostAmount ?? 0m))))
            .ToListAsync(ct);
    }

    /// The value received by stock-in vouchers and the cost issued by stock-out vouchers in the period. Opening stock is
    /// neither: nothing was bought or sold to bring it in.
    private async Task<(decimal InValue, decimal OutCost)> LoadPeriodMovementAsync(
        ExecutiveScope scope, ExecutivePeriod period, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        var start = period.Start;
        var end = period.End;
        var totals = await _db.InventoryLedger.AsNoTracking()
            .Where(e => branchIds.Contains(e.BranchId) && e.PostedAt >= start && e.PostedAt < end)
            .GroupBy(e => e.SourceType)
            .Select(g => new { SourceType = g.Key, InValue = g.Sum(e => e.InValue), Cost = g.Sum(e => e.CostAmount ?? 0m) })
            .ToListAsync(ct);
        return (totals.Where(t => t.SourceType == LedgerSourceType.StockIn).Sum(t => t.InValue),
            totals.Where(t => t.SourceType == LedgerSourceType.StockOut).Sum(t => t.Cost));
    }

    /// The product × warehouse pairs below zero.
    private static IEnumerable<BalanceRow> BelowZero(IEnumerable<BalanceRow> balances) =>
        balances.Where(b => b.Quantity < 0m);

    private sealed record WarehouseInfo(string Code, string Name, Guid BranchId);

    private sealed record ProductInfo(string Code, string Name, Guid? GroupId, string UnitName);

    private sealed record BalanceRow(Guid BranchId, Guid ProductId, Guid WarehouseId, decimal Quantity);

    /// Quantity and Value come from the ledger.
    private sealed record Pair(Guid BranchId, Guid ProductId, Guid WarehouseId, decimal Quantity, decimal Value);
}
