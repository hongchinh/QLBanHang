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
    private readonly IValidator<DebtLedgerReportRequest> _debtLedgerValidator;

    public CashDebtReportsController(
        ICashDebtReportService service,
        IValidator<DebtBalanceReportRequest> debtBalanceValidator,
        IValidator<DebtLedgerReportRequest> debtLedgerValidator)
    {
        _service = service;
        _debtBalanceValidator = debtBalanceValidator;
        _debtLedgerValidator = debtLedgerValidator;
    }

    [HttpGet("debt-balance")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<ActionResult<ApiResponse<DebtBalanceReportDto>>> DebtBalance(
        [FromQuery] DebtBalanceReportRequest request, CancellationToken ct)
    {
        await _debtBalanceValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetDebtBalanceAsync(request, ct));
    }

    [HttpGet("debt-ledger")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<ActionResult<ApiResponse<DebtLedgerReportDto>>> DebtLedger(
        [FromQuery] DebtLedgerReportRequest request, CancellationToken ct)
    {
        await _debtLedgerValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetDebtLedgerAsync(request, ct));
    }
}
