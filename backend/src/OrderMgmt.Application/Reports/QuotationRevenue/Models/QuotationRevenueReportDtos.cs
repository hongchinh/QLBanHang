using OrderMgmt.Application.Sales.Quotations.Models;

namespace OrderMgmt.Application.Reports.QuotationRevenue.Models;

public sealed class QuotationRevenueSummaryDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public DateOnly PrevFrom { get; init; }
    public DateOnly PrevTo { get; init; }
    public KpiDto RangeRevenue { get; init; } = default!;
    public KpiDto TotalCount { get; init; } = default!;
    public KpiDto CancelledCount { get; init; } = default!;
}

public sealed class QuotationRevenueSeriesDto
{
    public IReadOnlyList<QuotationRevenuePointDto> Points { get; init; } = Array.Empty<QuotationRevenuePointDto>();
}

public sealed class QuotationRevenuePointDto
{
    public DateOnly Date { get; init; }
    public decimal Total { get; init; }
    public int ConfirmedCount { get; init; }
}

public sealed class QuotationTopCustomerDto
{
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    public decimal Revenue { get; init; }
    public int QuotationCount { get; init; }
}
