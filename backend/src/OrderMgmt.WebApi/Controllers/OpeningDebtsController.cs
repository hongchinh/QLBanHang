using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.OpeningDebts.Interfaces;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/opening-debts")]
public class OpeningDebtsController : ApiControllerBase
{
    private readonly IOpeningDebtService _service;
    private readonly IValidator<UpsertOpeningDebtRequest> _upsertValidator;
    private readonly IValidator<OpeningDebtListRequest> _listValidator;

    public OpeningDebtsController(
        IOpeningDebtService service,
        IValidator<UpsertOpeningDebtRequest> upsertValidator,
        IValidator<OpeningDebtListRequest> listValidator)
    {
        _service = service;
        _upsertValidator = upsertValidator;
        _listValidator = listValidator;
    }

    [HttpGet]
    [HasPermission(Permissions.Debt.OpeningBalance)]
    public async Task<ActionResult<ApiResponse<OpeningDebtListResult>>> List(
        [FromQuery] OpeningDebtListRequest request, CancellationToken ct)
    {
        await _listValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.ListAsync(request, ct));
    }

    [HttpPost]
    [HasPermission(Permissions.Debt.OpeningBalance)]
    public async Task<ActionResult<ApiResponse<OpeningDebtDto>>> Create(
        [FromBody] UpsertOpeningDebtRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Debt.OpeningBalance)]
    public async Task<ActionResult<ApiResponse<OpeningDebtDto>>> Update(
        Guid id, [FromBody] UpsertOpeningDebtRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Debt.OpeningBalance)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid id, [FromQuery] uint? version, [FromQuery] bool confirmRemoveSettlements, CancellationToken ct)
    {
        await _service.DeleteAsync(id, version, confirmRemoveSettlements, ct);
        return Success();
    }
}
