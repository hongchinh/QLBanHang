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

    public InventoryReportsController(IInventoryReportService service, IValidator<StockOnHandReportRequest> stockOnHandValidator)
    {
        _service = service;
        _stockOnHandValidator = stockOnHandValidator;
    }

    [HttpGet("stock-on-hand")]
    [HasPermission(Permissions.Reports.Inventory)]
    public async Task<ActionResult<ApiResponse<StockOnHandReportDto>>> StockOnHand(
        [FromQuery] StockOnHandReportRequest request, CancellationToken ct)
    {
        await _stockOnHandValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetStockOnHandAsync(request, ct));
    }
}
