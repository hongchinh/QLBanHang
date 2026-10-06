using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace OrderMgmt.IntegrationTests.Fixtures;

public class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture? _pg;
    private string _connectionString;

    /// <summary>Test factory: gets its own clone of the fixture's template database.</summary>
    public WebAppFactory(PostgresFixture pg)
    {
        _pg = pg;
        _connectionString = string.Empty;
    }

    /// <summary>Template builder used by <see cref="PostgresFixture"/> only.</summary>
    internal WebAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>The database this factory uses (the clone, once initialized).</summary>
    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // AddInfrastructure prefers DefaultConnection over Default, so both must point at
                // this factory's database or an environment/user-secrets value would win.
                ["ConnectionStrings:Default"] = _connectionString,
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:Secret"] = "test-secret-do-not-use-in-production-min-32-chars-bbbbbbbbbbbbb",
                ["Jwt:Issuer"] = "OrderMgmtTest",
                ["Jwt:Audience"] = "OrderMgmtTestClient",
                ["Jwt:ExpiresInMinutes"] = "60",
                ["RefreshToken:ExpiresInDays"] = "14",
                ["AuthCookie:Name"] = "qldh.refresh",
                ["AuthCookie:Path"] = "/api/auth",
                ["AuthCookie:SameSite"] = "Lax",
                ["Database:AutoMigrateAndSeed"] = "false",
                ["Seed:AdminPassword"] = "Admin@123",
                ["RateLimiting:LoginPermitLimit"] = "1000",
                ["QuotationExport:TemplatePath"] = Path.Combine(AppContext.BaseDirectory, "templates", "template_baogia.xlsx"),
                ["QuotationExport:LibreOfficePath"] = "soffice",
                ["QuotationExport:ConversionTimeoutSeconds"] = "60",
                ["Vapid:PublicKey"] = "",
                ["Vapid:PrivateKey"] = "",
                ["Vapid:Subject"] = "mailto:test@test.com",
            });
        });

        builder.ConfigureServices(services =>
        {
            // No-op: Infrastructure already wires Npgsql via configuration.
        });
    }

    public async Task InitializeAsync()
    {
        if (_pg is null)
            throw new InvalidOperationException("The template-builder factory is prepared by PostgresFixture.");

        // Must run before Services is first touched: the host reads the connection string when it is built.
        _connectionString = await _pg.CreateDatabaseAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (_pg is not null && _connectionString.Length > 0)
            await _pg.DropDatabaseAsync(_connectionString);
    }
}
