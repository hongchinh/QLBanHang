using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Infrastructure.Persistence;
using Xunit;

namespace OrderMgmt.IntegrationTests.Fixtures;

[Collection(nameof(PostgresCollection))]
public class TemplateDatabaseTests
{
    private readonly PostgresFixture _pg;

    public TemplateDatabaseTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Each_factory_gets_an_isolated_seeded_database()
    {
        var a = new WebAppFactory(_pg);
        var b = new WebAppFactory(_pg);
        await ((IAsyncLifetime)a).InitializeAsync();
        await ((IAsyncLifetime)b).InitializeAsync();
        try
        {
            using (var scope = a.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ProductGroups.Add(new ProductGroup { Code = "ISO-1", Name = "Isolation" });
                await db.SaveChangesAsync();
            }

            using (var scope = b.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await db.ProductGroups.AnyAsync(g => g.Code == "ISO-1")).Should().BeFalse();
                (await db.ProductGroups.AnyAsync(g => g.Code == "EPS")).Should().BeTrue();
                (await db.Users.AnyAsync(u => u.Username == "admin")).Should().BeTrue();
            }

            DatabaseName(a).Should().NotBe(DatabaseName(b));
            foreach (var factory in new[] { a, b })
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var used = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Database;
                used.Should().Be(DatabaseName(factory));
                used.Should().MatchRegex("_[0-9a-f]{32}$");
            }
        }
        finally
        {
            await ((IAsyncLifetime)a).DisposeAsync();
            await ((IAsyncLifetime)b).DisposeAsync();
        }
    }

    [Fact]
    public async Task Clone_is_dropped_on_dispose()
    {
        var a = new WebAppFactory(_pg);
        await ((IAsyncLifetime)a).InitializeAsync();
        var name = DatabaseName(a);

        await ((IAsyncLifetime)a).DisposeAsync();

        await using var conn = new NpgsqlConnection(MaintenanceConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", conn);
        cmd.Parameters.AddWithValue("name", name);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0);
    }

    [Fact]
    public void Dev_database_names_are_refused()
    {
        var refuseTest = () => PostgresFixture.EnsureNotDevDatabase("qldonhang_test");
        var refuseDev = () => PostgresFixture.EnsureNotDevDatabase("qldonhang");
        var allowIntegration = () => PostgresFixture.EnsureNotDevDatabase("qldonhang_integtest");

        refuseTest.Should().Throw<InvalidOperationException>();
        refuseDev.Should().Throw<InvalidOperationException>();
        allowIntegration.Should().NotThrow();
    }

    [Fact]
    public async Task DefaultConnection_cannot_override_the_clone()
    {
        var a = new WebAppFactory(_pg);
        await ((IAsyncLifetime)a).InitializeAsync();
        try
        {
            var config = a.Services.GetRequiredService<IConfiguration>();
            config.GetConnectionString("DefaultConnection").Should().Be(a.ConnectionString);
            config.GetConnectionString("Default").Should().Be(a.ConnectionString);
        }
        finally
        {
            await ((IAsyncLifetime)a).DisposeAsync();
        }
    }

    private static string DatabaseName(WebAppFactory factory) =>
        new NpgsqlConnectionStringBuilder(factory.ConnectionString).Database!;

    private string MaintenanceConnectionString() =>
        new NpgsqlConnectionStringBuilder(_pg.ConnectionString) { Database = "postgres" }.ConnectionString;
}
