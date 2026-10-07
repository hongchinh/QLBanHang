using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OrderMgmt.WebApi.Middleware;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.Unit;

public class UniqueViolationMappingTests
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
    public async Task Unique_violation_returns_409_duplicate()
    {
        var (status, error) = await RunAsync(new DbUpdateException("save failed",
            new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation)));

        status.Should().Be(StatusCodes.Status409Conflict);
        error.GetProperty("code").GetString().Should().Be("DUPLICATE");
    }

    [Fact]
    public async Task Other_database_errors_stay_500()
    {
        var (status, _) = await RunAsync(new DbUpdateException("save failed",
            new PostgresException("fk", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation)));

        status.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
