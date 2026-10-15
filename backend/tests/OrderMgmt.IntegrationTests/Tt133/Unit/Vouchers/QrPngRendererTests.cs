using FluentAssertions;
using OrderMgmt.Application.Payments.Services;
using OrderMgmt.Infrastructure.Qr;
using SkiaSharp;
using Xunit;
using ZXing;
using ZXing.Common;

namespace OrderMgmt.IntegrationTests.Tt133.Unit.Vouchers;

public class QrPngRendererTests
{
    private static readonly string Payload =
        VietQrPayloadBuilder.Build("970436", "0123456789", "CONG TY ABC", 1_500_000m, "PT000125");

    private static Result Decode(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        var gray = new byte[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                gray[y * bitmap.Width + x] = (byte)((pixel.Red * 299 + pixel.Green * 587 + pixel.Blue * 114) / 1000);
            }
        var source = new RGBLuminanceSource(gray, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] },
        };
        return reader.Decode(source);
    }

    [Fact]
    public void Png_decodes_back_to_the_payload()
    {
        var result = Decode(QrPngRenderer.Render(Payload));

        result.Should().NotBeNull();
        result.Text.Should().Be(Payload);
    }

    [Fact]
    public void Png_is_square_with_whole_modules()
    {
        var png = QrPngRenderer.Render(Payload, 6);

        using var bitmap = SKBitmap.Decode(png);
        bitmap.Width.Should().Be(bitmap.Height);
        (bitmap.Width % 6).Should().Be(0);
        for (var x = 0; x < bitmap.Width; x++)
            bitmap.GetPixel(x, 0).Should().Be(SKColors.White, $"pixel {x} of the first row is in the quiet zone");
    }

    [Fact]
    public void Error_correction_is_M()
    {
        var result = Decode(QrPngRenderer.Render(Payload));

        result.Should().NotBeNull();
        result.ResultMetadata[ResultMetadataType.ERROR_CORRECTION_LEVEL].Should().Be("M");
    }
}
