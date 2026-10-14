using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CashVouchers.Services;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Services;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
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

            var branchId = await _currentBranch.GetIdAsync(token);
            var byId = rows.ToDictionary(r => r.Id);
            foreach (var id in distinct)
            {
                if (!byId.TryGetValue(id, out var row))
                    throw new NotFoundException(nameof(CashVoucher), id);
                if (!_currentUser.HasPermission(CashVoucherPermissions.View(row.Type)))
                    throw new ForbiddenException();
                if (row.BranchId != branchId)
                    throw new ForbiddenException("Phiếu thuộc chi nhánh khác.");
            }

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
        if (priceMode != StockPrintPriceMode.Voucher)
            throw new NotSupportedException($"Price mode {priceMode} is not supported yet.");
        var distinct = (ids ?? []).Distinct().ToList();

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

            var list = rows
                .OrderBy(r => r.VoucherAt)
                .ThenBy(r => r.Code, StringComparer.Ordinal)
                .Select(ToStockModel)
                .ToList();
            return (formContext, list);
        }, ct);

        var xlsx = _renderer.RenderStockVouchers(context, models);
        return await ToFileAsync(xlsx, models.Select(m => m.Code).ToList(), StockBatchPrefix(models), format, ct);
    }

    private static StockVoucherPrintModel ToStockModel(StockVoucherRow r)
    {
        var warehouses = r.Lines.DistinctBy(l => l.WarehouseId).ToList();
        return new StockVoucherPrintModel
        {
            Type = r.Type,
            Code = r.Code,
            Date = VnTime.ToVnDate(r.VoucherAt),
            IsCancelled = r.Status == StockVoucherStatus.Cancelled,
            PriceMode = StockPrintPriceMode.Voucher,
            HandlerName = VoucherPrintRules.FirstFilled(r.HandlerName, r.PartnerName),
            PartnerName = VoucherPrintRules.FirstFilled(r.PartnerName),
            PartnerAddress = VoucherPrintRules.FirstFilled(r.PartnerAddress),
            ReasonName = r.ReasonName,
            WarehouseNames = warehouses.Count > 0
                ? string.Join(", ", warehouses.Select(l => l.WarehouseName).Distinct())
                : r.WarehouseName,
            ShowWarehouseColumn = warehouses.Count > 1,
            Lines = r.Lines.Select((l, i) => new StockVoucherPrintLine
            {
                Number = i + 1,
                Name = VoucherPrintRules.LineName(l.ProductName, l.PricingMode, l.Length, l.Width, l.Thickness, l.SheetCount),
                Code = l.ProductCode,
                UnitName = l.UnitName,
                WarehouseCode = l.WarehouseCode,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Amount = l.Amount,
            }).ToList(),
            Totals = VoucherPrintRules.VoucherTotals(r.GoodsAmount, r.LineDiscountTotal, r.OrderDiscount, r.Freight, r.Total)
                .ToList(),
            VatIncludedLine = VatIncludedLine(r),
            AmountInWords = VndAmountInWords.Read(r.Total),
            PreparerName = r.OwnerName,
        };
    }

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
