using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Interfaces;
using OrderMgmt.Application.Inventory.StockVouchers.Models;

namespace OrderMgmt.WebApi.Controllers;

/// Type-specific permissions (stock_in.* / stock_out.*) are checked by the service.
[Route("api/stock-vouchers")]
[Authorize]
public class StockVouchersController : ApiControllerBase
{
    private readonly IStockVoucherService _service;

    public StockVouchersController(IStockVoucherService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StockVoucherDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<StockVoucherDto>>> Create(
        [FromBody] UpsertStockVoucherRequest request, CancellationToken ct)
        => Success(await _service.CreateAsync(request, ct));
}
