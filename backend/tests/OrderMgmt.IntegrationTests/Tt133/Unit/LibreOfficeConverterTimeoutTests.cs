using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using OrderMgmt.Infrastructure.Excel;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit;

public class LibreOfficeConverterTimeoutTests
{
    [Fact]
    public async Task Timeout_kills_the_process()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"qlbh_slow_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var marker = Path.Combine(folder, "marker.txt");
            string script;
            if (OperatingSystem.IsWindows())
            {
                script = Path.Combine(folder, "slow.cmd");
                await File.WriteAllTextAsync(script,
                    "@echo off\r\nping -n 6 127.0.0.1 > nul\r\n" + $"echo done > \"{marker}\"\r\n");
            }
            else
            {
                script = Path.Combine(folder, "slow.sh");
                await File.WriteAllTextAsync(script,
                    "#!/bin/sh\nsleep 5\n" + $"echo done > \"{marker}\"\n");
                File.SetUnixFileMode(script,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var converter = new LibreOfficeSpreadsheetPdfConverter(Options.Create(
                new QuotationExportOptions { LibreOfficePath = script, ConversionTimeoutSeconds = 1 }));

            var sw = Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => converter.ConvertAsync(new byte[] { 1 }));
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));

            await Task.Delay(TimeSpan.FromSeconds(7));
            File.Exists(marker).Should().BeFalse("the process tree was killed before it could write the marker");
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
