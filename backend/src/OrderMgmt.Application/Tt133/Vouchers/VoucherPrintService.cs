using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Services;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Services;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Tt133.Vouchers;

public class VoucherPrintService : IVoucherPrintService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly Tt133ExportSupport _support;
    private readonly IVoucherFormRenderer _renderer;

    public VoucherPrintService(
        IAppDbContext db,
        ICurrentUser currentUser,
        ICurrentBranch currentBranch,
        IDateTime clock,
        ITransactionRunner transaction,
        Tt133ExportSupport support,
        IVoucherFormRenderer renderer)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
        _support = support;
        _renderer = renderer;
    }

    public async Task<ExportFile> PrintCashVouchersAsync(IReadOnlyList<Guid> ids, ExportFormat format, CancellationToken ct = default)
    {
        // A JSON null binds Ids to null; it counts as empty.
        var distinct = (ids ?? []).Distinct().ToList();
        _support.EnsureSheetCount(distinct.Count, VouchersTooMany);

        var (context, models) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await _support.BuildContextAsync(ReportScope.Branch, token);

            // Soft-deleted reasons, partners and owners keep their names; the voucher and its lines are filtered by hand.
            var rows = await _db.CashVouchers.IgnoreQueryFilters().AsNoTracking()
                .Where(v => distinct.Contains(v.Id) && !v.IsDeleted)
                .Select(v => new
                {
                    v.Id,
                    v.Type,
                    v.Code,
                    v.VoucherAt,
                    v.BranchId,
                    v.Status,
                    v.PersonName,
                    v.PartnerName,
                    v.PartnerAddress,
                    v.Description,
                    v.AttachedDocs,
                    v.Total,
                    ReasonName = v.Reason!.Name,
                    OwnerName = v.Owner != null ? v.Owner.FullName : null,
                    LineDescriptions = v.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder)
                        .Select(l => l.Description).ToList(),
                })
                .ToListAsync(token);

            await EnsureAccessAsync(distinct, rows.ToDictionary(r => r.Id), nameof(CashVoucher),
                r => CashVoucherPermissions.View(r.Type), r => r.BranchId, token);

            var list = rows
                .OrderBy(r => r.VoucherAt)
                .ThenBy(r => r.Code, StringComparer.Ordinal)
                .Select(r => new CashVoucherPrintModel
                {
                    Type = r.Type,
                    Code = r.Code,
                    Date = VnTime.ToVnDate(r.VoucherAt),
                    IsCancelled = r.Status == DocumentStatus.Cancelled,
                    PersonName = VoucherPrintRules.FirstFilled(r.PersonName, r.PartnerName),
                    Address = VoucherPrintRules.FirstFilled(r.PartnerAddress),
                    Reason = VoucherPrintRules.CashReason(r.Description, r.LineDescriptions, r.ReasonName),
                    Amount = r.Total,
                    AmountInWords = VndAmountInWords.Read(r.Total),
                    AttachedDocsLine = VoucherPrintRules.AttachedDocsLine(r.AttachedDocs),
                    PreparerName = r.OwnerName,
                })
                .ToList();
            return (formContext, list);
        }, ct);

        var xlsx = _renderer.RenderCashVouchers(context, models);
        return await ToFileAsync(xlsx, models.Select(m => m.Code).ToList(), CashBatchPrefix(models), format, ct);
    }

    public async Task<ExportFile> PrintStockVouchersAsync(IReadOnlyList<Guid> ids, StockPrintPriceMode priceMode,
        ExportFormat format, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(priceMode))
            throw new ValidationDomainException(
                new Dictionary<string, string[]> { ["priceMode"] = ["Chế độ giá không hợp lệ."] }, null);
        if (priceMode == StockPrintPriceMode.Cost && !_currentUser.HasPermission(Permissions.Inventory.ViewCost))
            throw new ForbiddenException("Cần quyền xem giá vốn để in phiếu theo giá vốn.");
        // A JSON null binds Ids to null; it counts as empty.
        var distinct = (ids ?? []).Distinct().ToList();
        _support.EnsureSheetCount(distinct.Count, VouchersTooMany);

        var (context, models) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await _support.BuildContextAsync(ReportScope.Branch, token);

            // Soft-deleted partners, reasons, warehouses and owners keep their names; the voucher and its lines are
            // filtered by hand.
            var rows = await _db.StockVouchers.IgnoreQueryFilters().AsNoTracking()
                .Where(v => distinct.Contains(v.Id) && !v.IsDeleted)
                .Select(v => new StockVoucherRow
                {
                    Id = v.Id,
                    Type = v.Type,
                    Code = v.Code,
                    VoucherAt = v.VoucherAt,
                    BranchId = v.BranchId,
                    Status = v.Status,
                    HandlerName = v.HandlerName,
                    PartnerName = v.PartnerName,
                    PartnerAddress = v.PartnerAddress,
                    GoodsAmount = v.GoodsAmount,
                    LineDiscountTotal = v.LineDiscountTotal,
                    OrderDiscount = v.OrderDiscount,
                    Freight = v.Freight,
                    Total = v.Total,
                    ReasonName = v.Reason!.Name,
                    WarehouseName = v.Warehouse!.Name,
                    OwnerName = v.Owner != null ? v.Owner.FullName : null,
                    Lines = v.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).Select(l => new StockVoucherLineRow
                    {
                        Id = l.Id,
                        ProductName = l.ProductName,
                        ProductCode = l.ProductCode,
                        UnitName = l.UnitName,
                        PricingMode = l.PricingMode,
                        Length = l.Length,
                        Width = l.Width,
                        Thickness = l.Thickness,
                        SheetCount = l.SheetCount,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        Amount = l.Amount,
                        VatAmount = l.VatAmount,
                        PriceIncludesVat = l.PriceIncludesVat,
                        TrackInventory = l.TrackInventory,
                        WarehouseId = l.WarehouseId,
                        WarehouseCode = l.Warehouse!.Code,
                        WarehouseName = l.Warehouse.Name,
                    }).ToList(),
                })
                .ToListAsync(token);
            await EnsureAccessAsync(distinct, rows.ToDictionary(r => r.Id), nameof(StockVoucher),
                r => StockVoucherPermissions.View(r.Type), r => r.BranchId, token);

            CostSource? cost = null;
            if (priceMode == StockPrintPriceMode.Cost)
            {
                var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, token);
                var voucherIds = rows.Select(r => r.Id).ToList();
                // Read through the (SourceType, SourceId) index; SourceLineId has none, so lines are matched in memory.
                var ledger = await _db.InventoryLedger.AsNoTracking()
                    .Where(e => (e.SourceType == LedgerSourceType.StockIn || e.SourceType == LedgerSourceType.StockOut)
                        && voucherIds.Contains(e.SourceId))
                    .ToListAsync(token);
                cost = new CostSource(
                    // One ledger row per voucher line (the engine posts each line once); a duplicate would be a ledger bug.
                    ledger.Where(e => e.SourceLineId != null).ToDictionary(e => e.SourceLineId!.Value),
                    settings.CostingPeriod,
                    VnTime.ToVnDate(_clock.UtcNow));
            }

            var list = rows
                .OrderBy(r => r.VoucherAt)
                .ThenBy(r => r.Code, StringComparer.Ordinal)
                .Select(r => ToStockModel(r, priceMode, cost))
                .ToList();
            return (formContext, list);
        }, ct);

        var xlsx = _renderer.RenderStockVouchers(context, models);
        return await ToFileAsync(xlsx, models.Select(m => m.Code).ToList(), StockBatchPrefix(models), format, ct);
    }

    private static StockVoucherPrintModel ToStockModel(StockVoucherRow r, StockPrintPriceMode priceMode, CostSource? cost)
    {
        var cancelled = r.Status == StockVoucherStatus.Cancelled;
        // A cancelled voucher has no ledger rows left: at cost it prints without money.
        var mode = priceMode == StockPrintPriceMode.Cost && cancelled ? StockPrintPriceMode.None : priceMode;
        var printed = mode == StockPrintPriceMode.Voucher ? r.Lines : r.Lines.Where(l => l.TrackInventory).ToList();
        var model = ToStockHeader(r, printed);
        model.PriceMode = mode;
        if (priceMode == StockPrintPriceMode.Cost && cancelled) model.Notes.Add("Phiếu đã hủy, không có giá vốn.");

        switch (mode)
        {
            case StockPrintPriceMode.Voucher:
                FillAtVoucherPrices(model, r);
                break;
            case StockPrintPriceMode.Cost:
                FillAtCost(model, r, printed, cost!);
                break;
            default:
                model.Lines = printed.Select((l, i) => PrintLine(l, i + 1, null, null)).ToList();
                model.Totals = [new StockVoucherPrintTotal("Cộng", null)];
                break;
        }

        if (mode != StockPrintPriceMode.Voucher && printed.Count == 0)
        {
            model.Totals = [new StockVoucherPrintTotal("Cộng", null)];
            model.AmountInWords = null;
            model.Notes.Add("Phiếu không có hàng theo dõi tồn kho.");
        }
        return model;
    }

    /// Everything but the lines, totals and words; the warehouses are those of the printed lines, else the voucher's.
    private static StockVoucherPrintModel ToStockHeader(StockVoucherRow r, IReadOnlyList<StockVoucherLineRow> printed)
    {
        var warehouses = printed.DistinctBy(l => l.WarehouseId).ToList();
        return new StockVoucherPrintModel
        {
            Type = r.Type,
            Code = r.Code,
            Date = VnTime.ToVnDate(r.VoucherAt),
            IsCancelled = r.Status == StockVoucherStatus.Cancelled,
            HandlerName = VoucherPrintRules.FirstFilled(r.HandlerName, r.PartnerName),
            PartnerName = VoucherPrintRules.FirstFilled(r.PartnerName),
            PartnerAddress = VoucherPrintRules.FirstFilled(r.PartnerAddress),
            ReasonName = r.ReasonName,
            WarehouseNames = warehouses.Count > 0
                ? string.Join(", ", warehouses.Select(l => l.WarehouseName).Distinct())
                : r.WarehouseName,
            ShowWarehouseColumn = warehouses.Count > 1,
            PreparerName = r.OwnerName,
        };
    }

    /// Every line at its own price; the stored totals under the table.
    private static void FillAtVoucherPrices(StockVoucherPrintModel model, StockVoucherRow r)
    {
        model.Lines = r.Lines.Select((l, i) => PrintLine(l, i + 1, l.UnitPrice, l.Amount)).ToList();
        model.Totals = VoucherPrintRules.VoucherTotals(r.GoodsAmount, r.LineDiscountTotal, r.OrderDiscount, r.Freight, r.Total)
            .ToList();
        model.VatIncludedLine = VatIncludedLine(r);
        model.AmountInWords = VndAmountInWords.Read(r.Total);
    }

    /// Tracked lines at their ledger value (stock-in) or cost (stock-out); one "Cộng" row. A line without a ledger
    /// row prints with an empty price and amount.
    private static void FillAtCost(StockVoucherPrintModel model, StockVoucherRow r,
        IReadOnlyList<StockVoucherLineRow> printed, CostSource cost)
    {
        var stockIn = r.Type == StockDirection.In;
        model.Lines = printed.Select((l, i) =>
        {
            var entry = cost.ByLine.GetValueOrDefault(l.Id);
            decimal? unitPrice = stockIn
                ? entry is { QtyIn: > 0 } ? R4(entry.InValue / entry.QtyIn) : null
                : entry?.UnitCost;
            decimal? amount = stockIn ? entry?.InValue : entry?.CostAmount;
            return PrintLine(l, i + 1, unitPrice, amount);
        }).ToList();
        var sum = model.Lines.Sum(l => l.Amount ?? 0);
        model.Totals = [new StockVoucherPrintTotal("Cộng", sum)];
        model.AmountInWords = VndAmountInWords.Read(sum);
        if (!stockIn && CostProvisional.IsProvisional(model.Date, cost.Period, cost.Today))
            model.Notes.Add("Giá vốn tạm tính (kỳ tính giá vốn chưa kết thúc).");
    }

    private static StockVoucherPrintLine PrintLine(StockVoucherLineRow l, int number, decimal? unitPrice, decimal? amount) =>
        new()
        {
            Number = number,
            Name = VoucherPrintRules.LineName(l.ProductName, l.PricingMode, l.Length, l.Width, l.Thickness, l.SheetCount),
            Code = l.ProductCode,
            UnitName = l.UnitName,
            WarehouseCode = l.WarehouseCode,
            Quantity = l.Quantity,
            UnitPrice = unitPrice,
            Amount = amount,
        };

    /// The stock card's rounding of a unit cost.
    private static decimal R4(decimal x) => Math.Round(x, 4, MidpointRounding.AwayFromZero);

    /// The ledger rows of the printed vouchers by line, and what the provisional-cost note needs.
    private sealed record CostSource(Dictionary<Guid, InventoryLedgerEntry> ByLine, CostingPeriod Period, DateOnly Today);

    /// A stock-out whose prices include VAT shows that VAT under the table, outside the sum.
    private static string? VatIncludedLine(StockVoucherRow r)
    {
        if (r.Type != StockDirection.Out) return null;
        var vat = r.Lines.Where(l => l.PriceIncludesVat).Sum(l => l.VatAmount);
        return vat > 0 ? $"Trong đó thuế GTGT đã gồm trong giá: {VoucherPrintRules.Money(vat)}" : null;
    }

    /// One voucher → `{Code}`; several → `{prefix}_{yyyyMMdd-HHmm}` (VN time).
    private Task<ExportFile> ToFileAsync(byte[] xlsx, IReadOnlyList<string> codes, string batchPrefix,
        ExportFormat format, CancellationToken ct)
    {
        var baseName = codes.Count == 1 ? codes[0] : VoucherPrintRules.BatchFileBase(batchPrefix, _clock.UtcNow);
        return _support.ToFileAsync(xlsx, baseName, format, ct);
    }

    /// As the voucher page, per id in request order: missing → 404, no view permission of its type → 403, another
    /// branch → 403. The first failing id fails the whole request.
    private async Task EnsureAccessAsync<TRow>(IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, TRow> byId,
        string resource, Func<TRow, string> viewPermission, Func<TRow, Guid> branchOf, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var row))
                throw new NotFoundException(resource, id);
            if (!_currentUser.HasPermission(viewPermission(row)))
                throw new ForbiddenException();
            if (branchOf(row) != branchId)
                throw new ForbiddenException("Phiếu thuộc chi nhánh khác.");
        }
    }

    private static string VouchersTooMany(int count, int max) =>
        $"Số phiếu cần in ({count}) vượt giới hạn {max}. Hãy chọn ít phiếu hơn.";

    private static string CashBatchPrefix(IReadOnlyList<CashVoucherPrintModel> models)
    {
        var receipts = models.Any(m => m.Type == CashVoucherType.Receipt);
        var payments = models.Any(m => m.Type == CashVoucherType.Payment);
        return receipts && payments ? "PhieuThuChi" : payments ? "PhieuChi" : "PhieuThu";
    }

    private static string StockBatchPrefix(IReadOnlyList<StockVoucherPrintModel> models)
    {
        var stockIn = models.Any(m => m.Type == StockDirection.In);
        var stockOut = models.Any(m => m.Type == StockDirection.Out);
        return stockIn && stockOut ? "PhieuNhapXuat" : stockOut ? "PhieuXuatKho" : "PhieuNhapKho";
    }

    private sealed class StockVoucherRow
    {
        public Guid Id { get; init; }
        public StockDirection Type { get; init; }
        public string Code { get; init; } = default!;
        public DateTimeOffset VoucherAt { get; init; }
        public Guid BranchId { get; init; }
        public StockVoucherStatus Status { get; init; }
        public string? HandlerName { get; init; }
        public string? PartnerName { get; init; }
        public string? PartnerAddress { get; init; }
        public decimal GoodsAmount { get; init; }
        public decimal LineDiscountTotal { get; init; }
        public decimal OrderDiscount { get; init; }
        public decimal Freight { get; init; }
        public decimal Total { get; init; }
        public string? ReasonName { get; init; }
        public string WarehouseName { get; init; } = default!;
        public string? OwnerName { get; init; }
        public List<StockVoucherLineRow> Lines { get; init; } = new();
    }

    private sealed class StockVoucherLineRow
    {
        public Guid Id { get; init; }
        public string ProductName { get; init; } = default!;
        public string ProductCode { get; init; } = default!;
        public string UnitName { get; init; } = default!;
        public PricingMode PricingMode { get; init; }
        public decimal? Length { get; init; }
        public decimal? Width { get; init; }
        public decimal? Thickness { get; init; }
        public decimal? SheetCount { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal Amount { get; init; }
        public decimal VatAmount { get; init; }
        public bool PriceIncludesVat { get; init; }
        public bool TrackInventory { get; init; }
        public Guid WarehouseId { get; init; }
        public string WarehouseCode { get; init; } = default!;
        public string WarehouseName { get; init; } = default!;
    }
}
