using System.Globalization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Common;

public sealed record OpenEntry(Guid EntryId, Guid BranchId, Guid PartnerId, DebtSide Side,
    DebtDirection Direction, decimal Open, DateTimeOffset PostedAt);

public sealed record AllocationInput(Guid EntryId, decimal Amount);

/// Pure validation of a voucher's allocations against open debt entries (document matching).
public static class AllocationValidator
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// Validates allocations of a voucher's own entry (branch, partner, side, ownDirection) against open entries.
    /// capacity = own entry Amount − Σ settlements of the own entry that are kept (Manual/Offset/other).
    /// Returns camelCase error keys under `keyPrefix` ("allocations[0].amount", "allocations"); empty when valid.
    public static Dictionary<string, string[]> Validate(
        Guid branchId, Guid partnerId, DebtSide side, DebtDirection ownDirection, decimal capacity,
        IReadOnlyList<AllocationInput> allocations, IReadOnlyDictionary<Guid, OpenEntry> entries,
        string keyPrefix = "allocations")
    {
        var errors = new Dictionary<string, string[]>();
        var seen = new HashSet<Guid>();

        for (var i = 0; i < allocations.Count; i++)
        {
            var allocation = allocations[i];
            entries.TryGetValue(allocation.EntryId, out var entry);

            var amountError = allocation.Amount <= 0
                ? "Số tiền phân bổ phải lớn hơn 0."
                : entry is not null && allocation.Amount > entry.Open
                    ? $"Vượt số còn lại {entry.Open.ToString("N0", Vi)}."
                    : null;
            if (amountError is not null)
                errors[$"{keyPrefix}[{i}].amount"] = new[] { amountError };

            var isDuplicate = !seen.Add(allocation.EntryId);
            var entryError = entry is null
                ? "Chứng từ không tồn tại."
                : entry.BranchId != branchId || entry.PartnerId != partnerId || entry.Side != side
                    ? "Chứng từ không thuộc đối tượng/phía này."
                    : entry.Direction == ownDirection
                        ? "Chứng từ không đối trừ được với phiếu này."
                        : isDuplicate
                            ? "Chứng từ bị chọn hai lần."
                            : null;
            if (entryError is not null)
                errors[$"{keyPrefix}[{i}].entryId"] = new[] { entryError };
        }

        if (allocations.Sum(a => a.Amount) > capacity)
            errors[keyPrefix] = new[]
            {
                $"Tổng phân bổ vượt số tiền còn lại của phiếu ({capacity.ToString("N0", Vi)}).",
            };

        return errors;
    }
}
