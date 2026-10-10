using OrderMgmt.Application.CashDebt.Common;

namespace OrderMgmt.Application.CashDebt.Guards;

public sealed record FundCapture(IReadOnlyList<FundCaptureItem> Items);

public sealed record FundCaptureItem(Guid FundId, DateTimeOffset From, RunningBalanceResult Before);

/// Negative-fund check of a save (P17, D31): capture each affected fund before the save, check it after.
/// Callers hold the fund locks (P14) from capture to check.
public interface IFundBalanceGuard
{
    /// Movements per P17: OpeningAmount, then Active non-deleted cash vouchers and fund transfers (FundMovementQuery).
    Task<IReadOnlyList<BalanceMovement>> MovementsAsync(Guid fundId, CancellationToken ct = default);

    /// Same fund listed twice → the earlier From wins.
    Task<FundCapture> CaptureAsync(IEnumerable<(Guid FundId, DateTimeOffset From)> funds, CancellationToken ct = default);

    /// Recomputes each item; RunningBalance.IsWorse + NegativeFundPolicy + LimitPolicyDecision.
    /// Warn → ConfirmationRequiredException("NEGATIVE_FUND_WARNING", "Quỹ sẽ bị âm.", details, true);
    /// Block → ("NEGATIVE_FUND_BLOCKED", "Không đủ tiền trong quỹ.", details, false).
    /// details key = fund Code; value = ["Âm {−min:N0} tại {firstNegativeAt:dd/MM/yyyy HH:mm}"] (VN time).
    Task CheckAsync(FundCapture capture, bool acknowledge, CancellationToken ct = default);
}
