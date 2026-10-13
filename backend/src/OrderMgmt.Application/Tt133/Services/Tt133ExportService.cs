using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.CashDebt.Reports.Interfaces;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
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
