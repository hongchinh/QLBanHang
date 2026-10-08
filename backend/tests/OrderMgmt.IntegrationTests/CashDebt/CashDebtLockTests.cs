using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class CashDebtLockTests : CashDebtTestBase
{
    private static readonly Guid[] None = Array.Empty<Guid>();

    public CashDebtLockTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Partner_key_blocks_same_partner_and_branch_only()
    {
        // Advisory keys are plain strings: random ids are enough, no rows are needed.
        var branch1 = Guid.NewGuid();
        var branch2 = Guid.NewGuid();
        var partnerP = Guid.NewGuid();
        var partnerQ = Guid.NewGuid();

        await using var a = await Session.BeginAsync(_factory.Services);
        await a.Lock.AcquireAsync(branch1, new[] { partnerP }, None);

        await using (var b = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            await b.Lock.AcquireAsync(branch2, new[] { partnerP }, None);
            await b.Lock.AcquireAsync(branch1, new[] { partnerQ }, None);
            var blocked = () => b.Lock.AcquireAsync(branch1, new[] { partnerP }, None);
            (await blocked.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await b.Db.Database.RollbackTransactionAsync();
        }

        await a.Db.Database.CommitTransactionAsync();

        await using var c = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await c.Lock.AcquireAsync(branch1, new[] { partnerP }, None);
        await c.Db.Database.CommitTransactionAsync();
    }

    [Fact]
    public async Task Fund_key_blocks_same_fund()
    {
        var fund = Guid.NewGuid();
        var otherFund = Guid.NewGuid();

        await using var a = await Session.BeginAsync(_factory.Services);
        await a.Lock.AcquireAsync(Guid.NewGuid(), None, new[] { fund });

        await using (var b = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            await b.Lock.AcquireAsync(Guid.NewGuid(), None, new[] { otherFund });
            // A fund key does not depend on the branch passed in.
            var blocked = () => b.Lock.AcquireAsync(Guid.NewGuid(), None, new[] { fund });
            (await blocked.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await b.Db.Database.RollbackTransactionAsync();
        }

        await a.Db.Database.CommitTransactionAsync();
    }

    [Fact]
    public async Task Reason_key_is_shared_by_voucher_saves_and_exclusive_for_reason_edits()
    {
        var reason = Guid.NewGuid();
        var otherReason = Guid.NewGuid();

        // Two voucher saves with the same reason do not wait for each other; an edit of it waits for both.
        await using var a = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await using var b = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await a.Lock.AcquireReasonAsync(reason, exclusive: false);
        await b.Lock.AcquireReasonAsync(reason, exclusive: false);

        await using (var c = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            await c.Lock.AcquireReasonAsync(otherReason, exclusive: true);
            var edit = () => c.Lock.AcquireReasonAsync(reason, exclusive: true);
            (await edit.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await c.Db.Database.RollbackTransactionAsync();
        }

        await a.Db.Database.CommitTransactionAsync();
        await b.Db.Database.CommitTransactionAsync();

        // A voucher save waits for an edit of its reason.
        await using var editor = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await editor.Lock.AcquireReasonAsync(reason, exclusive: true);

        await using (var d = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            var save = () => d.Lock.AcquireReasonAsync(reason, exclusive: false);
            (await save.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await d.Db.Database.RollbackTransactionAsync();
        }

        await editor.Db.Database.CommitTransactionAsync();

        using var scope = _factory.Services.CreateScope();
        var outside = () => scope.ServiceProvider.GetRequiredService<ICashDebtLock>().AcquireReasonAsync(reason, exclusive: false);
        await outside.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task One_lock_per_key_and_requires_transaction()
    {
        var branchId = Guid.NewGuid();
        var partners = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var funds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var held = await InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<ICashDebtLock>().AcquireAsync(branchId, partners, funds, ct);
            return await sp.GetRequiredService<AppDbContext>().Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()")
                .SingleAsync(ct);
        });

        held.Should().Be(5, "one key per partner plus one key per fund");

        using var scope = _factory.Services.CreateScope();
        var cashDebtLock = scope.ServiceProvider.GetRequiredService<ICashDebtLock>();
        var outside = () => cashDebtLock.AcquireAsync(branchId, partners, funds);
        await outside.Should().ThrowAsync<InvalidOperationException>();
    }

    /// A DI scope with an open transaction.
    private sealed class Session : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope;

        private Session(AsyncServiceScope scope)
        {
            _scope = scope;
            Db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Lock = scope.ServiceProvider.GetRequiredService<ICashDebtLock>();
        }

        public AppDbContext Db { get; }
        public ICashDebtLock Lock { get; }

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
