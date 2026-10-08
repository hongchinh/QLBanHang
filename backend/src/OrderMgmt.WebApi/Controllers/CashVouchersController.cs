using FluentValidation;
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
    private readonly IValidator<UpsertCashVoucherRequest> _upsertValidator;

    public CashVouchersController(ICashVoucherService service, IValidator<UpsertCashVoucherRequest> upsertValidator)
    {
        _service = service;
        _upsertValidator = upsertValidator;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Create(
        [FromBody] UpsertCashVoucherRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Update(
        Guid id, [FromBody] UpsertCashVoucherRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Cancel(
        Guid id, [FromBody] CashDocumentActionRequest request, CancellationToken ct)
        => Success(await _service.CancelAsync(id, request, ct));

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Restore(
        Guid id, [FromBody] CashDocumentActionRequest request, CancellationToken ct)
        => Success(await _service.RestoreAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid id, [FromQuery] CashDocumentActionRequest request, CancellationToken ct)
    {
        await _service.DeleteAsync(id, request, ct);
        return Success();
    }
}
