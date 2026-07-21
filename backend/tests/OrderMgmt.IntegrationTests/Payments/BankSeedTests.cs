using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Payments;

[Collection(nameof(PostgresCollection))]
public class BankSeedTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;

    public BankSeedTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg.ConnectionString);
        await ((IAsyncLifetime)_factory).InitializeAsync();
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Seed_creates_active_banks_with_unique_bins()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var banks = await db.Banks.AsNoTracking().ToListAsync();

        banks.Should().NotBeEmpty();
        banks.Should().OnlyContain(b => b.IsActive);
        banks.Select(b => b.Bin).Should().OnlyHaveUniqueItems();
        banks.Select(b => b.Code).Should().OnlyHaveUniqueItems();
        banks.Should().Contain(b => b.Code == "VCB" && b.Bin == "970436");
        banks.Should().Contain(b => b.Code == "TCB" && b.Bin == "970407");
    }
}
