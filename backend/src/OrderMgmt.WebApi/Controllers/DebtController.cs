using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.CashDebt.Debt.Interfaces;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Dashboards.Executive.Realtime;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;
using OrderMgmt.WebApi.Filters;

namespace OrderMgmt.WebApi.Controllers;

/// Open items, partner search and the partner summary check their permissions (any of several codes) in the service;
/// the settlement endpoints need debt.matching.
[Route("api/debt")]
[Authorize]
[DashboardSignal(DashboardDomains.Debt)]
public class DebtController : ApiControllerBase
{
    private readonly IDebtService _service;
    private readonly IValidator<OpenItemsRequest> _openItemsValidator;
    private readonly IValidator<DebtPartnerSearchRequest> _partnerSearchValidator;
    private readonly IValidator<SettlementListRequest> _settlementListValidator;
    private readonly IValidator<CreateSettlementBatchRequest> _createBatchValidator;

    public DebtController(IDebtService service, IValidator<OpenItemsRequest> openItemsValidator,
        IValidator<DebtPartnerSearchRequest> partnerSearchValidator,
        IValidator<SettlementListRequest> settlementListValidator,
        IValidator<CreateSettlementBatchRequest> createBatchValidator)
    {
        _service = service;
        _openItemsValidator = openItemsValidator;
        _partnerSearchValidator = partnerSearchValidator;
        _settlementListValidator = settlementListValidator;
        _createBatchValidator = createBatchValidator;
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

    [HttpGet("settlements")]
    [HasPermission(Permissions.Debt.Matching)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SettlementDto>>>> ListSettlements(
        [FromQuery] SettlementListRequest request, CancellationToken ct)
    {
        await _settlementListValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.ListSettlementsAsync(request, ct));
    }

    [HttpPost("settlements")]
    [HasPermission(Permissions.Debt.Matching)]
    public async Task<ActionResult<ApiResponse<SettlementBatchDto>>> CreateSettlementBatch(
        [FromBody] CreateSettlementBatchRequest request, CancellationToken ct)
    {
        await _createBatchValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _service.CreateSettlementBatchAsync(request, ct));
    }

    [HttpDelete("settlements/{id:guid}")]
    [HasPermission(Permissions.Debt.Matching)]
    public async Task<ActionResult<ApiResponse>> DeleteSettlement(Guid id, CancellationToken ct)
    {
        await _service.DeleteSettlementAsync(id, ct);
        return Success();
    }

    [HttpDelete("settlement-batches/{batchId:guid}")]
    [HasPermission(Permissions.Debt.Matching)]
    public async Task<ActionResult<ApiResponse>> DeleteSettlementBatch(Guid batchId, CancellationToken ct)
    {
        await _service.DeleteSettlementBatchAsync(batchId, ct);
        return Success();
    }
}
