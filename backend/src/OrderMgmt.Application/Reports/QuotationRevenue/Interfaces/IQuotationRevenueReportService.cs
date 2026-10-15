using OrderMgmt.Application.Reports.QuotationRevenue.Models;

namespace OrderMgmt.Application.Reports.QuotationRevenue.Interfaces;

/// Revenue of confirmed quotations for the Doanh thu report: the rule, the "Ngày ghi nhận doanh thu" setting and the
/// owner scope of the old overview dashboard, moved here unchanged.
public interface IQuotationRevenueReportService
{
    Task<QuotationRevenueSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, Guid? saleUserId, CancellationToken ct);
    Task<QuotationRevenueSeriesDto> GetSeriesAsync(DateOnly from, DateOnly to, string granularity, Guid? saleUserId, CancellationToken ct);
    Task<IReadOnlyList<QuotationTopCustomerDto>> GetTopCustomersAsync(DateOnly from, DateOnly to, int limit, Guid? saleUserId, CancellationToken ct);
}
