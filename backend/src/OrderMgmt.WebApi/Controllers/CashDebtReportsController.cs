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
    private readonly IValidator<CashBookReportRequest> _cashBookValidator;

    public CashDebtReportsController(
        ICashDebtReportService service,
        IValidator<DebtBalanceReportRequest> debtBalanceValidator,
        IValidator<DebtLedgerReportRequest> debtLedgerValidator,
        IValidator<CashBookReportRequest> cashBookValidator)
    {
        _service = service;
        _debtBalanceValidator = debtBalanceValidator;
        _debtLedgerValidator = debtLedgerValidator;
        _cashBookValidator = cashBookValidator;
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

    [HttpGet("cash-book")]
    [HasPermission(Permissions.Reports.Cash)]
    public async Task<ActionResult<ApiResponse<CashBookReportDto>>> CashBook(
        [FromQuery] CashBookReportRequest request, CancellationToken ct)
    {
        await _cashBookValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetCashBookAsync(request, ct));
    }
}
