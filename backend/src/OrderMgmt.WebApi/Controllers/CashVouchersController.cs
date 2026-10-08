using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.CashVouchers.Interfaces;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Common.Models;

namespace OrderMgmt.WebApi.Controllers;

/// Type-specific permissions (receipts.* / payments.*) are checked by the service.
[Route("api/cash-vouchers")]
[Authorize]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
public class CashVouchersController : ApiControllerBase
{
    private readonly ICashVoucherService _service;

    public CashVouchersController(ICashVoucherService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Create(
        [FromBody] UpsertCashVoucherRequest request, CancellationToken ct)
        => Success(await _service.CreateAsync(request, ct));
}
