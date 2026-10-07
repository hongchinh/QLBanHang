using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.CashReasons.Interfaces;
using OrderMgmt.Application.CashDebt.CashReasons.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/cash-reasons")]
public class CashReasonsController : ApiControllerBase
{
    private readonly ICashReasonService _service;
    private readonly IValidator<CreateCashReasonRequest> _createValidator;
    private readonly IValidator<UpdateCashReasonRequest> _updateValidator;

    public CashReasonsController(
        ICashReasonService service,
        IValidator<CreateCashReasonRequest> createValidator,
        IValidator<UpdateCashReasonRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashReasonDto>>>> List(
        [FromQuery] CashVoucherType? type, [FromQuery] bool includeAuto, CancellationToken ct)
        => Success(await _service.ListAsync(type, includeAuto, ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CashReasonDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.Cash.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<CashReasonDto>>> Create(
        [FromBody] CreateCashReasonRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Cash.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<CashReasonDto>>> Update(
        Guid id, [FromBody] UpdateCashReasonRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Cash.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Success();
    }
}
