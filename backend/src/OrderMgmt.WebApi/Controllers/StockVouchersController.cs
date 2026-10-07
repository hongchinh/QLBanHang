using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Catalog.Customers.Models;
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

    /// `voucherAt` must carry an offset (…Z or +07:00).
    [HttpGet("defaults")]
    public async Task<ActionResult<ApiResponse<StockVoucherDefaultsDto>>> GetDefaults(
        [FromQuery] StockDirection type, [FromQuery] DateTimeOffset? voucherAt, CancellationToken ct)
        => Success(await _service.GetDefaultsAsync(type, voucherAt, ct));

    [HttpPost("stock-at")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StockAtResult>>>> GetStockAt(
        [FromBody] StockAtRequest request, CancellationToken ct)
        => Success(await _service.GetStockAtAsync(request, ct));

    [HttpGet("partners")]
    public async Task<ActionResult<ApiResponse<List<CustomerSearchItemDto>>>> SearchPartners(
        [FromQuery] StockDirection type,
        [FromQuery] string? keyword = null,
        [FromQuery] int limit = 20,
        [FromQuery] Guid? reasonId = null,
        CancellationToken ct = default)
        => Success(await _service.SearchPartnersAsync(type, keyword, limit, reasonId, ct));

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

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<StockVoucherDto>>> Cancel(
        Guid id, [FromBody] StockVoucherActionRequest request, CancellationToken ct)
        => Success(await _service.CancelAsync(id, request, ct));

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<ApiResponse<StockVoucherDto>>> Restore(
        Guid id, [FromBody] StockVoucherActionRequest request, CancellationToken ct)
        => Success(await _service.RestoreAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid id, [FromQuery] StockVoucherActionRequest request, CancellationToken ct)
    {
        await _service.DeleteAsync(id, request, ct);
        return Success();
    }
}
