using System.Text;
using System.Text.RegularExpressions;

namespace OrderMgmt.Application.Payments.Services;

public static class VietQrPayloadBuilder
{
    public static string ComputeCrc16(string input)
    {
        const ushort polynomial = 0x1021;
        ushort crc = 0xFFFF;

        foreach (var b in Encoding.ASCII.GetBytes(input))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ polynomial)
                    : (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4");
    }

    public static string Build(
        string bankBin,
        string accountNumber,
        string accountName,
        decimal amount,
        string? content)
    {
        var merchantAccountNested =
            Tlv("00", "A000000727") +
            Tlv("01", Tlv("00", bankBin) + Tlv("01", accountNumber)) +
            Tlv("02", "QRIBFTTA");
        var tag38 = Tlv("38", merchantAccountNested);

        var amountString = ((long)amount).ToString();

        var normalizedContent = content is null ? string.Empty : NormalizeAscii(content, 25);
        var tag62 = normalizedContent.Length == 0
            ? string.Empty
            : Tlv("62", Tlv("08", normalizedContent));

        var withoutCrc =
            Tlv("00", "01") +
            Tlv("01", "12") +
            tag38 +
            Tlv("52", "0000") +
            Tlv("53", "704") +
            Tlv("54", amountString) +
            Tlv("58", "VN") +
            Tlv("59", NormalizeAscii(accountName, 25)) +
            Tlv("60", "VIETNAM") +
            tag62 +
            "6304";

        return withoutCrc + ComputeCrc16(withoutCrc);
    }

    private static string Tlv(string tag, string value)
    {
        var byteLength = Encoding.UTF8.GetByteCount(value);
        if (byteLength > 99)
            throw new ArgumentException($"Value for tag '{tag}' exceeds the 99-byte TLV length limit.", nameof(value));

        return tag + byteLength.ToString("00") + value;
    }

    private static string NormalizeAscii(string input, int maxLength)
    {
        // Đ/đ has no Unicode decomposition (it's a distinct base letter, not base+combining
        // mark), so plain NFD wouldn't reduce it to "D" like it does for other Vietnamese
        // diacritics — map it explicitly before normalizing.
        var withoutDStroke = input.Replace('Đ', 'D').Replace('đ', 'd');
        var formD = withoutDStroke.Normalize(NormalizationForm.FormD);
        var withoutDiacritics = Regex.Replace(formD, @"\p{Mn}", string.Empty);
        var upper = withoutDiacritics.ToUpperInvariant();
        var filtered = Regex.Replace(upper, @"[^A-Z0-9 .,\-/]", string.Empty);
        return filtered.Length > maxLength ? filtered[..maxLength] : filtered;
    }
}
