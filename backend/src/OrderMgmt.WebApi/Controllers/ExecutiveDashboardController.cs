using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

/// Executive dashboard (Bảng điều hành): read-only. Each action needs dashboard.executive and the report permission
/// of its domain.
[Route("api/dashboard/executive")]
[HasPermission(Permissions.Dashboard.Executive)]
public class ExecutiveDashboardController : ApiControllerBase
{
    private readonly ISalesMetricsService _sales;
    private readonly ICashMetricsService _cash;
    private readonly IDebtMetricsService _debt;
    private readonly IInventoryMetricsService _inventory;
    private readonly IValidator<ExecutiveQuery> _queryValidator;
    private readonly IValidator<ExecutiveSeriesQuery> _seriesValidator;
    private readonly IValidator<SalesTopQuery> _topValidator;

    public ExecutiveDashboardController(ISalesMetricsService sales, ICashMetricsService cash, IDebtMetricsService debt,
        IInventoryMetricsService inventory, IValidator<ExecutiveQuery> queryValidator,
        IValidator<ExecutiveSeriesQuery> seriesValidator, IValidator<SalesTopQuery> topValidator)
    {
        _sales = sales;
        _cash = cash;
        _debt = debt;
        _inventory = inventory;
        _queryValidator = queryValidator;
        _seriesValidator = seriesValidator;
        _topValidator = topValidator;
    }

    [HttpGet("sales/summary")]
    [HasPermission(Permissions.Reports.Profit)]
    public async Task<ActionResult<ApiResponse<SalesSummaryDto>>> SalesSummary(
        [FromQuery] ExecutiveQuery query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _sales.GetSummaryAsync(query, ct));
    }

    [HttpGet("sales/series")]
    [HasPermission(Permissions.Reports.Profit)]
    public async Task<ActionResult<ApiResponse<SalesSeriesDto>>> SalesSeries(
        [FromQuery] ExecutiveSeriesQuery query, CancellationToken ct)
    {
        await _seriesValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _sales.GetSeriesAsync(query, ct));
    }

    [HttpGet("sales/top")]
    [HasPermission(Permissions.Reports.Profit)]
    public async Task<ActionResult<ApiResponse<List<SalesTopRowDto>>>> SalesTop(
        [FromQuery] SalesTopQuery query, CancellationToken ct)
    {
        await _topValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _sales.GetTopAsync(query, ct));
    }

    [HttpGet("cash/summary")]
    [HasPermission(Permissions.Reports.Cash)]
    public async Task<ActionResult<ApiResponse<CashSummaryDto>>> CashSummary(
        [FromQuery] ExecutiveQuery query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _cash.GetSummaryAsync(query, ct));
    }

    [HttpGet("cash/series")]
    [HasPermission(Permissions.Reports.Cash)]
    public async Task<ActionResult<ApiResponse<CashSeriesDto>>> CashSeries(
        [FromQuery] ExecutiveSeriesQuery query, CancellationToken ct)
    {
        await _seriesValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _cash.GetSeriesAsync(query, ct));
    }

    [HttpGet("cash/breakdown")]
    [HasPermission(Permissions.Reports.Cash)]
    public async Task<ActionResult<ApiResponse<CashBreakdownDto>>> CashBreakdown(
        [FromQuery] ExecutiveQuery query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _cash.GetBreakdownAsync(query, ct));
    }

    [HttpGet("debt/summary")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<ActionResult<ApiResponse<DebtSummaryDto>>> DebtSummary(
        [FromQuery] ExecutiveQuery query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _debt.GetSummaryAsync(query, ct));
    }

    [HttpGet("debt/breakdown")]
    [HasPermission(Permissions.Reports.Debt)]
    public async Task<ActionResult<ApiResponse<DebtBreakdownDto>>> DebtBreakdown(
        [FromQuery] ExecutiveQuery query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _debt.GetBreakdownAsync(query, ct));
    }

    [HttpGet("inventory/summary")]
    [HasPermission(Permissions.Reports.Inventory)]
    public async Task<ActionResult<ApiResponse<InventorySummaryDto>>> InventorySummary(
        [FromQuery] ExecutiveQuery query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _inventory.GetSummaryAsync(query, ct));
    }

    [HttpGet("inventory/breakdown")]
    [HasPermission(Permissions.Reports.Inventory)]
    public async Task<ActionResult<ApiResponse<InventoryBreakdownDto>>> InventoryBreakdown(
        [FromQuery] ExecutiveQuery query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);
        return Success(await _inventory.GetBreakdownAsync(query, ct));
    }
}
