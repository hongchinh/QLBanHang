using Microsoft.Extensions.Options;
using OrderMgmt.Application.Common.Interfaces;

namespace OrderMgmt.Infrastructure.Excel;

public class LibreOfficeSpreadsheetPdfConverter : ISpreadsheetPdfConverter
{
    private readonly IOptions<QuotationExportOptions> _options;

    public LibreOfficeSpreadsheetPdfConverter(IOptions<QuotationExportOptions> options)
        => _options = options;

    public async Task<byte[]> ConvertAsync(byte[] xlsxBytes, CancellationToken ct = default)
    {
        var opts = _options.Value;

        if (string.IsNullOrWhiteSpace(opts.LibreOfficePath))
            throw new InvalidOperationException(
                "LibreOffice executable path is not configured. Set QuotationExport:LibreOfficePath.");

        var tmpDir = Path.Combine(Path.GetTempPath(), $"qldh_pdf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var xlsxPath = Path.Combine(tmpDir, "input.xlsx");
            await File.WriteAllBytesAsync(xlsxPath, xlsxBytes, ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(opts.ConversionTimeoutSeconds));

            // One profile per conversion: conversions that run at the same time fail on a shared profile.
            var profileUri = new Uri(Path.Combine(tmpDir, "profile")).AbsoluteUri;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = opts.LibreOfficePath,
                Arguments = $"\"-env:UserInstallation={profileUri}\" --headless --convert-to pdf --outdir \"{tmpDir}\" \"{xlsxPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start LibreOffice process.");

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout or request abort: do not leave soffice running.
                try { process.Kill(entireProcessTree: true); } catch { /* the process may have just exited */ }
                throw;
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                throw new InvalidOperationException(
                    $"LibreOffice conversion failed (exit {process.ExitCode}): {stderr}");
            }

            var pdfPath = Path.Combine(tmpDir, "input.pdf");
            if (!File.Exists(pdfPath))
                throw new InvalidOperationException(
                    "LibreOffice did not produce a PDF output file.");

            return await File.ReadAllBytesAsync(pdfPath, ct);
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
