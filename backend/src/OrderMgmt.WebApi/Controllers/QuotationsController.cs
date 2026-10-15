using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;
using OrderMgmt.WebApi.Filters;

namespace OrderMgmt.WebApi.Controllers;

[QuotationSignal]
public class QuotationsController : ApiControllerBase
{
    private readonly IQuotationService _quotations;
    private readonly IValidator<UpsertQuotationRequest> _upsertValidator;
    private readonly IValidator<QuotationListRequest> _listValidator;
    private readonly IValidator<TransitionQuotationRequest> _transitionValidator;

    public QuotationsController(
        IQuotationService quotations,
        IValidator<UpsertQuotationRequest> upsertValidator,
        IValidator<QuotationListRequest> listValidator,
        IValidator<TransitionQuotationRequest> transitionValidator)
    {
        _quotations = quotations;
        _upsertValidator = upsertValidator;
        _listValidator = listValidator;
        _transitionValidator = transitionValidator;
    }

    [HttpGet]
    [HasPermission(Permissions.Quotations.View)]
    public async Task<ActionResult<ApiResponse<QuotationListResult>>> List(
        [FromQuery] QuotationListRequest request, CancellationToken ct)
    {
        await _listValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _quotations.ListAsync(request, ct));
    }

    [HttpGet("owners")]
    [HasPermission(Permissions.Quotations.ViewAll)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuotationOwnerOptionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QuotationOwnerOptionDto>>>> ListOwners(
        [FromQuery] bool includeDeleted, CancellationToken ct)
        => Success(await _quotations.ListOwnersAsync(includeDeleted, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Quotations.View)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _quotations.GetAsync(id, ct));

    [HttpGet("{id:guid}/activities")]
    [HasPermission(Permissions.Quotations.View)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<QuotationActivityDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QuotationActivityDto>>>> ListActivities(
        Guid id, CancellationToken ct)
        => Success(await _quotations.ListActivitiesAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.Quotations.Create)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> Create(
        [FromBody] UpsertQuotationRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _quotations.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Quotations.Update)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> Update(
        Guid id, [FromBody] UpsertQuotationRequest request, CancellationToken ct)
    {
        await _upsertValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _quotations.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Quotations.Delete)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await _quotations.DeleteAsync(id, ct);
        return Success();
    }

    [HttpPost("{id:guid}/transition")]
    [HasPermission(Permissions.Quotations.Update)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> Transition(
        Guid id, [FromBody] TransitionQuotationRequest request, CancellationToken ct)
    {
        await _transitionValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _quotations.TransitionAsync(id, request.Action, ct));
    }

    // Returns raw bytes (not wrapped in ApiResponse) so browsers can save directly.
    [HttpGet("{id:guid}/excel")]
    [HasPermission(Permissions.Quotations.Print)]
    public async Task<IActionResult> Excel(Guid id, CancellationToken ct)
    {
        var (bytes, fileName) = await _quotations.RenderExcelAsync(id, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("{id:guid}/pdf")]
    [HasPermission(Permissions.Quotations.Print)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var (bytes, fileName) = await _quotations.RenderPdfAsync(id, ct);
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("{id:guid}/handover-with-price/excel")]
    [HasPermission(Permissions.Quotations.Print)]
    public async Task<IActionResult> HandoverWithPriceExcel(Guid id, CancellationToken ct)
    {
        var (bytes, fileName) = await _quotations.RenderHandoverExcelAsync(id, withPrice: true, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("{id:guid}/handover-with-price/pdf")]
    [HasPermission(Permissions.Quotations.Print)]
    public async Task<IActionResult> HandoverWithPricePdf(Guid id, CancellationToken ct)
    {
        var (bytes, fileName) = await _quotations.RenderHandoverPdfAsync(id, withPrice: true, ct);
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("{id:guid}/handover-no-price/excel")]
    [HasPermission(Permissions.Quotations.Print)]
    public async Task<IActionResult> HandoverNoPriceExcel(Guid id, CancellationToken ct)
    {
        var (bytes, fileName) = await _quotations.RenderHandoverExcelAsync(id, withPrice: false, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("{id:guid}/handover-no-price/pdf")]
    [HasPermission(Permissions.Quotations.Print)]
    public async Task<IActionResult> HandoverNoPricePdf(Guid id, CancellationToken ct)
    {
        var (bytes, fileName) = await _quotations.RenderHandoverPdfAsync(id, withPrice: false, ct);
        return File(bytes, "application/pdf", fileName);
    }

    [HttpPost("{id:guid}/clone")]
    [HasPermission(Permissions.Quotations.Create)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> Clone(Guid id, CancellationToken ct)
        => Success(await _quotations.CloneAsync(id, ct));

    // Service distinguishes TransferOwn (self) vs TransferAny (others); [Authorize] gates
    // anonymous callers at the HTTP layer and the service rejects callers missing the
    // specific permission with 403.
    [HttpPatch("{id:guid}/owner")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<QuotationDto>>> TransferOwner(
        Guid id,
        [FromBody] TransferOwnerRequest request,
        [FromServices] IValidator<TransferOwnerRequest> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Success(await _quotations.TransferOwnerAsync(id, request, ct));
    }
}
