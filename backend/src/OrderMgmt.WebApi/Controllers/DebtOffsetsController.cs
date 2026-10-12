using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.DebtOffsets.Interfaces;
using OrderMgmt.Application.CashDebt.DebtOffsets.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

/// Debt offset vouchers (P23): create, view, cancel and delete; no edit and no restore.
[Route("api/debt-offsets")]
public class DebtOffsetsController : ApiControllerBase
{
    private readonly IDebtOffsetService _service;
    private readonly IValidator<CreateDebtOffsetRequest> _createValidator;
    private readonly IValidator<DebtOffsetListRequest> _listValidator;

    public DebtOffsetsController(IDebtOffsetService service, IValidator<CreateDebtOffsetRequest> createValidator,
        IValidator<DebtOffsetListRequest> listValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _listValidator = listValidator;
    }

    [HttpGet]
    [HasPermission(Permissions.DebtOffsets.View)]
    public async Task<ActionResult<ApiResponse<DebtOffsetListResult>>> List(
        [FromQuery] DebtOffsetListRequest request, CancellationToken ct)
    {
        await _listValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.ListAsync(request, ct));
    }

    [HttpGet("defaults")]
    [HasPermission(Permissions.DebtOffsets.View)]
    public async Task<ActionResult<ApiResponse<DebtOffsetDefaultsDto>>> GetDefaults(CancellationToken ct)
        => Success(await _service.GetDefaultsAsync(ct));

    [HttpGet("{id:guid}/activities")]
    [HasPermission(Permissions.DebtOffsets.View)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashDocumentActivityDto>>>> ListActivities(
        Guid id, CancellationToken ct)
        => Success(await _service.ListActivitiesAsync(id, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.DebtOffsets.View)]
    public async Task<ActionResult<ApiResponse<DebtOffsetDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.DebtOffsets.Create)]
    public async Task<ActionResult<ApiResponse<DebtOffsetDto>>> Create(
        [FromBody] CreateDebtOffsetRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateAsync(request, ct));
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.DebtOffsets.Cancel)]
    public async Task<ActionResult<ApiResponse<DebtOffsetDto>>> Cancel(
        Guid id, [FromBody] CashDocumentActionRequest request, CancellationToken ct)
        => Success(await _service.CancelAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.DebtOffsets.Delete)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid id, [FromQuery] CashDocumentActionRequest request, CancellationToken ct)
    {
        await _service.DeleteAsync(id, request, ct);
        return Success();
    }
}
