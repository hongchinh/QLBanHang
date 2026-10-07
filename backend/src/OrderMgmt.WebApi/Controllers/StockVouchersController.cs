using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.WebApi.Controllers;

/// Type-specific permissions (stock_in.* / stock_out.*) are checked by the service.
[Route("api/stock-vouchers")]
[Authorize]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
public class StockVouchersController : ApiControllerBase
{
    private readonly IStockVoucherService _service;
    private readonly IValidator<UpsertStockVoucherRequest> _upsertValidator;
    private readonly IValidator<StockVoucherListRequest> _listValidator;

    public StockVouchersController(IStockVoucherService service, IValidator<UpsertStockVoucherRequest> upsertValidator,
        IValidator<StockVoucherListRequest> listValidator)
    {
        _service = service;
        _upsertValidator = upsertValidator;
        _listValidator = listValidator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<StockVoucherListResult>>> List(
        [FromQuery] StockVoucherListRequest request, CancellationToken ct)
    {
        await _listValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.ListAsync(request, ct));
    }

    [HttpGet("owners")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StockVoucherOwnerDto>>>> ListOwners(
        [FromQuery] StockDirection type, CancellationToken ct)
        => Success(await _service.ListOwnersAsync(type, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StockVoucherDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpGet("{id:guid}/activities")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StockVoucherActivityDto>>>> ListActivities(
        Guid id, CancellationToken ct)
        => Success(await _service.ListActivitiesAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<StockVoucherDto>>> Create(
        [FromBody] UpsertStockVoucherRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StockVoucherDto>>> Update(
        Guid id, [FromBody] UpsertStockVoucherRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }
}
