using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderMgmt.IntegrationTests.Fixtures;

/// <summary>
/// Provides PostgreSQL for the test suite.
///
/// Default: Testcontainers (requires Docker Engine).
/// Override: set environment variable <c>TEST_DB_CONNECTION</c> to use an existing server.
///
/// The fixture never touches the database named in the connection string. It migrates and seeds a
/// template database (<c>&lt;base&gt;_tpl</c>) once per run, and every <see cref="WebAppFactory"/> gets
/// its own clone (<c>&lt;base&gt;_&lt;guid&gt;</c>), dropped when the factory is disposed.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private const string ConnectionEnvVar = "TEST_DB_CONNECTION";
    private static readonly string[] DevDatabaseNames = { "qldonhang_test", "qldonhang" };

    private PostgreSqlContainer? _container;
    private string? _templateName;
    // Clones not dropped by their factory (e.g. a test failed before disposing it) are dropped at the end of the run.
    private readonly ConcurrentDictionary<string, string> _clones = new();

    public string ConnectionString { get; private set; } = default!;

    public static void EnsureNotDevDatabase(string databaseName)
    {
        if (DevDatabaseNames.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"{ConnectionEnvVar} points to the dev database '{databaseName}'. " +
                "Use a dedicated test database name such as 'qldonhang_integtest'.");
    }

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        if (!string.IsNullOrWhiteSpace(external))
        {
            EnsureNotDevDatabase(new NpgsqlConnectionStringBuilder(external).Database ?? string.Empty);
            ConnectionString = external;
        }
        else
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("qldonhang_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        _templateName = $"{BaseDatabaseName}_tpl";
        await ExecuteOnMaintenanceAsync($"DROP DATABASE IF EXISTS \"{_templateName}\" WITH (FORCE)");
        await ExecuteOnMaintenanceAsync($"CREATE DATABASE \"{_templateName}\"");

        await using (var builder = new WebAppFactory(WithDatabase(_templateName)))
        {
            using (var scope = builder.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
            }
            await DbSeeder.SeedAsync(builder.Services);
        }

        // A template with open connections cannot be cloned.
        NpgsqlConnection.ClearAllPools();
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"{BaseDatabaseName}_{Guid.NewGuid():N}";
        await ExecuteOnMaintenanceAsync($"CREATE DATABASE \"{name}\" TEMPLATE \"{_templateName}\"");
        var connectionString = WithDatabase(name);
        _clones[name] = connectionString;
        return connectionString;
    }

    public async Task DropDatabaseAsync(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        using (var conn = new NpgsqlConnection(connectionString))
            NpgsqlConnection.ClearPool(conn);
        await ExecuteOnMaintenanceAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        _clones.TryRemove(name, out _);
    }

    public async Task DisposeAsync()
    {
        foreach (var connectionString in _clones.Values.ToList())
            await DropDatabaseAsync(connectionString);

        if (_templateName is not null)
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteOnMaintenanceAsync($"DROP DATABASE IF EXISTS \"{_templateName}\" WITH (FORCE)");
        }

        if (_container is not null) await _container.DisposeAsync();
    }

    private string BaseDatabaseName => new NpgsqlConnectionStringBuilder(ConnectionString).Database!;

    private string WithDatabase(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;

    private async Task ExecuteOnMaintenanceAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(WithDatabase("postgres"));
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
