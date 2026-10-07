using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.Reports.Interfaces;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Reports.Services;

/// Inventory reports of the working branch. Values need inventory.view_cost and follow the costing scope (D32).
public class InventoryReportService : IInventoryReportService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;

    public InventoryReportService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
    }

    public async Task<StockOnHandReportDto> GetStockOnHandAsync(StockOnHandReportRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
        var at = request.At is { } requested ? VnTime.ToUtc(requested) : _clock.UtcNow;

        var ledger = _db.InventoryLedger.AsNoTracking().Where(e => e.BranchId == branchId);
        if (request.At.HasValue)
            ledger = ledger.Where(e => e.PostedAt <= at);
        var pairs = await ledger
            .GroupBy(e => new { e.ProductId, e.WarehouseId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseId,
                Qty = g.Sum(e => e.QtyIn - e.QtyOut),
                Value = g.Sum(e => e.InValue - (e.CostAmount ?? 0m)),
            })
            .ToListAsync(ct);

        // Current quantities come from StockBalances (D21); the `at` variant sums the ledger.
        var balances = request.At.HasValue
            ? null
            : (await _db.StockBalances.AsNoTracking().Where(b => b.BranchId == branchId).ToListAsync(ct))
                .ToDictionary(b => (b.ProductId, b.WarehouseId), b => b.Quantity);

        var productIds = pairs.Select(p => p.ProductId).Distinct().ToList();
        var products = _db.Products.IgnoreQueryFilters().AsNoTracking().Where(p => productIds.Contains(p.Id));
        if (request.ProductGroupId is { } groupId)
            products = products.Where(p => p.ProductGroupId == groupId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            products = products.Where(p => EF.Functions.ILike(p.Code, pattern) || EF.Functions.ILike(p.Name, pattern));
        }
        var productInfo = await products
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                GroupName = p.ProductGroup != null ? p.ProductGroup.Name : null,
                p.PricingMode,
                UnitName = p.Unit != null ? p.Unit.Name : null,
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var warehouseIds = pairs.Select(p => p.WarehouseId).Distinct().ToList();
        var warehouses = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Code, w.Name })
            .ToDictionaryAsync(w => w.Id, ct);

        var rows = new List<(StockOnHandRowDto Row, decimal Value)>();
        decimal totalValue = 0m;
        var branchScope = settings.CostingScope == CostingScope.Branch;
        foreach (var product in pairs.Where(p => productInfo.ContainsKey(p.ProductId)).GroupBy(p => p.ProductId))
        {
            var info = productInfo[product.Key];
            var ordered = product.OrderBy(p => warehouses[p.WarehouseId].Code, StringComparer.Ordinal).ToList();
            var values = branchScope
                ? SplitByQuantity(ordered.Sum(p => p.Value), ordered.Select(p => p.Qty).ToList())
                : ordered.Select(p => p.Value).ToList();
            if (branchScope && request.WarehouseId is null)
                totalValue += ordered.Sum(p => p.Value);

            for (var i = 0; i < ordered.Count; i++)
            {
                var pair = ordered[i];
                if (request.WarehouseId is { } warehouseId && pair.WarehouseId != warehouseId)
                    continue;
                if (!branchScope || request.WarehouseId is not null)
                    totalValue += values[i];

                var warehouse = warehouses[pair.WarehouseId];
                rows.Add((new StockOnHandRowDto
                {
                    ProductId = info.Id,
                    ProductCode = info.Code,
                    ProductName = info.Name,
                    ProductGroupName = info.GroupName,
                    UnitName = StockUnit.NameFor(info.PricingMode, info.UnitName),
                    WarehouseId = warehouse.Id,
                    WarehouseCode = warehouse.Code,
                    WarehouseName = warehouse.Name,
                    Quantity = balances is null ? pair.Qty : balances.GetValueOrDefault((pair.ProductId, pair.WarehouseId)),
                }, values[i]));
            }
        }

        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);
        return new StockOnHandReportDto
        {
            At = at,
            IsProvisional = IsProvisional(VnTime.ToVnDate(at), settings.CostingPeriod),
            CanViewCost = canViewCost,
            TotalValue = canViewCost ? totalValue : null,
            Rows = rows
                .Where(r => r.Row.Quantity != 0m || r.Value != 0m)
                .Select(r => { r.Row.Value = canViewCost ? r.Value : null; return r.Row; })
                .OrderBy(r => r.ProductCode, StringComparer.Ordinal)
                .ThenBy(r => r.WarehouseCode, StringComparer.Ordinal)
                .ToList(),
        };
    }

    public async Task<StockCardDto> GetStockCardAsync(StockCardRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var from = request.From!.Value;
        var to = request.To!.Value;
        var product = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.Code, p.Name, p.PricingMode, UnitName = p.Unit != null ? p.Unit.Name : null })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product", request.ProductId);
        if (request.WarehouseId is { } requestedWarehouseId)
        {
            var warehouseBranchId = await _db.Warehouses.AsNoTracking()
                .Where(w => w.Id == requestedWarehouseId)
                .Select(w => (Guid?)w.BranchId)
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Warehouse", requestedWarehouseId);
            if (warehouseBranchId != branchId)
                throw new ForbiddenException("Kho không thuộc chi nhánh đang làm việc.");
        }
        var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);

        var ledger = _db.InventoryLedger.AsNoTracking().Where(e => e.BranchId == branchId && e.ProductId == request.ProductId);
        if (request.WarehouseId is { } warehouseId)
            ledger = ledger.Where(e => e.WarehouseId == warehouseId);
        var start = VnTime.StartOfDay(from);
        var end = VnTime.StartOfNextDay(to);
        var before = ledger.Where(e => e.PostedAt < start);
        var openingQty = await before.SumAsync(e => e.QtyIn - e.QtyOut, ct);
        var openingValue = await before.SumAsync(e => e.InValue - (e.CostAmount ?? 0m), ct);
        var entries = await ledger.Where(e => e.PostedAt >= start && e.PostedAt < end).InPostingOrder().ToListAsync(ct);

        var voucherIds = entries.Where(e => e.SourceType != LedgerSourceType.Opening).Select(e => e.SourceId).Distinct().ToList();
        var vouchers = await _db.StockVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => voucherIds.Contains(v.Id))
            .Select(v => new { v.Id, ReasonName = v.Reason != null ? v.Reason.Name : null, v.PartnerName })
            .ToDictionaryAsync(v => v.Id, ct);
        var warehouseIds = entries.Select(e => e.WarehouseId).Distinct().ToList();
        var warehouseCodes = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Code, ct);

        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);
        // D32: under Branch scope a single warehouse has no value of its own.
        var valuesAtScopeOnly = request.WarehouseId is not null && settings.CostingScope == CostingScope.Branch;
        var showRunning = canViewCost && !valuesAtScopeOnly;

        var runningQty = openingQty;
        var runningValue = openingValue;
        var rows = new List<StockCardRowDto>(entries.Count);
        foreach (var e in entries)
        {
            runningQty += e.QtyIn - e.QtyOut;
            runningValue += e.InValue - (e.CostAmount ?? 0m);
            var opening = e.SourceType == LedgerSourceType.Opening;
            var voucher = opening ? null : vouchers.GetValueOrDefault(e.SourceId);
            var unitCost = e.QtyOut > 0m ? e.UnitCost : e.QtyIn > 0m ? R4(e.InValue / e.QtyIn) : (decimal?)null;
            rows.Add(new StockCardRowDto
            {
                PostedAt = e.PostedAt,
                SourceType = e.SourceType,
                SourceId = e.SourceId,
                SourceCode = e.SourceCode,
                ReasonName = opening ? "Tồn đầu kỳ" : voucher?.ReasonName,
                PartnerName = voucher?.PartnerName,
                WarehouseCode = warehouseCodes.GetValueOrDefault(e.WarehouseId) ?? "",
                QtyIn = e.QtyIn,
                QtyOut = e.QtyOut,
                UnitCost = canViewCost ? unitCost : null,
                InValue = canViewCost ? e.InValue : null,
                CostAmount = canViewCost ? e.CostAmount : null,
                RunningQty = runningQty,
                RunningValue = showRunning ? runningValue : null,
            });
        }

        var inQty = entries.Sum(e => e.QtyIn);
        var outQty = entries.Sum(e => e.QtyOut);
        var inValue = entries.Sum(e => e.InValue);
        var outValue = entries.Sum(e => e.CostAmount ?? 0m);
        return new StockCardDto
        {
            ProductId = product.Id,
            ProductCode = product.Code,
            ProductName = product.Name,
            UnitName = StockUnit.NameFor(product.PricingMode, product.UnitName),
            From = from,
            To = to,
            IsProvisional = IsProvisional(to, settings.CostingPeriod),
            CanViewCost = canViewCost,
            ValuesAtScopeOnly = valuesAtScopeOnly,
            OpeningQty = openingQty,
            OpeningValue = showRunning ? openingValue : null,
            InQty = inQty,
            OutQty = outQty,
            InValue = canViewCost ? inValue : null,
            OutValue = canViewCost ? outValue : null,
            ClosingQty = openingQty + inQty - outQty,
            ClosingValue = showRunning ? openingValue + inValue - outValue : null,
            Rows = rows,
        };
    }

    /// Branch scope (D32): each warehouse gets R0(scopeValue × qty / scopeQty); the rounding residual goes to the last
    /// warehouse (by code) holding stock. A zero scope quantity gives every warehouse 0.
    private static List<decimal> SplitByQuantity(decimal scopeValue, IReadOnlyList<decimal> quantities)
    {
        var scopeQty = quantities.Sum();
        if (scopeQty == 0m)
            return quantities.Select(_ => 0m).ToList();

        var values = quantities.Select(q => R0(scopeValue * q / scopeQty)).ToList();
        var last = quantities.Count - 1;
        while (quantities[last] == 0m)
            last--;
        values[last] += scopeValue - values.Sum();
        return values;
    }

    /// The costing period containing `date` has not ended yet (VN today).
    private bool IsProvisional(DateOnly date, CostingPeriod period) =>
        CostingPeriodCalendar.PeriodOf(date, period).End >= VnTime.ToVnDate(_clock.UtcNow);

    private static decimal R0(decimal x) => Math.Round(x, 0, MidpointRounding.AwayFromZero);

    private static decimal R4(decimal x) => Math.Round(x, 4, MidpointRounding.AwayFromZero);

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
