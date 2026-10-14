using System.Globalization;
using OrderMgmt.Application.Inventory.Common;

namespace OrderMgmt.Application.Tt133.Vouchers;

/// <summary>Pure text rules of the voucher print forms.</summary>
public static class VoucherPrintRules
{
    public const string Dots = "……………………";

    public static string? FirstFilled(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    public static string? CashReason(string? description, IReadOnlyList<string> lineDescriptions, string? reasonName)
    {
        var lines = lineDescriptions.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
        return FirstFilled(description, lines.Count > 0 ? string.Join("; ", lines) : null, reasonName);
    }

    public static string AttachedDocsLine(string? attachedDocs)
    {
        var text = attachedDocs?.Trim();
        if (string.IsNullOrEmpty(text)) return $"Kèm theo: {Dots} chứng từ gốc";
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? $"Kèm theo: {n.ToString(CultureInfo.InvariantCulture)} chứng từ gốc"
            : $"Kèm theo: {text}";
    }

    public static string Money(decimal amount)
        => Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"));

    public static string BatchFileBase(string prefix, DateTimeOffset utcNow)
        => $"{prefix}_{utcNow.ToOffset(VnTime.Offset).ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}";
}
