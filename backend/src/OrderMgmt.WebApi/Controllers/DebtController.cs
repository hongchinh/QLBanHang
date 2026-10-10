using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Debt.Interfaces;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;

namespace OrderMgmt.WebApi.Controllers;

/// Permissions (any of several codes per endpoint) are checked by the service.
[Route("api/debt")]
[Authorize]
public class DebtController : ApiControllerBase
{
    private readonly IDebtService _service;
    private readonly IValidator<OpenItemsRequest> _openItemsValidator;
    private readonly IValidator<DebtPartnerSearchRequest> _partnerSearchValidator;

    public DebtController(IDebtService service, IValidator<OpenItemsRequest> openItemsValidator,
        IValidator<DebtPartnerSearchRequest> partnerSearchValidator)
    {
        _service = service;
        _openItemsValidator = openItemsValidator;
        _partnerSearchValidator = partnerSearchValidator;
    }

    [HttpGet("partner-summary")]
    public async Task<ActionResult<ApiResponse<PartnerDebtSummaryDto>>> PartnerSummary(
        [FromQuery] Guid partnerId, CancellationToken ct)
        => Success(await _service.GetPartnerSummaryAsync(partnerId, ct));

    [HttpGet("open-items")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OpenItemDto>>>> OpenItems(
        [FromQuery] OpenItemsRequest request, CancellationToken ct)
    {
        await _openItemsValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.GetOpenItemsAsync(request, ct));
    }

    [HttpGet("partners")]
    public async Task<ActionResult<ApiResponse<List<CustomerSearchItemDto>>>> SearchPartners(
        [FromQuery] DebtPartnerSearchRequest request, CancellationToken ct)
    {
        await _partnerSearchValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.SearchPartnersAsync(request, ct));
    }
}
