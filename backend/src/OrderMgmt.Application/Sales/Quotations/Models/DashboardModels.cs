namespace OrderMgmt.Application.Sales.Quotations.Models;

public sealed class DashboardSummaryDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public DateOnly PrevFrom { get; init; }
    public DateOnly PrevTo { get; init; }
    public KpiDto TodayRevenue { get; init; } = default!;
    public KpiDto RangeRevenue { get; init; } = default!;
    public KpiDto TotalCount { get; init; } = default!;
    public KpiDto CancelledCount { get; init; } = default!;
    public FunnelDto Funnel { get; init; } = default!;
}

public sealed class KpiDto
{
    public decimal Value { get; init; }
    public decimal? DeltaPct { get; init; }
    public IReadOnlyList<decimal> Spark { get; init; } = Array.Empty<decimal>();
}

public sealed class FunnelDto
{
    public int Draft { get; init; }
    public int Sent { get; init; }
    public int Confirmed { get; init; }
    public int Cancelled { get; init; }
    public decimal? SentRate { get; init; }
    public decimal? ConfirmRate { get; init; }
}

public sealed class RevenueSeriesDto
{
    public IReadOnlyList<RevenuePointDto> Points { get; init; } = Array.Empty<RevenuePointDto>();
}

public sealed class RevenuePointDto
{
    public DateOnly Date { get; init; }
    public decimal Total { get; init; }
    public int VoucherCount { get; init; }
}

public sealed class TopCustomerDto
{
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    public decimal Revenue { get; init; }
    public int VoucherCount { get; init; }
}

public sealed class TopProductDto
{
    public Guid? ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public decimal Revenue { get; init; }
    public decimal Quantity { get; init; }
}

public sealed class ActivityItemDto
{
    public DateTime At { get; init; }
    public string Type { get; init; } = default!;
    public Guid QuotationId { get; init; }
    public string Code { get; init; } = default!;
    public string CustomerName { get; init; } = default!;
    public string? ActorName { get; init; }
    public decimal? Amount { get; init; }
}

public sealed class SalesLeaderboardItemDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = default!;
    public decimal Revenue { get; init; }
    public int ConfirmedCount { get; init; }
    public decimal? ConversionRate { get; init; }
    public decimal? DeltaPct { get; init; }
}
