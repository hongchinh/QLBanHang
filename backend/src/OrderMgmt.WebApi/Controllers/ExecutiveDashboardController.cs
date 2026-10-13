using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

/// Executive dashboard (Bảng điều hành): read-only. Each action also needs the report permission of its domain (P9).
[Route("api/dashboard/executive")]
[HasPermission(Permissions.Dashboard.Executive)]
public class ExecutiveDashboardController : ApiControllerBase
{
    private readonly ISalesMetricsService _sales;
    private readonly IValidator<ExecutiveQuery> _queryValidator;
    private readonly IValidator<ExecutiveSeriesQuery> _seriesValidator;

    public ExecutiveDashboardController(ISalesMetricsService sales, IValidator<ExecutiveQuery> queryValidator,
        IValidator<ExecutiveSeriesQuery> seriesValidator)
    {
        _sales = sales;
        _queryValidator = queryValidator;
        _seriesValidator = seriesValidator;
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
}
