using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OrderMgmt.Domain.Common;
using OrderMgmt.WebApi.Middleware;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Unit;

public class ConfirmationExceptionMappingTests
{
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(int Status, JsonElement Error)> RunAsync(Exception thrown)
    {
        var middleware = new GlobalExceptionMiddleware(_ => throw thrown,
            NullLogger<GlobalExceptionMiddleware>.Instance, new TestEnvironment());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, document.RootElement.GetProperty("error").Clone());
    }

    [Fact]
    public async Task Confirmation_required_maps_to_422_with_code_and_details()
    {
        var details = new Dictionary<string, string[]> { ["partner"] = new[] { "Vượt hạn mức công nợ" } };
        var exception = new ConfirmationRequiredException("DEBT_LIMIT", "Cần xác nhận.", details, canConfirm: true);

        var (status, error) = await RunAsync(exception);

        status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        error.GetProperty("code").GetString().Should().Be("DEBT_LIMIT");
        error.GetProperty("message").GetString().Should().Be("Cần xác nhận.");
        error.GetProperty("details").GetProperty("partner")[0].GetString().Should().Be("Vượt hạn mức công nợ");
    }
}
