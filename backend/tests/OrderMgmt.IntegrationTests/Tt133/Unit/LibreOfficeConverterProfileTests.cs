using FluentAssertions;
using Microsoft.Extensions.Options;
using OrderMgmt.Infrastructure.Excel;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit;

public class LibreOfficeConverterProfileTests
{
    [Fact]
    public async Task Each_conversion_gets_its_own_profile_directory()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"qlbh_args_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            // The script only records its arguments; it writes no PDF, so the converter throws afterwards.
            var log = Path.Combine(folder, "args.txt");
            string script;
            if (OperatingSystem.IsWindows())
            {
                script = Path.Combine(folder, "args.cmd");
                await File.WriteAllTextAsync(script, "@echo off\r\n" + $"echo %* >> \"{log}\"\r\n");
            }
            else
            {
                script = Path.Combine(folder, "args.sh");
                await File.WriteAllTextAsync(script, "#!/bin/sh\n" + $"echo \"$@\" >> \"{log}\"\n");
                File.SetUnixFileMode(script,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var converter = new LibreOfficeSpreadsheetPdfConverter(Options.Create(
                new QuotationExportOptions { LibreOfficePath = script, ConversionTimeoutSeconds = 10 }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => converter.ConvertAsync(new byte[] { 1 }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => converter.ConvertAsync(new byte[] { 1 }));

            var profiles = (await File.ReadAllLinesAsync(log))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim('"'))
                    .SingleOrDefault(a => a.StartsWith("-env:UserInstallation=file:///")))
                .ToList();

            profiles.Should().HaveCount(2);
            profiles.Should().OnlyContain(p => p != null && p.Contains("qldh_pdf_"));
            profiles.Should().OnlyHaveUniqueItems();
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
