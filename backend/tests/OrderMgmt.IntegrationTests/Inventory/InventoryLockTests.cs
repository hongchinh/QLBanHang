using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class InventoryLockTests : InventoryTestBase
{
    public InventoryLockTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Requires_an_open_transaction()
    {
        using var scope = _factory.Services.CreateScope();
        var inventoryLock = scope.ServiceProvider.GetRequiredService<IInventoryLock>();

        var gate = () => inventoryLock.AcquireBranchGateAsync(new[] { MainBranchId }, exclusive: false);
        await gate.Should().ThrowAsync<InvalidOperationException>();
        var keys = () => inventoryLock.AcquireAsync(new[] { (_productId, MainBranchId) });
        await keys.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Same_key_blocks_and_different_key_does_not()
    {
        var k = (_productId, MainBranchId);
        var k2 = (Guid.NewGuid(), MainBranchId);

        await using var a = await Session.BeginAsync(_factory.Services);
        await a.Lock.AcquireAsync(new[] { k });

        await using (var b = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            await b.Lock.AcquireAsync(new[] { k2 });
            var blocked = () => b.Lock.AcquireAsync(new[] { k });
            (await blocked.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await b.Db.Database.RollbackTransactionAsync();
        }

        await a.Db.Database.CommitTransactionAsync();

        await using var c = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await c.Lock.AcquireAsync(new[] { k });
        await c.Db.Database.CommitTransactionAsync();
    }

    [Fact]
    public async Task Shared_gates_coexist_and_exclusive_gate_waits()
    {
        var gate = new[] { MainBranchId };

        await using var a = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await using var b = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await a.Lock.AcquireBranchGateAsync(gate, exclusive: false);
        await b.Lock.AcquireBranchGateAsync(gate, exclusive: false);

        await using (var c = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            var exclusive = () => c.Lock.AcquireBranchGateAsync(gate, exclusive: true);
            (await exclusive.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await c.Db.Database.RollbackTransactionAsync();
        }

        await a.Db.Database.CommitTransactionAsync();
        await b.Db.Database.CommitTransactionAsync();

        await using var holder = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await holder.Lock.AcquireBranchGateAsync(gate, exclusive: true);

        await using (var d = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            var shared = () => d.Lock.AcquireBranchGateAsync(gate, exclusive: false);
            (await shared.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await d.Db.Database.RollbackTransactionAsync();
        }

        await holder.Db.Database.CommitTransactionAsync();
    }

    /// A DI scope with an open transaction.
    private sealed class Session : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope;

        private Session(AsyncServiceScope scope)
        {
            _scope = scope;
            Db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Lock = scope.ServiceProvider.GetRequiredService<IInventoryLock>();
        }

        public AppDbContext Db { get; }
        public IInventoryLock Lock { get; }

        public static async Task<Session> BeginAsync(IServiceProvider services, bool shortLockTimeout = false)
        {
            var session = new Session(services.CreateAsyncScope());
            await session.Db.Database.BeginTransactionAsync();
            if (shortLockTimeout)
                await session.Db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");
            return session;
        }

        public ValueTask DisposeAsync() => _scope.DisposeAsync();
    }
}
