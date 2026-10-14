using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

/// Funds below zero: how many, and Σ |balance|.
public sealed record NegativeFunds(int Count, decimal Amount);

public interface ICashMetricsService
{
    Task<CashSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<CashSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default);
    Task<CashBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);

    /// The funds of the scope below zero at the end of `at`. No permission check: the caller masks.
    Task<NegativeFunds> GetNegativeFundsAsync(ExecutiveScope scope, DateOnly at, CancellationToken ct = default);
}
