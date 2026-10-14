using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.Tt133.Services;

/// Pieces shared by the report exports and the voucher prints: form context, sheet-count guard and the xlsx / pdf file.
public class Tt133ExportSupport
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string PdfContentType = "application/pdf";

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentBranch _currentBranch;
    private readonly IDateTime _clock;
    private readonly ISpreadsheetPdfConverter _pdfConverter;
    private readonly IOptions<Tt133ExportOptions> _options;
    private readonly ILogger<Tt133ExportSupport> _logger;

    public Tt133ExportSupport(
        IAppDbContext db,
        ICurrentUser currentUser,
        ICurrentBranch currentBranch,
        IDateTime clock,
        ISpreadsheetPdfConverter pdfConverter,
        IOptions<Tt133ExportOptions> options,
        ILogger<Tt133ExportSupport> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _currentBranch = currentBranch;
        _clock = clock;
        _pdfConverter = pdfConverter;
        _options = options;
        _logger = logger;
    }

    /// Company profile, working branch and signer of the form. Scope Company prints no branch.
    public async Task<Tt133FormContext> BuildContextAsync(ReportScope scope, CancellationToken ct)
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

    /// 0 sheets: 422 EXPORT_NO_TARGETS; more than MaxSheets: 422 EXPORT_TOO_MANY_SHEETS with the caller's message.
    public void EnsureSheetCount(int count, Func<int, int, string> tooManyMessage)
    {
        if (count == 0)
            throw new UnprocessableException("EXPORT_NO_TARGETS", "Không có đối tượng nào để xuất.");

        var max = _options.Value.MaxSheets;
        if (count > max)
            throw new UnprocessableException("EXPORT_TOO_MANY_SHEETS", tooManyMessage(count, max));
    }

    public async Task<ExportFile> ToFileAsync(byte[] xlsx, string baseName, ExportFormat format, CancellationToken ct)
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
}
