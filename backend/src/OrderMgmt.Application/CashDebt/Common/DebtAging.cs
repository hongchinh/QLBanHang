using System.Globalization;

namespace OrderMgmt.Application.CashDebt.Common;

/// Pure bucket-boundary rules for the aging and overdue reports (B5).
public static class DebtAging
{
    public const int MaxBoundaries = 6;
    public static readonly IReadOnlyList<int> DefaultBoundaries = new[] { 30, 60, 90 };

    /// null/blank → DefaultBoundaries. "30,60,90" (spaces allowed) → [30, 60, 90].
    /// null when any part is not an int, a value is ≤ 0, values are not strictly ascending, or there are more than MaxBoundaries.
    public static IReadOnlyList<int>? ParseBoundaries(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return DefaultBoundaries;

        var parts = text.Split(',');
        if (parts.Length > MaxBoundaries) return null;

        var result = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value <= 0
                || (result.Count > 0 && value <= result[^1]))
                return null;
            result.Add(value);
        }
        return result;
    }

    /// 0 when days ≤ b[0]; i when b[i-1] < days ≤ b[i]; b.Count when days > b[^1].
    public static int BucketIndex(int days, IReadOnlyList<int> boundaries)
    {
        for (var i = 0; i < boundaries.Count; i++)
            if (days <= boundaries[i]) return i;
        return boundaries.Count;
    }

    /// One (From, To) per bucket: (firstFrom, b[0]), (b[0]+1, b[1]), …, (b[^1]+1, null).
    public static IReadOnlyList<(int From, int? To)> Ranges(IReadOnlyList<int> boundaries, int firstFrom)
    {
        var ranges = new List<(int From, int? To)>(boundaries.Count + 1);
        for (var i = 0; i < boundaries.Count; i++)
            ranges.Add((i == 0 ? firstFrom : boundaries[i - 1] + 1, boundaries[i]));
        ranges.Add((boundaries[^1] + 1, null));
        return ranges;
    }
}
