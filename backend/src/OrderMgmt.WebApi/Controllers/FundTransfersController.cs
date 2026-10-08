using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.FundTransfers.Interfaces;
using OrderMgmt.Application.CashDebt.FundTransfers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

/// One document type, so the fund_transfers.* permissions are static guards; edit_all is checked by the service.
[Route("api/fund-transfers")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
public class FundTransfersController : ApiControllerBase
{
    private readonly IFundTransferService _service;
    private readonly IValidator<UpsertFundTransferRequest> _upsertValidator;

    public FundTransfersController(IFundTransferService service, IValidator<UpsertFundTransferRequest> upsertValidator)
    {
        _service = service;
        _upsertValidator = upsertValidator;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.FundTransfers.View)]
    public async Task<ActionResult<ApiResponse<FundTransferDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.FundTransfers.Create)]
    public async Task<ActionResult<ApiResponse<FundTransferDto>>> Create(
        [FromBody] UpsertFundTransferRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.FundTransfers.Edit)]
    public async Task<ActionResult<ApiResponse<FundTransferDto>>> Update(
        Guid id, [FromBody] UpsertFundTransferRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.UpdateAsync(id, request, ct));
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.FundTransfers.Cancel)]
    public async Task<ActionResult<ApiResponse<FundTransferDto>>> Cancel(
        Guid id, [FromBody] CashDocumentActionRequest request, CancellationToken ct)
        => Success(await _service.CancelAsync(id, request, ct));

    [HttpPost("{id:guid}/restore")]
    [HasPermission(Permissions.FundTransfers.Cancel)]
    public async Task<ActionResult<ApiResponse<FundTransferDto>>> Restore(
        Guid id, [FromBody] CashDocumentActionRequest request, CancellationToken ct)
        => Success(await _service.RestoreAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.FundTransfers.Delete)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid id, [FromQuery] CashDocumentActionRequest request, CancellationToken ct)
    {
        await _service.DeleteAsync(id, request, ct);
        return Success();
    }
}
