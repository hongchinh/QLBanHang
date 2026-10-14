using System.Globalization;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;

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

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Product name; a non-PerUnit line adds the set dimensions (mm) and the sheet count.</summary>
    public static string LineName(string productName, PricingMode mode, decimal? length, decimal? width,
        decimal? thickness, decimal? sheetCount)
    {
        if (mode == PricingMode.PerUnit) return productName;
        string Num(decimal v) => v.ToString("0.###", Vi);
        var dims = new[] { length, width, thickness }.Where(v => v is > 0).Select(v => Num(v!.Value)).ToList();
        var parts = new List<string>();
        if (dims.Count > 0) parts.Add($"{string.Join("×", dims)} mm");
        if (sheetCount is > 0) parts.Add($"{Num(sheetCount.Value)} tấm");
        return parts.Count == 0 ? productName : $"{productName} ({string.Join(", ", parts)})";
    }

    /// <summary>Rows under a stock voucher table in the "Voucher" price mode, from the stored totals.</summary>
    public static IReadOnlyList<StockVoucherPrintTotal> VoucherTotals(decimal goodsAmount, decimal lineDiscountTotal,
        decimal orderDiscount, decimal freight, decimal total)
    {
        var discount = lineDiscountTotal + orderDiscount;
        var vat = total - freight - (goodsAmount - discount);
        if (discount == 0 && vat == 0 && freight == 0)
            return new[] { new StockVoucherPrintTotal("Cộng", total) };
        var rows = new List<StockVoucherPrintTotal> { new("Cộng tiền hàng", goodsAmount) };
        if (discount != 0) rows.Add(new("Chiết khấu", discount));
        if (vat != 0) rows.Add(new("Thuế GTGT", vat));
        if (freight != 0) rows.Add(new("Cước vận chuyển", freight));
        rows.Add(new("Tổng cộng", total));
        return rows;
    }
}
