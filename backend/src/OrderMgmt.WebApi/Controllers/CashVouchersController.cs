using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.CashDebt.CashVouchers.Interfaces;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Vouchers;
using OrderMgmt.Domain.Enums;
using OrderMgmt.WebApi.Filters;

namespace OrderMgmt.WebApi.Controllers;

/// Type-specific permissions (receipts.* / payments.*) are checked by the service.
[Route("api/cash-vouchers")]
[Authorize]
[DashboardSignal(DashboardDomains.Cash | DashboardDomains.Debt)]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
public class CashVouchersController : ApiControllerBase
{
    private readonly ICashVoucherService _service;
    private readonly IValidator<UpsertCashVoucherRequest> _upsertValidator;
    private readonly IValidator<CashVoucherListRequest> _listValidator;
    private readonly IVoucherPrintService _print;

    public CashVouchersController(ICashVoucherService service, IValidator<UpsertCashVoucherRequest> upsertValidator,
        IValidator<CashVoucherListRequest> listValidator, IVoucherPrintService print)
    {
        _service = service;
        _upsertValidator = upsertValidator;
        _listValidator = listValidator;
        _print = print;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<CashVoucherListResult>>> List(
        [FromQuery] CashVoucherListRequest request, CancellationToken ct)
    {
        await _listValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.ListAsync(request, ct));
    }

    /// 01-TT / 02-TT of one voucher as Excel or PDF.
    [HttpGet("{id:guid}/print")]
    public async Task<IActionResult> Print(Guid id, [FromQuery] ExportFormat format, CancellationToken ct)
    {
        var file = await _print.PrintCashVouchersAsync(new[] { id }, format, ct);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    /// Several vouchers, one sheet each. A POST because a list of ids does not fit a URL; it changes nothing.
    [HttpPost("print")]
    [SkipDashboardSignal]
    public async Task<IActionResult> PrintMany(
        [FromBody] CashVoucherPrintRequest request, [FromQuery] ExportFormat format, CancellationToken ct)
    {
        var file = await _print.PrintCashVouchersAsync(request.Ids ?? [], format, ct);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpGet("defaults")]
    public async Task<ActionResult<ApiResponse<CashVoucherDefaultsDto>>> GetDefaults(
        [FromQuery] CashVoucherType type, CancellationToken ct)
        => Success(await _service.GetDefaultsAsync(type, ct));

    [HttpGet("partners")]
    public async Task<ActionResult<ApiResponse<List<CustomerSearchItemDto>>>> SearchPartners(
        [FromQuery] CashVoucherType type,
        [FromQuery] string? keyword = null,
        [FromQuery] int limit = 20,
        [FromQuery] Guid? reasonId = null,
        [FromQuery] bool? activeOnly = null,
        CancellationToken ct = default)
        => Success(await _service.SearchPartnersAsync(type, keyword, limit, reasonId, activeOnly, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CashVoucherDto>>> Get(Guid id, CancellationToken ct)
        => Success(await _service.GetAsync(id, ct));

    [HttpGet("{id:guid}/activities")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashDocumentActivityDto>>>> ListActivities(
        Guid id, CancellationToken ct)
        => Success(await _service.ListActivitiesAsync(id, ct));

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
