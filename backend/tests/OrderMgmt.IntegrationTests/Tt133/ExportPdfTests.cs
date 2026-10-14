using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Tt133;

/// PDF path of the TT133 exports: the converter is replaced, LibreOffice is not needed.
[Collection(nameof(PostgresCollection))]
public class ExportPdfTests
{
    private const string Url = "/api/reports/cash-book/export?kind=Cash&from=2026-10-01&to=2026-10-31&format=pdf";
    private static readonly byte[] FakePdf = "%PDF-1.0 fake\n"u8.ToArray();

    private readonly PostgresFixture _pg;

    public ExportPdfTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Pdf_returns_the_converted_bytes()
    {
        var fake = new RecordingConverter(FakePdf);
        var factory = new WebAppFactoryWithConverter(_pg, fake);
        await ((IAsyncLifetime)factory).InitializeAsync();
        try
        {
            using var client = await AdminClientAsync(factory);

            var response = await client.GetAsync(Url);

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            response.Content.Headers.ContentDisposition!.FileName.Should().Be("S04a-DNN_20261001_20261031.pdf");
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(FakePdf);
            fake.Received.Should().NotBeNull();
            fake.Received!.Take(2).Should().Equal((byte)'P', (byte)'K');
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    [Fact]
    public async Task Pdf_failure_is_503()
    {
        var fake = new RecordingConverter(new InvalidOperationException("LibreOffice executable path is not configured."));
        var factory = new WebAppFactoryWithConverter(_pg, fake);
        await ((IAsyncLifetime)factory).InitializeAsync();
        try
        {
            using var client = await AdminClientAsync(factory);

            var response = await client.GetAsync(Url);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, await response.Content.ReadAsStringAsync());
            var error = (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!;
            error.Code.Should().Be("PDF_EXPORT_FAILED");
            error.Message.Should().Be("Xuất PDF thất bại, hãy dùng Excel.");
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    private static async Task<HttpClient> AdminClientAsync(WebAppFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }
}

file sealed class WebAppFactoryWithConverter : WebAppFactory
{
    private readonly ISpreadsheetPdfConverter _converter;

    public WebAppFactoryWithConverter(PostgresFixture pg, ISpreadsheetPdfConverter converter) : base(pg) => _converter = converter;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISpreadsheetPdfConverter));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddSingleton(_converter);
        });
    }
}

/// Returns the given bytes or throws the given exception, and keeps the workbook it was handed.
file sealed class RecordingConverter : ISpreadsheetPdfConverter
{
    private readonly byte[]? _pdf;
    private readonly Exception? _failure;

    public RecordingConverter(byte[] pdf) => _pdf = pdf;
    public RecordingConverter(Exception failure) => _failure = failure;

    public byte[]? Received { get; private set; }

    public Task<byte[]> ConvertAsync(byte[] xlsxBytes, CancellationToken ct = default)
    {
        Received = xlsxBytes;
        return _failure is null ? Task.FromResult(_pdf!) : Task.FromException<byte[]>(_failure);
    }
}
