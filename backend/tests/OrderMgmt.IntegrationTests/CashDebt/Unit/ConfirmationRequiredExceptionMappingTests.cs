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

public class ConfirmationRequiredExceptionMappingTests
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
    public async Task Warning_confirmation_required_returns_422()
    {
        var details = new Dictionary<string, string[]>
        {
            { "warning_type", new[] { "A warning message" } }
        };
        var exception = new ConfirmationRequiredException("TEST_WARNING", "Test warning message.", isWarning: true, details);

        var (status, error) = await RunAsync(exception);

        status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        error.GetProperty("code").GetString().Should().Be("TEST_WARNING");
        error.GetProperty("message").GetString().Should().Be("Test warning message.");
        error.GetProperty("details").GetProperty("warning_type")[0].GetString().Should().Be("A warning message");
    }

    [Fact]
    public async Task Blocked_confirmation_required_returns_422()
    {
        var details = new Dictionary<string, string[]>
        {
            { "blocked_reason", new[] { "Operation blocked" } }
        };
        var exception = new ConfirmationRequiredException("TEST_BLOCKED", "Test blocked message.", isWarning: false, details);

        var (status, error) = await RunAsync(exception);

        status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        error.GetProperty("code").GetString().Should().Be("TEST_BLOCKED");
        error.GetProperty("message").GetString().Should().Be("Test blocked message.");
        error.GetProperty("details").GetProperty("blocked_reason")[0].GetString().Should().Be("Operation blocked");
    }

    [Fact]
    public async Task Empty_details_returns_empty_object()
    {
        var exception = new ConfirmationRequiredException("TEST_EMPTY", "Test empty details.", isWarning: true, new Dictionary<string, string[]>());

        var (status, error) = await RunAsync(exception);

        status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        error.GetProperty("code").GetString().Should().Be("TEST_EMPTY");
        error.GetProperty("details").GetPropertyCount().Should().Be(0);
    }
}
