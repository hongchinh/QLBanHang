using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.OpeningDebts.Interfaces;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;
using OrderMgmt.WebApi.Filters;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/opening-debts")]
[DashboardSignal(DashboardDomains.Debt)]
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

    [HttpGet("owners")]
    [HasPermission(Permissions.Debt.OpeningBalance)]
    public async Task<ActionResult<ApiResponse<List<OpeningDebtOwnerDto>>>> ListOwners(CancellationToken ct) =>
        Success(await _service.ListOwnersAsync(ct));

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
        Guid id, [FromQuery] uint? version, [FromQuery] bool confirmRemoveSettlements,
        [FromQuery] string? confirmedSettlements, CancellationToken ct)
    {
        await _service.DeleteAsync(id, version, confirmRemoveSettlements, confirmedSettlements, ct);
        return Success();
    }
}
