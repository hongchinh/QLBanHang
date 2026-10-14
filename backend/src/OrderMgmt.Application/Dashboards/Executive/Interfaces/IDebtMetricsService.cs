using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Models;

namespace OrderMgmt.Application.Dashboards.Executive.Interfaces;

/// The debt items of the attention list: overdue receivables (customers, amount), credit limits exceeded (customer ×
/// branch pairs, Σ excess) and payables due within seven days (documents, amount).
public sealed record DebtAttention(int OverduePartners, decimal OverdueAmount, int OverLimitCount, decimal OverLimitAmount,
    int PayablesDueSoonCount, decimal PayablesDueSoonAmount);

public interface IDebtMetricsService
{
    Task<DebtSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<DebtBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);

    /// The receivable balance per branch of the scope at the end of `at`. No permission check: the caller masks.
    Task<IReadOnlyDictionary<Guid, decimal>> GetReceivableByBranchAsync(ExecutiveScope scope, DateOnly at,
        CancellationToken ct = default);

    /// The debt items of the attention list at the end of `at`. No permission check: the caller masks.
    Task<DebtAttention> GetAttentionAsync(ExecutiveScope scope, DateOnly at, CancellationToken ct = default);
}
