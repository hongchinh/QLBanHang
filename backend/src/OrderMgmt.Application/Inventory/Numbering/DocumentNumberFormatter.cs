using System.Globalization;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Numbering;

/// Document number patterns (D13): {KH} prefix, {STT} counter zero-padded to the length (never truncated),
/// {THANG} month (MM), {NAM} year (yyyy). The counter period follows the voucher's VN date.
public static class DocumentNumberFormatter
{
    public const string PrefixToken = "{KH}";
    public const string CounterToken = "{STT}";
    public const string MonthToken = "{THANG}";
    public const string YearToken = "{NAM}";

    public static string Format(string pattern, string prefix, int length, long counter, DateOnly date) =>
        pattern
            .Replace(PrefixToken, prefix)
            .Replace(YearToken, date.Year.ToString("D4", CultureInfo.InvariantCulture))
            .Replace(MonthToken, date.Month.ToString("D2", CultureInfo.InvariantCulture))
            .Replace(CounterToken, counter.ToString(CultureInfo.InvariantCulture).PadLeft(length, '0'));

    public static string PeriodKey(NumberingResetPolicy policy, DateOnly date) => policy switch
    {
        NumberingResetPolicy.Monthly => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        NumberingResetPolicy.Yearly => date.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => string.Empty,
    };

    public static IReadOnlyList<string> Validate(string pattern, int length, NumberingResetPolicy policy)
    {
        var errors = new List<string>();
        pattern ??= string.Empty;

        if (!pattern.Contains(CounterToken))
            errors.Add("Mẫu đánh số phải có {STT}.");
        if (policy == NumberingResetPolicy.Monthly && (!pattern.Contains(MonthToken) || !pattern.Contains(YearToken)))
            errors.Add("Đánh số đặt lại theo tháng: mẫu phải có {THANG} và {NAM}.");
        if (policy == NumberingResetPolicy.Yearly && !pattern.Contains(YearToken))
            errors.Add("Đánh số đặt lại theo năm: mẫu phải có {NAM}.");
        if (length is < 1 or > 10)
            errors.Add("Độ dài số phải từ 1 đến 10.");

        return errors;
    }
}
