using FluentAssertions;
using OrderMgmt.Application.Payments.Services;
using OrderMgmt.Infrastructure.Qr;
using SkiaSharp;
using Xunit;
using ZXing;
using ZXing.SkiaSharp;

namespace OrderMgmt.IntegrationTests.Tt133.Unit.Vouchers;

public class QrPngRendererTests
{
    private static readonly string Payload =
        VietQrPayloadBuilder.Build("970436", "0123456789", "CONG TY ABC", 1_500_000m, "PT000125");

    private static Result Decode(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        var reader = new BarcodeReader { AutoRotate = false, Options = { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
        return reader.Decode(bitmap);
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
