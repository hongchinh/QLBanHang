using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Reports.QuotationRevenue.Interfaces;
using OrderMgmt.Application.Reports.QuotationRevenue.Models;
using OrderMgmt.Application.Reports.SalesRevenue.Interfaces;
using OrderMgmt.Application.Reports.SalesRevenue.Models;
using OrderMgmt.Application.Reports.VehicleRevenue.Interfaces;
using OrderMgmt.Application.Reports.VehicleRevenue.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.WebApi.Authorization;

namespace OrderMgmt.WebApi.Controllers;

public class ReportsController : ApiControllerBase
{
    private readonly ISalesRevenueReportService _salesRevenue;
    private readonly IVehicleRevenueReportService _vehicleRevenue;
    private readonly IQuotationRevenueReportService _quotationRevenue;
    private readonly IValidator<SalesRevenueReportRequest> _salesRevenueValidator;
    private readonly IValidator<VehicleRevenueReportRequest> _vehicleRevenueValidator;
    private readonly IValidator<SalesRevenueLineItemsRequest> _salesRevenueLineItemsValidator;
    private readonly IRevenueReportExcelRenderer _revenueExcelRenderer;

    public ReportsController(
        ISalesRevenueReportService salesRevenue,
        IVehicleRevenueReportService vehicleRevenue,
        IQuotationRevenueReportService quotationRevenue,
        IValidator<SalesRevenueReportRequest> salesRevenueValidator,
        IValidator<VehicleRevenueReportRequest> vehicleRevenueValidator,
        IValidator<SalesRevenueLineItemsRequest> salesRevenueLineItemsValidator,
        IRevenueReportExcelRenderer revenueExcelRenderer)
    {
        _salesRevenue = salesRevenue;
        _vehicleRevenue = vehicleRevenue;
        _quotationRevenue = quotationRevenue;
        _salesRevenueValidator = salesRevenueValidator;
        _vehicleRevenueValidator = vehicleRevenueValidator;
        _salesRevenueLineItemsValidator = salesRevenueLineItemsValidator;
        _revenueExcelRenderer = revenueExcelRenderer;
    }

    [HttpGet("sales-revenue")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<ActionResult<ApiResponse<SalesRevenueReportDto>>> SalesRevenue(
        [FromQuery] SalesRevenueReportRequest request, CancellationToken ct)
    {
        await _salesRevenueValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _salesRevenue.GetAsync(request, ct));
    }

    [HttpGet("sales-revenue/{saleUserId:guid}/lines")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<ActionResult<ApiResponse<List<SalesRevenueLineItemDto>>>> SalesRevenueLines(
        Guid saleUserId,
        [FromQuery] SalesRevenueLineItemsRequest request,
        CancellationToken ct)
    {
        await _salesRevenueLineItemsValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _salesRevenue.GetLineItemsAsync(saleUserId, request, ct));
    }

    [HttpGet("revenue-lines")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<ActionResult<ApiResponse<List<SalesRevenueLineItemDto>>>> RevenueLines(
        [FromQuery] SalesRevenueLineItemsRequest request,
        CancellationToken ct)
    {
        await _salesRevenueLineItemsValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _salesRevenue.GetLineItemsAsync(request, ct));
    }

    [HttpGet("revenue-lines/excel")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<IActionResult> RevenueLineItemsExcel(
        [FromQuery] SalesRevenueLineItemsRequest request,
        CancellationToken ct)
    {
        await _salesRevenueLineItemsValidator.ValidateAndThrowAsync(request, ct);
        var items = await _salesRevenue.GetLineItemsAsync(request, ct);
        var (bytes, fileName) = _revenueExcelRenderer.Render(items, request.From, request.To);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("vehicle-revenue")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<ActionResult<ApiResponse<VehicleRevenueReportDto>>> VehicleRevenue(
        [FromQuery] VehicleRevenueReportRequest request, CancellationToken ct)
    {
        await _vehicleRevenueValidator.ValidateAndThrowAsync(request, ct);
        return Success(await _vehicleRevenue.GetAsync(request, ct));
    }

    [HttpGet("revenue/summary")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<ActionResult<ApiResponse<QuotationRevenueSummaryDto>>> QuotationRevenueSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? saleUserId,
        CancellationToken ct)
        => Success(await _quotationRevenue.GetSummaryAsync(from, to, saleUserId, ct));

    [HttpGet("revenue/series")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<ActionResult<ApiResponse<QuotationRevenueSeriesDto>>> QuotationRevenueSeries(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string granularity = "day",
        [FromQuery] Guid? saleUserId = null,
        CancellationToken ct = default)
        => Success(await _quotationRevenue.GetSeriesAsync(from, to, granularity, saleUserId, ct));

    [HttpGet("revenue/top-customers")]
    [HasPermission(Permissions.Reports.Revenue)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QuotationTopCustomerDto>>>> QuotationRevenueTopCustomers(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int limit = 5,
        [FromQuery] Guid? saleUserId = null,
        CancellationToken ct = default)
        => Success(await _quotationRevenue.GetTopCustomersAsync(from, to, limit, saleUserId, ct));
}
