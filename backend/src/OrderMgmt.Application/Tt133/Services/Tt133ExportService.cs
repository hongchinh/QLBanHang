using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.CashDebt.Reports.Interfaces;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Reports.Interfaces;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Application.Tt133.Interfaces;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Tt133.Services;

public class Tt133ExportService : ITt133ExportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly ICashDebtReportService _cashDebtReports;
    private readonly IInventoryReportService _inventoryReports;
    private readonly ITt133FormRenderer _renderer;
    private readonly ISpreadsheetPdfConverter _pdfConverter;
    private readonly IOptions<Tt133ExportOptions> _options;
    private readonly ILogger<Tt133ExportService> _logger;

    public Tt133ExportService(
        IAppDbContext db,
        ICurrentUser currentUser,
        ICurrentBranch currentBranch,
        IDateTime clock,
        ITransactionRunner transaction,
        ICashDebtReportService cashDebtReports,
        IInventoryReportService inventoryReports,
        ITt133FormRenderer renderer,
        ISpreadsheetPdfConverter pdfConverter,
        IOptions<Tt133ExportOptions> options,
        ILogger<Tt133ExportService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _transaction = transaction;
        _cashDebtReports = cashDebtReports;
        _inventoryReports = inventoryReports;
        _renderer = renderer;
        _pdfConverter = pdfConverter;
        _options = options;
        _logger = logger;
    }

    public async Task<ExportFile> ExportCashBookAsync(CashBookReportRequest request, ExportFormat format, CancellationToken ct = default)
    {
        var kind = request.Kind switch
        {
            FundKindFilter.Cash => FundKind.Cash,
            FundKindFilter.Bank => FundKind.Bank,
            _ => throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                ["kind"] = ["Chọn loại quỹ Tiền mặt hoặc Ngân hàng để xuất theo mẫu."],
            }, null),
        };

        var (context, report) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await BuildContextAsync(ReportScope.Branch, token);
            var data = await _cashDebtReports.GetCashBookAsync(request, token);
            EnsureSheetCount(data.Groups.Count);
            return (formContext, data);
        }, ct);

        var xlsx = _renderer.RenderCashBook(context, report, kind);
        var code = kind == FundKind.Bank ? "S05-DNN" : "S04a-DNN";
        return await ToFileAsync(xlsx, BaseName(code, request.From, request.To), format, ct);
    }

    public async Task<ExportFile> ExportDebtLedgerAsync(DebtLedgerExportRequest request, ExportFormat format, CancellationToken ct = default)
    {
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);

        var (context, ledgers) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await BuildContextAsync(request.Scope, token);
            var targets = await DebtLedgerTargetsAsync(request, token);
            EnsureSheetCount(targets.Count);

            var data = new List<DebtLedgerReportDto>(targets.Count);
            foreach (var partnerId in targets)
            {
                data.Add(await _cashDebtReports.GetDebtLedgerAsync(new DebtLedgerReportRequest
                {
                    Side = request.Side,
                    PartnerId = partnerId,
                    From = request.From,
                    To = request.To,
                    Scope = request.Scope,
                }, token));
            }
            // One sheet per partner in code order, whatever the order of the request.
            data.Sort((a, b) => string.CompareOrdinal(a.PartnerCode, b.PartnerCode));
            return (formContext, data);
        }, ct);

        var xlsx = _renderer.RenderDebtLedgers(context, ledgers);
        return await ToFileAsync(xlsx, BaseName("S12-DNN", request.From, request.To), format, ct);
    }

    public async Task<ExportFile> ExportDebtSummaryAsync(DebtBalanceReportRequest request, ExportFormat format, CancellationToken ct = default)
    {
        if (request.PartnerId.HasValue)
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                ["partnerId"] = ["Bảng tổng hợp xuất cho mọi đối tượng. Dùng sổ S12 cho một đối tượng."],
            }, null);
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);

        var (context, report) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await BuildContextAsync(request.Scope, token);
            var data = await _cashDebtReports.GetDebtBalanceAsync(request, token);
            return (formContext, data);
        }, ct);

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var xlsx = _renderer.RenderDebtSummary(context, report, search);
        return await ToFileAsync(xlsx, BaseName("TongHopCongNo", request.From, request.To), format, ct);
    }

    public async Task<ExportFile> ExportDebtReconciliationAsync(DebtReconciliationReportRequest request, ExportFormat format, CancellationToken ct = default)
    {
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);

        var (context, report) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await BuildContextAsync(request.Scope, token);
            var data = await _cashDebtReports.GetDebtReconciliationAsync(request, token);
            return (formContext, data);
        }, ct);

        var xlsx = _renderer.RenderDebtReconciliation(context, report);
        return await ToFileAsync(xlsx, BaseName($"DoiChieuCongNo_{report.PartnerCode}", request.From, request.To), format, ct);
    }

    public async Task<ExportFile> ExportStockSummaryAsync(StockSummaryRequest request, ExportFormat format, CancellationToken ct = default)
    {
        ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope);

        var (context, report, warehouseLabel, groupName) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await BuildContextAsync(request.Scope, token);
            var data = await _inventoryReports.GetStockSummaryAsync(request, token);

            var label = "Tất cả kho";
            if (request.WarehouseId is { } warehouseId)
            {
                var warehouse = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
                    .Where(w => w.Id == warehouseId)
                    .Select(w => new { w.Code, w.Name })
                    .SingleAsync(token);
                label = $"{warehouse.Code} – {warehouse.Name}";
            }

            string? group = null;
            if (request.ProductGroupId is { } groupId)
                group = await _db.ProductGroups.IgnoreQueryFilters().AsNoTracking()
                    .Where(g => g.Id == groupId)
                    .Select(g => g.Name)
                    .SingleOrDefaultAsync(token);
            return (formContext, data, label, group);
        }, ct);

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var xlsx = _renderer.RenderStockSummary(context, report, warehouseLabel, groupName, search);
        return await ToFileAsync(xlsx, BaseName("S07-DNN", report.From, report.To), format, ct);
    }

    public async Task<ExportFile> ExportStockCardsAsync(StockCardExportRequest request, ExportFormat format, CancellationToken ct = default)
    {
        var (context, sheets) = await _transaction.RunSnapshotAsync(async token =>
        {
            var formContext = await BuildContextAsync(ReportScope.Branch, token);
            var data = await ResolveStockSheetsAsync(request, token);
            return (formContext, data);
        }, ct);

        var xlsx = _renderer.RenderStockCards(context, sheets);
        return await ToFileAsync(xlsx, BaseName("S08-DNN", request.From!.Value, request.To!.Value), format, ct);
    }

    /// All: every partner the balance report lists (a non-zero opening balance or movement); otherwise the distinct ids.
    private async Task<List<Guid>> DebtLedgerTargetsAsync(DebtLedgerExportRequest request, CancellationToken ct)
    {
        if (!request.All) return request.PartnerIds.Distinct().ToList();

        var balance = await _cashDebtReports.GetDebtBalanceAsync(new DebtBalanceReportRequest
        {
            Side = request.Side,
            From = request.From,
            To = request.To,
            Scope = request.Scope,
        }, ct);
        return balance.Rows.Where(r => r.PartnerId.HasValue).Select(r => r.PartnerId!.Value).ToList();
    }

    /// The sheets of S06 / S08 (T20): one stock card per product x warehouse, in product code then warehouse code order.
    private async Task<List<StockCardSheet>> ResolveStockSheetsAsync(StockCardExportRequest request, CancellationToken ct)
    {
        var from = request.From!.Value;
        var to = request.To!.Value;

        List<StockCardTargetDto> targets;
        if (request.All)
        {
            targets = await _inventoryReports.ListStockCardTargetsAsync(new StockCardTargetsRequest
            {
                From = from,
                To = to,
                WarehouseId = request.WarehouseId,
            }, ct);
        }
        else if (request.WarehouseId is { } warehouseId)
        {
            // An explicit product with a warehouse is always a sheet; GetStockCardAsync answers 404 / 403 for a bad id.
            var warehouse = await _db.Warehouses.IgnoreQueryFilters().AsNoTracking()
                .Where(w => w.Id == warehouseId)
                .Select(w => new { w.Code, w.Name })
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Warehouse", warehouseId);
            targets = request.ProductIds.Distinct()
                .Select(productId => new StockCardTargetDto
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    WarehouseCode = warehouse.Code,
                    WarehouseName = warehouse.Name,
                })
                .ToList();
        }
        else
        {
            targets = await _inventoryReports.ListStockCardTargetsAsync(new StockCardTargetsRequest
            {
                From = from,
                To = to,
                ProductIds = request.ProductIds.Distinct().ToList(),
            }, ct);
        }
        EnsureSheetCount(targets.Count);

        var sheets = new List<StockCardSheet>(targets.Count);
        foreach (var target in targets)
        {
            var card = await _inventoryReports.GetStockCardAsync(new StockCardRequest
            {
                ProductId = target.ProductId,
                WarehouseId = target.WarehouseId,
                From = from,
                To = to,
            }, ct);
            sheets.Add(new StockCardSheet(card, target.WarehouseCode, target.WarehouseName));
        }
        return sheets
            .OrderBy(s => s.Card.ProductCode, StringComparer.Ordinal)
            .ThenBy(s => s.WarehouseCode, StringComparer.Ordinal)
            .ToList();
    }

    /// Company profile, working branch and signer of the form. Scope Company prints no branch.
    private async Task<Tt133FormContext> BuildContextAsync(ReportScope scope, CancellationToken ct)
    {
        var profile = await _db.CompanyProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == 1, ct);
        var context = new Tt133FormContext
        {
            CompanyName = profile?.Name,
            TaxCode = profile?.TaxCode,
            ChiefAccountantName = profile?.ChiefAccountantName,
            LegalRepresentativeName = profile?.LegalRepresentativeName,
            AccountingServiceNote = profile?.AccountingServiceNote,
            BookkeeperName = _currentUser.FullName,
            PrintDate = VnTime.ToVnDate(_clock.UtcNow),
        };

        if (scope == ReportScope.Company)
        {
            context.Address = profile?.Address;
            context.OpeningDate = await _db.Branches.AsNoTracking()
                .Where(b => b.OpeningDate != null)
                .OrderBy(b => b.OpeningDate)
                .Select(b => b.OpeningDate)
                .FirstOrDefaultAsync(ct);
            return context;
        }

        var branchId = await _currentBranch.GetIdAsync(ct);
        var branch = await _db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Name, b.Address, b.OpeningDate })
            .SingleAsync(ct);
        context.BranchName = branch.Name;
        context.Address = string.IsNullOrWhiteSpace(branch.Address) ? profile?.Address : branch.Address;
        context.OpeningDate = branch.OpeningDate;
        return context;
    }

    /// 0 sheets: 422 EXPORT_NO_TARGETS; more than MaxSheets: 422 EXPORT_TOO_MANY_SHEETS.
    private void EnsureSheetCount(int count)
    {
        if (count == 0)
            throw new UnprocessableException("EXPORT_NO_TARGETS", "Không có đối tượng nào để xuất.");

        var max = _options.Value.MaxSheets;
        if (count > max)
            throw new UnprocessableException("EXPORT_TOO_MANY_SHEETS",
                $"Số sổ cần xuất ({count}) vượt giới hạn {max}. Hãy thu hẹp điều kiện lọc.");
    }

    private async Task<ExportFile> ToFileAsync(byte[] xlsx, string baseName, ExportFormat format, CancellationToken ct)
    {
        if (format == ExportFormat.Xlsx)
            return new ExportFile(xlsx, XlsxContentType, baseName + ".xlsx");

        try
        {
            var pdf = await _pdfConverter.ConvertAsync(xlsx, ct);
            return new ExportFile(pdf, PdfContentType, baseName + ".pdf");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The converter's own timeout throws with its linked token, so only the request token tells an aborted request.
            _logger.LogError(ex, "PDF conversion of {BaseName} failed", baseName);
            throw new ServiceUnavailableException("PDF_EXPORT_FAILED", "Xuất PDF thất bại, hãy dùng Excel.");
        }
    }

    private static string BaseName(string code, DateOnly from, DateOnly to) =>
        string.Create(CultureInfo.InvariantCulture, $"{code}_{from:yyyyMMdd}_{to:yyyyMMdd}");
}
