using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.CashDebt.Ledger;

/// The 422 confirmation (P10) of a save, cancel or delete that would remove settlements of other documents.
public static class SettlementRemovalConfirmation
{
    public const string Code = "DEBT_SETTLEMENTS_WILL_BE_REMOVED";

    public static ConfirmationRequiredException Create(IDictionary<string, string[]> details) =>
        new(Code, "Chứng từ đã được đối trừ. Lưu sẽ bỏ các đối trừ sau.", details, canConfirm: true);

    /// One confirmation for two sources saved together (a stock voucher and its automatic voucher): the details of
    /// both, merged by the other document's code.
    public static ConfirmationRequiredException Merge(ConfirmationRequiredException first,
        ConfirmationRequiredException second) =>
        Create(first.Details.Concat(second.Details)
            .GroupBy(d => d.Key)
            .ToDictionary(g => g.Key, g => g.SelectMany(d => d.Value).ToArray()));
}
