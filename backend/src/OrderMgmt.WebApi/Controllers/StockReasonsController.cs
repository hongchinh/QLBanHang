using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockReasons.Interfaces;
using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/stock-reasons")]
public class StockReasonsController : ApiControllerBase
{
    private readonly IStockReasonService _service;
    private readonly IValidator<CreateStockReasonRequest> _createValidator;
    private readonly IValidator<UpdateStockReasonRequest> _updateValidator;

    public StockReasonsController(
        IStockReasonService service,
        IValidator<CreateStockReasonRequest> createValidator,
        IValidator<UpdateStockReasonRequest> updateValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StockReasonDto>>>> List(
        [FromQuery] StockDirection? direction, CancellationToken ct)
        => Success(await _service.ListAsync(direction, ct));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<StockReasonDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.Inventory.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<StockReasonDto>>> Create(
        [FromBody] CreateStockReasonRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Inventory.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse<StockReasonDto>>> Update(
        Guid id, [FromBody] UpdateStockReasonRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Inventory.ManageCatalogs)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Success();
    }
}
