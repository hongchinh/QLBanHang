using System.Text;

namespace OrderMgmt.Application.Common;

/// <summary>Reads a VND amount in Vietnamese words ("Một nghìn năm trăm đồng").</summary>
public static class VndAmountInWords
{
    private static readonly string[] Digits =
        { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

    private const decimal ChunkSize = 1_000_000_000m;

    public static string Read(decimal amount)
    {
        var n = Math.Round(Math.Abs(amount), 0, MidpointRounding.AwayFromZero);
        if (n == 0) return "Không đồng";

        // Chunks of 9 digits, lowest first; chunk i is followed by "tỷ" repeated i times.
        var chunks = new List<int>();
        for (var rest = n; rest > 0; rest = decimal.Floor(rest / ChunkSize))
            chunks.Add((int)(rest % ChunkSize));

        var words = new List<string>();
        var first = true;
        for (var i = chunks.Count - 1; i >= 0; i--)
        {
            var chunk = chunks[i];
            if (chunk == 0) continue;

            foreach (var (value, unit) in new[] { (chunk / 1_000_000, "triệu"), (chunk / 1_000 % 1_000, "nghìn"), (chunk % 1_000, (string?)null) })
            {
                if (value == 0) continue;
                words.Add(ReadGroup(value, full: !first));
                if (unit != null) words.Add(unit);
                first = false;
            }

            for (var k = 0; k < i; k++) words.Add("tỷ");
        }

        var text = string.Join(' ', words);
        return char.ToUpperInvariant(text[0]) + text[1..] + " đồng";
    }

    private static string ReadGroup(int value, bool full)
    {
        int h = value / 100, t = value / 10 % 10, u = value % 10;
        var parts = new List<string>(4);

        var hundredsRead = false;
        if (h > 0 || full)
        {
            parts.Add($"{Digits[h]} trăm");
            hundredsRead = true;
        }

        if (t == 0)
        {
            if (u > 0 && hundredsRead) parts.Add("linh");
        }
        else
        {
            parts.Add(t == 1 ? "mười" : $"{Digits[t]} mươi");
        }

        if (u > 0)
        {
            parts.Add(u == 1 && t >= 2 ? "mốt" : u == 5 && t >= 1 ? "lăm" : Digits[u]);
        }

        return string.Join(' ', parts);
    }
}
