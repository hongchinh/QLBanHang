using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderMgmt.Application.CashDebt.Interfaces;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;
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
    public async Task Stock_reason_key_is_shared_by_voucher_saves_and_exclusive_for_reason_edits()
    {
        var reason = Guid.NewGuid();
        var otherReason = Guid.NewGuid();

        // Two stock voucher saves with the same reason do not wait for each other; an edit of it waits for both.
        await using var a = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await using var b = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await a.Lock.AcquireStockReasonAsync(reason, exclusive: false);
        await b.Lock.AcquireStockReasonAsync(reason, exclusive: false);

        await using (var c = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            // The key is per reason, and a cash reason with the same id has a key of its own.
            await c.Lock.AcquireStockReasonAsync(otherReason, exclusive: true);
            await c.Lock.AcquireReasonAsync(reason, exclusive: true);
            var edit = () => c.Lock.AcquireStockReasonAsync(reason, exclusive: true);
            (await edit.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await c.Db.Database.RollbackTransactionAsync();
        }

        await a.Db.Database.CommitTransactionAsync();
        await b.Db.Database.CommitTransactionAsync();

        // A stock voucher save waits for an edit of its reason.
        await using var editor = await Session.BeginAsync(_factory.Services, shortLockTimeout: true);
        await editor.Lock.AcquireStockReasonAsync(reason, exclusive: true);

        await using (var d = await Session.BeginAsync(_factory.Services, shortLockTimeout: true))
        {
            var save = () => d.Lock.AcquireStockReasonAsync(reason, exclusive: false);
            (await save.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55P03");
            await d.Db.Database.RollbackTransactionAsync();
        }

        await editor.Db.Database.CommitTransactionAsync();

        using var scope = _factory.Services.CreateScope();
        var outside = () => scope.ServiceProvider.GetRequiredService<ICashDebtLock>()
            .AcquireStockReasonAsync(reason, exclusive: false);
        await outside.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Stock_reason_edit_and_delete_wait_for_an_open_shared_key()
    {
        var edited = await CreateStockReasonAsync(StockDirection.Out, PartnerType.Any, DebtEffect.None);
        var deleted = await CreateStockReasonAsync(StockDirection.Out, PartnerType.Any, DebtEffect.None);

        // A stock voucher save holds the shared keys of its reasons until it commits.
        Task<(HttpStatusCode Status, StockReasonDto? Reason, ApiError? Error)> edit;
        Task<HttpResponseMessage> delete;
        await using (var save = await Session.BeginAsync(_factory.Services))
        {
            await save.Lock.AcquireStockReasonAsync(edited, exclusive: false);
            await save.Lock.AcquireStockReasonAsync(deleted, exclusive: false);

            edit = PutStockReasonAsync(edited, StockDirection.Out, PartnerType.Any, DebtEffect.ReceivableIncrease);
            delete = _client.DeleteAsync($"/api/stock-reasons/{deleted}");
            await WaitForBlockedAsync(Task.WhenAll(edit, delete), count: 2);
            edit.IsCompleted.Should().BeFalse("the reason edit waits for the save holding its key");
            delete.IsCompleted.Should().BeFalse("the reason delete waits for the save holding its key");

            await save.Db.Database.CommitTransactionAsync();
        }

        var saved = await edit;
        saved.Status.Should().Be(HttpStatusCode.OK, saved.Error?.Message);
        saved.Reason!.DebtEffect.Should().Be(DebtEffect.ReceivableIncrease);
        (await delete).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Stock_voucher_save_and_stock_reason_edit_wait_for_each_other()
    {
        var code = "SR" + Guid.NewGuid().ToString("N")[..8];
        var branchId = await CreateBranchAsync(code);
        var warehouseId = await CreateWarehouseAsync(branchId, code);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        var product = await CreateInventoryProductAsync("SP-" + Guid.NewGuid().ToString("N")[..8], track: false);
        UpsertStockVoucherRequest Sale(Guid reasonId, string at)
        {
            var request = VoucherRequest(StockDirection.Out, reasonId, at, LineRequest(product, 1, 1_000));
            request.WarehouseId = warehouseId;
            return request;
        }

        // A create takes its number (the counter row) after its keys: while it waits there, the reason edit waits for
        // the shared reason key and then sees the reason in use.
        var edited = await CreateStockReasonAsync(StockDirection.Out, PartnerType.Any, DebtEffect.None);
        var (create, edit) = await RaceAsync(HoldNextCode(DocumentType.StockOut, branchId),
            () => PostVoucherAsync(client, Sale(edited, "2026-10-05 09:00")),
            () => PutStockReasonAsync(edited, StockDirection.Out, PartnerType.Any, DebtEffect.ReceivableIncrease));
        create.Status.Should().Be(HttpStatusCode.OK, create.Error?.Message);
        edit.Status.Should().Be(HttpStatusCode.BadRequest);
        edit.Error!.Details!.Should().ContainKey("debtEffect");

        // The reason edit writes the reason row after its in-use check: while it waits there, the create (validated
        // against the old reason) waits for the reason key and then checks the new debt effect, which needs a partner.
        var switched = await CreateStockReasonAsync(StockDirection.Out, PartnerType.Any, DebtEffect.None);
        var (editFirst, createAfter) = await RaceAsync(HoldRow("stock_reasons", switched),
            () => PutStockReasonAsync(switched, StockDirection.Out, PartnerType.Any, DebtEffect.ReceivableIncrease),
            () => PostVoucherAsync(client, Sale(switched, "2026-10-05 10:00")));
        editFirst.Status.Should().Be(HttpStatusCode.OK, editFirst.Error?.Message);
        createAfter.Status.Should().Be(HttpStatusCode.BadRequest, createAfter.Error?.Message);
        createAfter.Error!.Details!.Should().ContainKey("partnerId");

        await AssertCashDebtInvariantsAsync();
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

    private async Task<Guid> CreateStockReasonAsync(StockDirection direction, PartnerType partnerType, DebtEffect debtEffect) =>
        (await ReadDataAsync<StockReasonDto>(await _client.PostAsJsonAsync("/api/stock-reasons", new CreateStockReasonRequest
        {
            Code = "LD" + Guid.NewGuid().ToString("N")[..8], Name = "Lý do thử", Direction = direction,
            PartnerType = partnerType, DebtEffect = debtEffect,
        }))).Id;

    private async Task<(HttpStatusCode Status, StockReasonDto? Reason, ApiError? Error)> PutStockReasonAsync(Guid id,
        StockDirection direction, PartnerType partnerType, DebtEffect debtEffect) =>
        await ReadDocumentResponseAsync<StockReasonDto>(await _client.PutAsJsonAsync($"/api/stock-reasons/{id}",
            new UpdateStockReasonRequest
            {
                Name = "Lý do thử", Direction = direction, PartnerType = partnerType, DebtEffect = debtEffect,
            }));

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
