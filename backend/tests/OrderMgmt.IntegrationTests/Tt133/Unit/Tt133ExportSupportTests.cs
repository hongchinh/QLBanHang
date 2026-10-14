using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Tt133;
using OrderMgmt.Application.Tt133.Models;
using OrderMgmt.Application.Tt133.Services;
using OrderMgmt.Domain.Common;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133.Unit;

public class Tt133ExportSupportTests
{
    private static Tt133ExportSupport Create(FakeConverter converter) =>
        new(null!, null!, null!, null!, converter, Options.Create(new Tt133ExportOptions { MaxSheets = 2 }),
            NullLogger<Tt133ExportSupport>.Instance);

    [Fact]
    public void Zero_sheets_is_no_targets()
    {
        var act = () => Create(new FakeConverter()).EnsureSheetCount(0, (n, max) => "x");

        var ex = act.Should().Throw<UnprocessableException>().Which;
        ex.Code.Should().Be("EXPORT_NO_TARGETS");
        ex.Message.Should().Be("Không có đối tượng nào để xuất.");
    }

    [Fact]
    public void Too_many_sheets_uses_the_callers_message()
    {
        var support = Create(new FakeConverter());

        var act = () => support.EnsureSheetCount(3, (n, max) => $"{n}>{max}");

        var ex = act.Should().Throw<UnprocessableException>().Which;
        ex.Code.Should().Be("EXPORT_TOO_MANY_SHEETS");
        ex.Message.Should().Be("3>2");
        support.EnsureSheetCount(2, (n, max) => $"{n}>{max}");
    }

    [Fact]
    public async Task Xlsx_is_returned_as_is()
    {
        var converter = new FakeConverter();

        var file = await Create(converter).ToFileAsync([1, 2, 3], "A", ExportFormat.Xlsx, default);

        file.Bytes.Should().Equal(1, 2, 3);
        file.ContentType.Should().Be(Tt133ExportSupport.XlsxContentType);
        file.FileName.Should().Be("A.xlsx");
        converter.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Pdf_is_converted()
    {
        var file = await Create(new FakeConverter { Result = [9] }).ToFileAsync([1, 2, 3], "A", ExportFormat.Pdf, default);

        file.Bytes.Should().Equal(9);
        file.ContentType.Should().Be("application/pdf");
        file.FileName.Should().Be("A.pdf");
    }

    [Fact]
    public async Task Pdf_failure_is_503()
    {
        var support = Create(new FakeConverter { Failure = new InvalidOperationException("boom") });

        var act = () => support.ToFileAsync([1], "A", ExportFormat.Pdf, default);

        var ex = (await act.Should().ThrowAsync<ServiceUnavailableException>()).Which;
        ex.Code.Should().Be("PDF_EXPORT_FAILED");
        ex.Message.Should().Be("Xuất PDF thất bại, hãy dùng Excel.");
    }

    private sealed class FakeConverter : ISpreadsheetPdfConverter
    {
        public byte[] Result { get; init; } = [];
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }

        public Task<byte[]> ConvertAsync(byte[] xlsxBytes, CancellationToken ct = default)
        {
            Calls++;
            if (Failure is not null) throw Failure;
            return Task.FromResult(Result);
        }
    }
}
