using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Funds.Interfaces;
using OrderMgmt.Application.CashDebt.Funds.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/funds")]
public class FundsController : ApiControllerBase
{
    private readonly IFundService _service;
    private readonly IValidator<CreateFundRequest> _createValidator;
    private readonly IValidator<UpdateFundRequest> _updateValidator;

    public FundsController(
        IFundService service,
        IValidator<CreateFundRequest> createValidator,
        IValidator<UpdateFundRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FundDto>>>> List(
        [FromQuery] FundListRequest request, CancellationToken ct)
        => Success(await _service.ListAsync(request, ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<FundDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.Cash.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<FundDto>>> Create(
        [FromBody] CreateFundRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Cash.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<FundDto>>> Update(
        Guid id, [FromBody] UpdateFundRequest request, CancellationToken ct)
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
