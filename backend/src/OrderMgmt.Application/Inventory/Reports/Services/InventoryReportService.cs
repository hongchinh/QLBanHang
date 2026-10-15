using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common;
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
    private readonly ITransactionRunner _transaction;

    public InventoryReportService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock,
        ITransactionRunner transaction)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
    }

    /// One REPEATABLE READ snapshot for all the report's queries (review finding: no read skew).
    public Task<StockOnHandReportDto> GetStockOnHandAsync(StockOnHandReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildStockOnHandAsync(request, c), ct);

    private async Task<StockOnHandReportDto> BuildStockOnHandAsync(StockOnHandReportRequest request, CancellationToken ct)
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
                ? StockValueSplit.ByQuantity(ordered.Sum(p => p.Value), ordered.Select(p => p.Qty).ToList())
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
                // Mask first, then drop: without view_cost a zero-quantity row never reveals a residual value.
                .Select(r => { r.Row.Value = canViewCost ? r.Value : null; return r.Row; })
                .Where(r => r.Quantity != 0m || (r.Value ?? 0m) != 0m)
                .OrderBy(r => r.ProductCode, StringComparer.Ordinal)
                .ThenBy(r => r.WarehouseCode, StringComparer.Ordinal)
                .ToList(),
        };
    }

    /// One REPEATABLE READ snapshot for all the report's queries (review finding: no read skew).
    public Task<StockCardDto> GetStockCardAsync(StockCardRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildStockCardAsync(request, c), ct);

    private async Task<StockCardDto> BuildStockCardAsync(StockCardRequest request, CancellationToken ct)
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

    /// One REPEATABLE READ snapshot for all the report's queries.
    public Task<StockSummaryReportDto> GetStockSummaryAsync(StockSummaryRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildStockSummaryAsync(request, c), ct);

    private async Task<StockSummaryReportDto> BuildStockSummaryAsync(StockSummaryRequest request, CancellationToken ct)
    {
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);
        var branchId = await _currentBranch.GetIdAsync(ct);
        var from = request.From!.Value;
        var to = request.To!.Value;
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
        // T19: a ledger row is "opening" when PostedAt < start and "in the period" when start <= PostedAt < end.
        var start = VnTime.StartOfDay(from);
        var end = VnTime.StartOfNextDay(to);

        var products = _db.Products.IgnoreQueryFilters().AsNoTracking().Where(p => p.TrackInventory);
        if (request.ProductGroupId is { } groupId)
            products = products.Where(p => p.ProductGroupId == groupId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{LikePattern.Escape(request.Search.Trim())}%";
            products = products.Where(p => EF.Functions.ILike(p.Code, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(p.Name, pattern, LikePattern.EscapeCharacter));
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
        var productIds = productInfo.Keys.ToList();

        var ledger = _db.InventoryLedger.AsNoTracking()
            .Where(e => e.PostedAt < end && productIds.Contains(e.ProductId));
        if (request.Scope == ReportScope.Branch)
        {
            ledger = ledger.Where(e => e.BranchId == branchId);
        }
        else
        {
            // S15: company scope skips demo branches, soft-deleted ones included.
            ledger = ledger.Where(e => !_db.Branches.IgnoreQueryFilters().Any(b => b.Id == e.BranchId && b.IsDemo));
        }
        if (request.WarehouseId is { } warehouseId)
            ledger = ledger.Where(e => e.WarehouseId == warehouseId);
        var sums = await ledger
            .GroupBy(e => e.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                OpeningQty = g.Sum(e => e.PostedAt < start ? e.QtyIn - e.QtyOut : 0m),
                OpeningValue = g.Sum(e => e.PostedAt < start ? e.InValue - (e.CostAmount ?? 0m) : 0m),
                InQty = g.Sum(e => e.PostedAt >= start ? e.QtyIn : 0m),
                InValue = g.Sum(e => e.PostedAt >= start ? e.InValue : 0m),
                OutQty = g.Sum(e => e.PostedAt >= start ? e.QtyOut : 0m),
                OutValue = g.Sum(e => e.PostedAt >= start ? e.CostAmount ?? 0m : 0m),
            })
            .ToListAsync(ct);

        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);
        // D32: under Branch costing a single warehouse has no value of its own.
        var valuesAtScopeOnly = request.WarehouseId is not null && settings.CostingScope == CostingScope.Branch;
        var showValues = canViewCost && !valuesAtScopeOnly;

        var rows = sums
            .Select(x =>
            {
                var info = productInfo[x.ProductId];
                // Mask first, then drop: without values a zero-quantity row never reveals a residual value.
                return new StockSummaryRowDto
                {
                    ProductId = info.Id,
                    ProductCode = info.Code,
                    ProductName = info.Name,
                    UnitName = StockUnit.NameFor(info.PricingMode, info.UnitName),
                    ProductGroupName = info.GroupName,
                    OpeningQty = x.OpeningQty,
                    OpeningValue = showValues ? x.OpeningValue : null,
                    InQty = x.InQty,
                    InValue = showValues ? x.InValue : null,
                    OutQty = x.OutQty,
                    OutValue = showValues ? x.OutValue : null,
                    ClosingQty = x.OpeningQty + x.InQty - x.OutQty,
                    ClosingValue = showValues ? x.OpeningValue + x.InValue - x.OutValue : null,
                };
            })
            // A row with nothing to show (all quantities and values zero) is dropped.
            .Where(r => r.OpeningQty != 0m || r.InQty != 0m || r.OutQty != 0m || r.ClosingQty != 0m
                || (r.OpeningValue ?? 0m) != 0m || (r.InValue ?? 0m) != 0m || (r.OutValue ?? 0m) != 0m
                || (r.ClosingValue ?? 0m) != 0m)
            .OrderBy(r => r.ProductCode, StringComparer.Ordinal)
            .ToList();

        return new StockSummaryReportDto
        {
            From = from,
            To = to,
            Scope = request.Scope,
            WarehouseId = request.WarehouseId,
            IsProvisional = IsProvisional(to, settings.CostingPeriod),
            CanViewCost = canViewCost,
            ValuesAtScopeOnly = valuesAtScopeOnly,
            Rows = rows,
            TotalOpeningValue = showValues ? rows.Sum(r => r.OpeningValue) : null,
            TotalInValue = showValues ? rows.Sum(r => r.InValue) : null,
            TotalOutValue = showValues ? rows.Sum(r => r.OutValue) : null,
            TotalClosingValue = showValues ? rows.Sum(r => r.ClosingValue) : null,
        };
    }

    /// One REPEATABLE READ snapshot for all the report's queries.
    public Task<List<StockCardTargetDto>> ListStockCardTargetsAsync(StockCardTargetsRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildStockCardTargetsAsync(request, c), ct);

    private async Task<List<StockCardTargetDto>> BuildStockCardTargetsAsync(StockCardTargetsRequest request, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var start = VnTime.StartOfDay(request.From);
        var end = VnTime.StartOfNextDay(request.To);

        var ledger = _db.InventoryLedger.AsNoTracking().Where(e => e.BranchId == branchId && e.PostedAt < end);
        if (request.WarehouseId is { } warehouseId)
            ledger = ledger.Where(e => e.WarehouseId == warehouseId);
        if (request.ProductIds is { } requestedProducts)
            ledger = ledger.Where(e => requestedProducts.Contains(e.ProductId));
        var pairs = await ledger
            .GroupBy(e => new { e.ProductId, e.WarehouseId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseId,
                OpeningQty = g.Sum(e => e.PostedAt < start ? e.QtyIn - e.QtyOut : 0m),
                InPeriod = g.Count(e => e.PostedAt >= start),
            })
            .ToListAsync(ct);
        var active = pairs.Where(p => p.OpeningQty != 0m || p.InPeriod > 0).ToList();

        var productIds = active.Select(p => p.ProductId).Distinct().ToList();
        var products = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var warehouseIds = active.Select(p => p.WarehouseId).Distinct().ToList();
        var warehouses = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Code, w.Name })
            .ToDictionaryAsync(w => w.Id, ct);

        return active
            .Select(p => new StockCardTargetDto
            {
                ProductId = p.ProductId,
                ProductCode = products.GetValueOrDefault(p.ProductId) ?? "",
                WarehouseId = p.WarehouseId,
                WarehouseCode = warehouses[p.WarehouseId].Code,
                WarehouseName = warehouses[p.WarehouseId].Name,
            })
            .OrderBy(t => t.ProductCode, StringComparer.Ordinal)
            .ThenBy(t => t.WarehouseCode, StringComparer.Ordinal)
            .ToList();
    }

    /// The costing period containing `date` has not ended yet (VN today).
    private bool IsProvisional(DateOnly date, CostingPeriod period) =>
        CostProvisional.IsProvisional(date, period, VnTime.ToVnDate(_clock.UtcNow));

    private static decimal R4(decimal x) => Math.Round(x, 4, MidpointRounding.AwayFromZero);

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
