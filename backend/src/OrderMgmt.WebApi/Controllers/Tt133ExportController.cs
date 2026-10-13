using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Tt133.Interfaces;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

/// Excel/PDF exports of the report forms of Circular 133/2016/TT-BTC. One action per report, so each keeps the
/// permission of its source report.
[Route("api/reports")]
public class Tt133ExportController : ApiControllerBase
{
    private readonly ITt133ExportService _exports;
    private readonly IValidator<CashBookReportRequest> _cashBookValidator;
    private readonly IValidator<DebtLedgerExportRequest> _debtLedgerValidator;

    public Tt133ExportController(
        ITt133ExportService exports,
        IValidator<CashBookReportRequest> cashBookValidator,
        IValidator<DebtLedgerExportRequest> debtLedgerValidator)
    {
        _exports = exports;
        _cashBookValidator = cashBookValidator;
        _debtLedgerValidator = debtLedgerValidator;
    }

    /// S04a-DNN (kind=Cash) or S05-DNN (kind=Bank).
    [HttpGet("cash-book/export")]
    [HasPermission(Permissions.Reports.Cash)]
    public async Task<IActionResult> CashBook(
        [FromQuery] CashBookReportRequest request, [FromQuery] ExportFormat format, CancellationToken ct)
    {
        await _cashBookValidator.ValidateAndThrowAsync(request, ct);
        var file = await _exports.ExportCashBookAsync(request, format, ct);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    /// S12-DNN: the body names the partners (a POST, because a list of ids does not fit a URL).
    [HttpPost("debt-ledger/export")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<IActionResult> DebtLedger(
        [FromBody] DebtLedgerExportRequest request, [FromQuery] ExportFormat format, CancellationToken ct)
    {
        await _debtLedgerValidator.ValidateAndThrowAsync(request, ct);
        var file = await _exports.ExportDebtLedgerAsync(request, format, ct);
        return File(file.Bytes, file.ContentType, file.FileName);
    }
}
