using FluentValidation;
using Microsoft.AspNetCore.Mvc;
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
}
