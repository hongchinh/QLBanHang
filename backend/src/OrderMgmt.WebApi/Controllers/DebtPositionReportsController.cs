using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Reports.Interfaces;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

/// Debt position reports of the working branch (Round 2B).
[Route("api/reports")]
public class DebtPositionReportsController : ApiControllerBase
{
    private readonly IDebtPositionReportService _service;
    private readonly IValidator<DebtDocumentsReportRequest> _documentsValidator;
    private readonly IValidator<DebtAgingReportRequest> _agingValidator;

    public DebtPositionReportsController(
        IDebtPositionReportService service,
        IValidator<DebtDocumentsReportRequest> documentsValidator,
        IValidator<DebtAgingReportRequest> agingValidator)
    {
        _service = service;
        _documentsValidator = documentsValidator;
        _agingValidator = agingValidator;
    }

    [HttpGet("debt-documents")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<ActionResult<ApiResponse<DebtDocumentsReportDto>>> DebtDocuments(
        [FromQuery] DebtDocumentsReportRequest request, CancellationToken ct)
    {
        await _documentsValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetDocumentsAsync(request, ct));
    }

    [HttpGet("debt-aging")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<ActionResult<ApiResponse<DebtAgingReportDto>>> DebtAging(
        [FromQuery] DebtAgingReportRequest request, CancellationToken ct)
    {
        await _agingValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetAgingAsync(request, ct));
    }
}
