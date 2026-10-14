namespace OrderMgmt.Application.Common.Interfaces;

public interface ISpreadsheetPdfConverter
{
    Task<byte[]> ConvertAsync(byte[] xlsxBytes, CancellationToken ct = default);
}
