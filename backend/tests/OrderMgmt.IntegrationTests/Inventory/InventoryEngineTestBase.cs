using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;

namespace OrderMgmt.IntegrationTests.Inventory;

/// Engine-level helpers: services called directly, without the HTTP API.
public abstract class InventoryEngineTestBase : InventoryTestBase
{
    protected InventoryEngineTestBase(PostgresFixture pg) : base(pg) { }

    /// Runs `work` in a new scope inside ITransactionRunner.
    protected async Task<T> InTransactionAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
        return await runner.RunAsync(ct => work(scope.ServiceProvider, ct));
    }

    /// `at` is VN time "yyyy-MM-dd HH:mm". `value` is the line value of the whole quantity.
    protected LedgerEntryDraft InDraft(Guid productId, Guid warehouseId, string at, decimal qty, decimal value,
        Guid? sourceId = null, string code = "PN") =>
        new(productId, warehouseId, MainBranchId, Vn(at), LedgerSourceType.StockIn, sourceId ?? Guid.NewGuid(),
            null, code, 0, qty, 0m, value);

    protected LedgerEntryDraft OutDraft(Guid productId, Guid warehouseId, string at, decimal qty,
        Guid? sourceId = null, string code = "PX") =>
        new(productId, warehouseId, MainBranchId, Vn(at), LedgerSourceType.StockOut, sourceId ?? Guid.NewGuid(),
            null, code, 0, 0m, qty, 0m);

    /// Replaces the ledger rows of one source (ledger only, no costing).
    protected Task<LedgerChangeResult> ReplaceAsync(LedgerSourceType sourceType, Guid sourceId, params LedgerEntryDraft[] drafts) =>
        InTransactionAsync((sp, ct) =>
            sp.GetRequiredService<IInventoryLedgerService>().ReplaceSourceAsync(sourceType, sourceId, drafts, ct));

    protected Task<LedgerChangeResult> ReplaceAsync(LedgerEntryDraft draft) =>
        ReplaceAsync(draft.SourceType, draft.SourceId, draft);

    protected Task<List<InventoryLedgerEntry>> LedgerAsync(Guid productId, Guid? warehouseId = null) =>
        InDbAsync(db => db.InventoryLedger.AsNoTracking()
            .Where(e => e.ProductId == productId && (warehouseId == null || e.WarehouseId == warehouseId))
            .InPostingOrder()
            .ToListAsync());

    protected Task<decimal?> BalanceAsync(Guid productId, Guid warehouseId) =>
        InDbAsync(db => db.StockBalances
            .Where(b => b.ProductId == productId && b.WarehouseId == warehouseId)
            .Select(b => (decimal?)b.Quantity)
            .SingleOrDefaultAsync());
}
