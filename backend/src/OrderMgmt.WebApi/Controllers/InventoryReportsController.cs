using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Reports.Interfaces;
using OrderMgmt.Application.Inventory.Reports.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/reports")]
public class InventoryReportsController : ApiControllerBase
{
    private readonly IInventoryReportService _service;
    private readonly IValidator<StockOnHandReportRequest> _stockOnHandValidator;
    private readonly IValidator<StockCardRequest> _stockCardValidator;

    public InventoryReportsController(IInventoryReportService service, IValidator<StockOnHandReportRequest> stockOnHandValidator,
        IValidator<StockCardRequest> stockCardValidator)
    {
        _service = service;
        _stockOnHandValidator = stockOnHandValidator;
        _stockCardValidator = stockCardValidator;
    }

    [HttpGet("stock-on-hand")]
    [HasPermission(Permissions.Reports.Inventory)]
    public async Task<ActionResult<ApiResponse<StockOnHandReportDto>>> StockOnHand(
        [FromQuery] StockOnHandReportRequest request, CancellationToken ct)
    {
        await _stockOnHandValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetStockOnHandAsync(request, ct));
    }

    [HttpGet("stock-card")]
    [HasPermission(Permissions.Reports.Inventory)]
    public async Task<ActionResult<ApiResponse<StockCardDto>>> StockCard(
        [FromQuery] StockCardRequest request, CancellationToken ct)
    {
        await _stockCardValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetStockCardAsync(request, ct));
    }
}
