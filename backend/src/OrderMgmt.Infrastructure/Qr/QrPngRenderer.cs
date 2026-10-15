using QRCoder;

namespace OrderMgmt.Infrastructure.Qr;

public static class QrPngRenderer
{
    /// A PNG of the payload: error correction M, the standard 4-module quiet zone.
    public static byte[] Render(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule, drawQuietZones: true);
    }
}
