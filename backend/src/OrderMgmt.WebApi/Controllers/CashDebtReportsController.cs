using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Reports.Interfaces;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

/// Cash and debt reports of the working branch (Phase 09).
[Route("api/reports")]
public class CashDebtReportsController : ApiControllerBase
{
    private readonly ICashDebtReportService _service;
    private readonly IValidator<DebtBalanceReportRequest> _debtBalanceValidator;

    public CashDebtReportsController(ICashDebtReportService service, IValidator<DebtBalanceReportRequest> debtBalanceValidator)
    {
        _service = service;
        _debtBalanceValidator = debtBalanceValidator;
    }

    [HttpGet("debt-balance")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<ActionResult<ApiResponse<DebtBalanceReportDto>>> DebtBalance(
        [FromQuery] DebtBalanceReportRequest request, CancellationToken ct)
    {
        await _debtBalanceValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetDebtBalanceAsync(request, ct));
    }
}
