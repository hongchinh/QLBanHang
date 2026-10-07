using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.OpeningStocks.Interfaces;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/inventory/opening-stock")]
public class OpeningStocksController : ApiControllerBase
{
    private readonly IOpeningStockService _service;
    private readonly IValidator<SaveOpeningStockRequest> _saveValidator;

    public OpeningStocksController(IOpeningStockService service, IValidator<SaveOpeningStockRequest> saveValidator)
    {
        _service = service;
        _saveValidator = saveValidator;
    }

    [HttpGet]
    [HasPermission(Permissions.Inventory.OpeningStock)]
    public async Task<ActionResult<ApiResponse<OpeningStockGridDto>>> Get([FromQuery] Guid warehouseId, CancellationToken ct)
        => Success(await _service.GetAsync(warehouseId, ct));

    [HttpPut]
    [HasPermission(Permissions.Inventory.OpeningStock)]
    public async Task<ActionResult<ApiResponse<OpeningStockGridDto>>> Save(
        [FromBody] SaveOpeningStockRequest request, CancellationToken ct)
    {
        await _saveValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.SaveAsync(request, ct));
    }
}
