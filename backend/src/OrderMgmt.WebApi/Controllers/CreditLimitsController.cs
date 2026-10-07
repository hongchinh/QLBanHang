using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.CreditLimits.Interfaces;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/credit-limits")]
public class CreditLimitsController : ApiControllerBase
{
    private readonly ICreditLimitService _service;
    private readonly IValidator<UpsertCreditLimitRequest> _upsertValidator;
    private readonly IValidator<PageRequest> _listValidator;

    public CreditLimitsController(
        ICreditLimitService service,
        IValidator<UpsertCreditLimitRequest> upsertValidator,
        IValidator<PageRequest> listValidator)
    {
        _service = service;
        _upsertValidator = upsertValidator;
        _listValidator = listValidator;
    }

    [HttpGet]
    [HasPermission(Permissions.CreditLimits.Manage)]
    public async Task<ActionResult<ApiResponse<CreditLimitListResult>>> List(
        [FromQuery] PageRequest request, CancellationToken ct)
    {
        await _listValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.ListAsync(request, ct));
    }

    [HttpPut("{customerId:guid}")]
    [HasPermission(Permissions.CreditLimits.Manage)]
    public async Task<ActionResult<ApiResponse<CreditLimitDto>>> Upsert(
        Guid customerId, [FromBody] UpsertCreditLimitRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpsertAsync(customerId, request, ct));
    }

    [HttpDelete("{customerId:guid}")]
    [HasPermission(Permissions.CreditLimits.Manage)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid customerId, CancellationToken ct)
    {
        await _service.DeleteAsync(customerId, ct);
        return Success();
    }
}
