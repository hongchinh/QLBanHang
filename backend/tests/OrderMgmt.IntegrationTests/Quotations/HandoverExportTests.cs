using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Quotations;

[Collection(nameof(PostgresCollection))]
public class HandoverExportTests : QuotationTestBase
{
    public HandoverExportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task HandoverWithPrice_Excel_returns_xlsx()
    {
        var create = await _client.PostAsJsonAsync("/api/quotations", BuildRequest());
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var response = await _client.GetAsync($"/api/quotations/{id}/handover-with-price/excel");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await response.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandoverNoPrice_Excel_returns_xlsx()
    {
        var create = await _client.PostAsJsonAsync("/api/quotations", BuildRequest());
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var response = await _client.GetAsync($"/api/quotations/{id}/handover-no-price/excel");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await response.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandoverWithPrice_WritesTaxRateToTaxRow()
    {
        var request = BuildRequest(
            new UpsertQuotationLineRequest
            {
                SortOrder = 0,
                ProductId = _productId,
                ProductName = "Test EPS 1",
                UnitName = "Tấm",
                PricingMode = PricingMode.PerUnit,
                Quantity = 1,
                UnitPrice = 10_000,
            },
            new UpsertQuotationLineRequest
            {
                SortOrder = 1,
                ProductId = _productId,
                ProductName = "Test EPS 2",
                UnitName = "Tấm",
                PricingMode = PricingMode.PerUnit,
                Quantity = 1,
                UnitPrice = 20_000,
            },
            new UpsertQuotationLineRequest
            {
                SortOrder = 2,
                ProductId = _productId,
                ProductName = "Test EPS 3",
                UnitName = "Tấm",
                PricingMode = PricingMode.PerUnit,
                Quantity = 1,
                UnitPrice = 30_000,
            });
        request.TaxRate = 8;

        var create = await _client.PostAsJsonAsync("/api/quotations", request);
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var response = await _client.GetAsync($"/api/quotations/{id}/handover-with-price/excel");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);

        ws.Cell("E18").GetDouble().Should().Be(0.08d);
        ws.Cell("F18").GetDouble().Should().Be(4_800d);
        ws.Cell("F19").GetDouble().Should().Be(64_800d);
    }

    [Fact]
    public async Task Handover_ProductSummary_UsesCurrentProductGroupsAndExcludesShippingGroup()
    {
        Guid xpsProductId;
        Guid shippingProductId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var xpsGroupId = (await db.ProductGroups.FirstAsync(g => g.Code == "XPS")).Id;
            var shippingGroupId = (await db.ProductGroups.FirstAsync(g => g.Code == "VC")).Id;

            var xps = new Product
            {
                Code = "HH-HANDOVER-XPS",
                Name = "Handover XPS",
                ProductGroupId = xpsGroupId,
                UnitId = _unitId,
                DefaultPrice = 20_000,
                Status = ProductStatus.Active,
                PricingMode = PricingMode.PerUnit,
            };
            var shipping = new Product
            {
                Code = "HH-HANDOVER-SHIP",
                Name = "Handover shipping",
                ProductGroupId = shippingGroupId,
                UnitId = _unitId,
                DefaultPrice = 10_000,
                Status = ProductStatus.Active,
                PricingMode = PricingMode.PerUnit,
            };
            db.Products.AddRange(xps, shipping);
            await db.SaveChangesAsync();
            xpsProductId = xps.Id;
            shippingProductId = shipping.Id;
        }

        var create = await _client.PostAsJsonAsync("/api/quotations", BuildRequest(
            new UpsertQuotationLineRequest
            {
                SortOrder = 0,
                ProductId = _productId,
                ProductName = "Custom EPS name",
                UnitName = "Tấm",
                PricingMode = PricingMode.PerUnit,
                Quantity = 1,
                UnitPrice = 12_000,
            },
            new UpsertQuotationLineRequest
            {
                SortOrder = 1,
                ProductId = xpsProductId,
                ProductName = "Custom XPS name",
                UnitName = "Tấm",
                PricingMode = PricingMode.PerUnit,
                Quantity = 1,
                UnitPrice = 20_000,
            },
            new UpsertQuotationLineRequest
            {
                SortOrder = 2,
                ProductId = shippingProductId,
                ProductName = "Phí giao hàng",
                UnitName = "Lần",
                PricingMode = PricingMode.PerUnit,
                Quantity = 1,
                UnitPrice = 10_000,
            }));
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var response = await _client.GetAsync($"/api/quotations/{id}/handover-with-price/excel");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var summary = wb.Worksheet(1).Cell("B12").GetString();

        summary.Should().Be("Hàng hóa cung cấp: Tấm xốp EPS, Tấm xốp XPS");
    }

    [Fact]
    public async Task HandoverWithPrice_Pdf_returns_pdf_via_fake_converter()
    {
        var factory = new WebAppFactoryWithFakeHandoverPdfConverter(_pg);
        await ((IAsyncLifetime)factory).InitializeAsync();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var create = await client.PostAsJsonAsync("/api/quotations", BuildRequest());
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var response = await client.GetAsync($"/api/quotations/{id}/handover-with-price/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        client.Dispose();
        await ((IAsyncLifetime)factory).DisposeAsync();
    }

    [Fact]
    public async Task HandoverNoPrice_Pdf_returns_pdf_via_fake_converter()
    {
        var factory = new WebAppFactoryWithFakeHandoverPdfConverter(_pg);
        await ((IAsyncLifetime)factory).InitializeAsync();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var create = await client.PostAsJsonAsync("/api/quotations", BuildRequest());
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var response = await client.GetAsync($"/api/quotations/{id}/handover-no-price/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        client.Dispose();
        await ((IAsyncLifetime)factory).DisposeAsync();
    }

    private static async Task AuthenticateClientAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }
}

file sealed class WebAppFactoryWithFakeHandoverPdfConverter : WebAppFactory
{
    public WebAppFactoryWithFakeHandoverPdfConverter(PostgresFixture pg) : base(pg) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IQuotationSpreadsheetPdfConverter));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddScoped<IQuotationSpreadsheetPdfConverter, FakeHandoverPdfConverter>();
        });
    }
}

file sealed class FakeHandoverPdfConverter : IQuotationSpreadsheetPdfConverter
{
    private static readonly byte[] FakePdf = "%PDF-1.0 fake\n"u8.ToArray();
    public Task<byte[]> ConvertAsync(byte[] xlsxBytes, CancellationToken ct = default)
        => Task.FromResult(FakePdf);
}
