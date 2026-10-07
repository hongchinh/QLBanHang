using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Costing;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

[Route("api/inventory/recalc-cost")]
public class InventoryCostController : ApiControllerBase
{
    private readonly IInventoryRecalcService _service;
    private readonly IValidator<RecalculateCostRequest> _validator;

    public InventoryCostController(IInventoryRecalcService service, IValidator<RecalculateCostRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost]
    [HasPermission(Permissions.Inventory.RecalcCost)]
    public async Task<ActionResult<ApiResponse<RecalculateCostResult>>> Recalculate(
        [FromBody] RecalculateCostRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.RecalculateAsync(request, ct));
    }
}
