# Phase 04 — Voucher & ledger schema, posting engine

**Status:** [ ] pending
**Complexity:** XL

## Objective

Create the voucher, ledger, costing and counter tables, then the engine on top of them: a transaction port, advisory locks, an atomic document counter, ledger replacement with running quantities and `StockBalance`, the cost recalculation service, and the posting orchestrator that enforces the negative-stock policy. Also lock catalog changes once inventory activity exists. No voucher HTTP API yet (Phase 05).

## Files

- `backend/src/OrderMgmt.Domain/Enums/InventoryEnums.cs` (modify)
- `backend/src/OrderMgmt.Domain/Common/DomainException.cs` (modify — `NegativeStockException`)
- `backend/src/OrderMgmt.Domain/Entities/Inventory/{StockVoucher,StockVoucherLine,StockVoucherActivity,OpeningStock,InventoryLedgerEntry,InventoryCostPeriod,StockBalance,DocumentCounter}.cs` (new)
- `backend/src/OrderMgmt.Application/Common/Interfaces/{IAppDbContext,ITransactionRunner}.cs` (modify / new)
- `backend/src/OrderMgmt.Application/Inventory/Interfaces/{IInventoryLock,IDocumentCounter}.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Common/StockUnit.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Ledger/{IInventoryLedgerService,InventoryLedgerService,LedgerModels}.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Costing/{IInventoryCostingService,InventoryCostingService}.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Posting/{IInventoryPostingService,InventoryPostingService}.cs` (new)
- `backend/src/OrderMgmt.Application/Catalog/Products/**`, `Catalog/Customers/Services/CustomerService.cs`, `Inventory/{Warehouses,StockReasons,PaymentMethods}/Services/*` (modify — constraints)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/{AppDbContext.cs,Configurations/InventoryConfiguration.cs}` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/EfTransactionRunner.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Inventory/{PostgresInventoryLock,PostgresDocumentCounter}.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_AddStockVouchersAndLedger.cs` (generated)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` (modify — `Vn`, `CreateInventoryProductAsync`, `AssertInvariantsAsync`)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryEngineTestBase.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/{StockSchemaTests,TransactionRunnerTests,InventoryLockTests,DocumentCounterTests,InventoryLedgerServiceTests,InventoryCostingServiceTests,InventoryPostingServiceTests,CatalogConstraintTests}.cs` (new)

## Tasks

### Task 4.1 — Voucher, ledger, cost-period, balance and counter schema

Add to `InventoryEnums.cs`: `StockVoucherStatus { Active = 1, Cancelled = 9 }`, `LedgerSourceType { Opening = 0, StockIn = 1, StockOut = 2 }` (numeric order = same-time posting order), `StockVoucherActivityAction { Created = 1, Updated = 2, Cancelled = 3, Restored = 4, Deleted = 5 }`.

Entities (table names are snake_case plurals):

| Entity | Base | Fields | Config notes |
|---|---|---|---|
| `StockVoucher` | `BaseEntity` | `StockDirection Type`, `Code`, `DateTimeOffset VoucherAt`, `BranchId`, `WarehouseId`, `Guid? PartnerId`, `PartnerName?`, `PartnerAddress?`, `PartnerTaxCode?`, `HandlerName?`, `ReasonId`, `Guid? PaymentMethodId`, `Note?`, `Freight`, `OrderDiscount`, `GoodsAmount`, `LineDiscountTotal`, `VatTotal`, `Total`, `PaidAmount`, `StockVoucherStatus Status`, `DateTimeOffset? CancelledAt`, `Guid? CancelledBy`, `Guid OwnerUserId`, `uint Version`, `Lines`, `Activities` + navs (`Branch`, `Warehouse`, `Partner` → `Customer`, `Reason`, `PaymentMethod`, `Owner` → `User`) | Money `numeric(18,2)`; `VoucherAt` `timestamptz`; `Version` `.IsRowVersion()` (Npgsql maps `uint` row version to `xmin`); unique `(Type, BranchId, Code)` filtered `is_deleted = false`; indexes `(BranchId, Type, VoucherAt)`, `PartnerId`, `ReasonId`; every FK `Restrict`; query filter `!IsDeleted`; `Lines` / `Activities` FK `Restrict` (soft-delete cascade via `AppDbContext`) |
| `StockVoucherLine` | `BaseEntity` | `StockVoucherId`, `SortOrder`, `ProductId`, `ProductCode`, `ProductName`, `WarehouseId`, `TrackInventory`, `PricingMode`, `UnitName`, `PriceIncludesVat`, `SheetCount?`, `Length?`, `Width?`, `Thickness?`, `Quantity`, `UnitPrice`, `Amount`, `DiscountRate`, `DiscountAmount`, `DiscountManual`, `OrderDiscountAllocated`, `FreightAllocated`, `VatRate`, `VatAmount`, `NetAmount`, `InboundValue`, `Note?` | `Quantity` and dimensions `numeric(18,6)`; rates `numeric(5,2)`; money `numeric(18,2)`; query filter `!IsDeleted && !StockVoucher.IsDeleted` |
| `StockVoucherActivity` | `BaseEntity` | `StockVoucherId`, `Action`, `ActorUserId?`, `OccurredAt`, `Description`, `MetadataJson?` | Mirrors `QuotationActivityConfiguration` |
| `OpeningStock` | `BaseEntity` | `BranchId`, `WarehouseId`, `ProductId`, `DateOnly OpeningDate`, `Quantity`, `Amount` | Unique `(WarehouseId, ProductId)` filtered `is_deleted = false`; `Quantity` `numeric(18,6)`, `Amount` `numeric(18,2)` |
| `InventoryLedgerEntry` | — | `Guid Id`, `DateTimeOffset PostedAt`, `LedgerSourceType SourceType`, `SourceId`, `Guid? SourceLineId`, `SourceCode` (max 50), `LineSortOrder`, `BranchId`, `WarehouseId`, `ProductId`, `QtyIn`, `QtyOut`, `InValue`, `RunningQty`, `decimal? UnitCost`, `decimal? CostAmount` | Table `inventory_ledger`; quantities `numeric(18,6)`; `UnitCost` `numeric(18,4)`; indexes `(ProductId, WarehouseId, PostedAt)`, `(ProductId, BranchId, PostedAt)`, `(SourceType, SourceId)`; no query filter |
| `InventoryCostPeriod` | — | `Guid Id`, `ProductId`, `BranchId`, `ScopeKey`, `DateOnly PeriodStart`, `DateOnly PeriodEnd`, `OpeningQty`, `OpeningValue`, `InQty`, `InValue`, `OutQty`, `OutValue`, `AvgCost`, `ClosingQty`, `ClosingValue` | Unique `(ProductId, ScopeKey, PeriodStart)`; index `(BranchId, PeriodStart)`; quantities `numeric(18,6)`, values `numeric(18,2)`, `AvgCost` `numeric(18,4)` |
| `StockBalance` | — | `WarehouseId`, `ProductId`, `BranchId`, `Quantity` | PK `(WarehouseId, ProductId)`; `Quantity` `numeric(18,6)` |
| `DocumentCounter` | — | `DocumentType DocType`, `BranchId`, `PeriodKey` (max 10), `long Value` | PK `(DocType, BranchId, PeriodKey)` |

1. **Write the failing test** `Inventory/StockSchemaTests.cs : InventoryTestBase`. The helpers of Task 4.5 / Phase 05 do not exist yet: use the seeded `_productId` from `QuotationTestBase`, and read the `NMH` reason id and the admin user id inline through `InDbAsync`.
   - `Voucher_code_is_unique_per_type_and_branch_among_active_rows`: insert a voucher with 2 lines through `InDbAsync` (lines added with `db.StockVoucherLines.Add`, see SUMMARY "Child rows") and reload it with `Include(Lines)`. A duplicate `(Type, BranchId, Code)` → `DbUpdateException` with inner `SqlState == "23505"`. The same code is accepted for another type, another branch, or after soft delete.
   - `Stale_version_update_throws_concurrency_exception`: load the same voucher in two scopes; save a change in A, then in B → `DbUpdateConcurrencyException`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockSchemaTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** enums, entities, configurations, `DbSet`s on `IAppDbContext` / `AppDbContext` (`StockVouchers`, `StockVoucherLines`, `StockVoucherActivities`, `OpeningStocks`, `InventoryLedger`, `InventoryCostPeriods`, `StockBalances`, `DocumentCounters`), migration `AddStockVouchersAndLedger`. The scaffolded migration may list an `xmin` column (type `xid`, `rowVersion: true`) — that is expected: Npgsql never emits DDL for system columns. Confirm with `dotnet ef migrations script <previous> AddStockVouchersAndLedger --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi` that no `xmin` column is created; do not hand-edit the mapping.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock voucher, ledger, cost period, balance and counter schema"`

### Task 4.2 — `ITransactionRunner`

```csharp
// Application/Common/Interfaces/ITransactionRunner.cs
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
    Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}
```

`Infrastructure/Persistence/EfTransactionRunner` (scoped; depends on `AppDbContext`):
- If `Database.CurrentTransaction` is not null, run `work` directly (join the outer transaction).
- Otherwise `BeginTransactionAsync(IsolationLevel.ReadCommitted)` → `work` → `CommitAsync`.
- On exception: `RollbackAsync`, then `ChangeTracker.Clear()`, then rethrow.

1. **Write the failing test** `Inventory/TransactionRunnerTests.cs : InventoryTestBase`:
   - `Commits_on_success_and_rolls_back_every_save_on_failure`: a successful run adds a `Branch`; a failing run does two `SaveChangesAsync` and then throws → no rows.
   - `Nested_run_joins_the_outer_transaction` (inner save, outer throws → nothing persisted).
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~TransactionRunnerTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** interface, `EfTransactionRunner`, `services.AddScoped<ITransactionRunner, EfTransactionRunner>()` in Infrastructure DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(persistence): transaction runner port"`

### Task 4.3 — `IInventoryLock` (advisory locks)

```csharp
// Application/Inventory/Interfaces/IInventoryLock.cs
public interface IInventoryLock
{
    // D30 branch costing gate, taken FIRST. Shared for voucher posting / opening stock;
    // exclusive (ascending branch id) for settings-change and manual recalculation.
    // Key: hashtextextended('inv-branch:{branchId:N}', 0). Throws InvalidOperationException without a transaction.
    Task AcquireBranchGateAsync(IEnumerable<Guid> branchIds, bool exclusive, CancellationToken ct = default);

    // pg_advisory_xact_lock per distinct (ProductId, BranchId), ascending by ProductId then BranchId.
    // Taken after the branch gate. Throws InvalidOperationException when no transaction is open.
    Task AcquireAsync(IEnumerable<(Guid ProductId, Guid BranchId)> keys, CancellationToken ct = default);
}
```

`Infrastructure/Inventory/PostgresInventoryLock`: for the gate, run `SELECT pg_advisory_xact_lock_shared(hashtextextended({key}, 0))` (shared) or `pg_advisory_xact_lock(...)` (exclusive) per branch id in ascending order, with `key = $"inv-branch:{branchId:N}"`; for product keys, run `ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))")` with `key = $"inv:{productId:N}:{branchId:N}"`. Lock order everywhere (D30): branch gate → product keys → document counter row. Exclusive gate holders take **no** product keys, so a settings change never holds thousands of advisory locks (local PostgreSQL: `max_locks_per_transaction = 64`, `max_connections = 100`).

1. **Write the failing test** `Inventory/InventoryLockTests.cs : InventoryTestBase`:
   - `Requires_an_open_transaction` → `InvalidOperationException` (both methods).
   - `Same_key_blocks_and_different_key_does_not`:
     1. scope A opens a transaction and acquires `k`;
     2. scope B opens a transaction, runs `SET LOCAL lock_timeout = '300ms'` and acquires `k2` successfully;
     3. scope B then acquires `k` → `PostgresException` with `SqlState == "55P03"`;
     4. roll back B and commit A; scope C acquires `k` successfully.
   - `Shared_gates_coexist_and_exclusive_gate_waits`: scopes A and B both take the shared gate of the main branch (no wait); scope C with `lock_timeout = '300ms'` asks for the exclusive gate → `55P03`; after A and B commit, C gets it; while C holds it, a shared request with a short timeout → `55P03`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~InventoryLockTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** interface, implementation, DI registration (scoped).
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): branch costing gate and advisory lock per product"`

### Task 4.4 — `IDocumentCounter`

```csharp
// Application/Inventory/Interfaces/IDocumentCounter.cs
public interface IDocumentCounter
{
    // Atomic upsert-increment inside the caller's transaction; the row lock serializes concurrent creators.
    Task<long> NextAsync(DocumentType docType, Guid branchId, string periodKey, CancellationToken ct = default);
    // Value NextAsync would return now; does not write.
    Task<long> PeekNextAsync(DocumentType docType, Guid branchId, string periodKey, CancellationToken ct = default);
}
```

`Infrastructure/Inventory/PostgresDocumentCounter`:
- `NextAsync`: `(await db.Database.SqlQuery<long>($@"INSERT INTO document_counters (doc_type, branch_id, period_key, value) VALUES ({(int)docType}, {branchId}, {periodKey}, 1) ON CONFLICT (doc_type, branch_id, period_key) DO UPDATE SET value = document_counters.value + 1 RETURNING value AS ""Value""").ToListAsync(ct)).Single()`. Do **not** compose LINQ on top: PostgreSQL rejects `INSERT` inside a subquery.
- `PeekNextAsync`: `SELECT COALESCE((SELECT value FROM document_counters WHERE …), 0) + 1 AS "Value"`.

1. **Write the failing test** `Inventory/DocumentCounterTests.cs : InventoryTestBase`:
   - `Increments_per_key_and_peek_does_not_write` (1, 2, 3; type / branch / period key are independent; `PeekNextAsync` does not increment)
   - `Rolled_back_increment_is_discarded`
   - `Parallel_calls_return_distinct_values` (10 tasks, each in its own scope and transaction → `{1..10}`)
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~DocumentCounterTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** interface, implementation, DI (scoped).
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): transactional document counter"`

### Task 4.5 — Ledger replacement, running quantity and `StockBalance`

```csharp
// Application/Inventory/Ledger/LedgerModels.cs
public sealed record LedgerEntryDraft(
    Guid ProductId, Guid WarehouseId, Guid BranchId, DateTimeOffset PostedAt,
    LedgerSourceType SourceType, Guid SourceId, Guid? SourceLineId, string SourceCode, int LineSortOrder,
    decimal QtyIn, decimal QtyOut, decimal InValue);

public sealed record PairChange(
    Guid ProductId, Guid WarehouseId, Guid BranchId,
    DateTimeOffset From,                // earliest old/new PostedAt of this pair (UTC)
    decimal? MinRunningQty,             // AFTER the change: min RunningQty among rows with PostedAt >= From (null if none)
    DateTimeOffset? FirstNegativeAt,    // AFTER: PostedAt of the first such row with RunningQty < 0
    decimal? OldMinRunningQty,          // BEFORE the change, same window (read before the old rows are deleted; D31)
    DateTimeOffset? OldFirstNegativeAt, // BEFORE: first negative point in the same window
    decimal Balance);                   // StockBalance after the change

public sealed record LedgerChangeResult(IReadOnlyList<PairChange> Pairs);

// Application/Inventory/Ledger/IInventoryLedgerService.cs
public interface IInventoryLedgerService
{
    Task<LedgerChangeResult> ReplaceSourceAsync(LedgerSourceType sourceType, Guid sourceId,
        IReadOnlyList<LedgerEntryDraft> entries, CancellationToken ct = default);
}
```

Algorithm (runs inside the caller's transaction):
0. Normalize every draft's `PostedAt` to UTC (`VnTime.ToUtc`, D27).
1. Read the old rows of `(sourceType, sourceId)` (`ProductId`, `WarehouseId`, `BranchId`, `PostedAt`). Compute `From[pair]` = min `PostedAt` over the pair's old rows and new drafts. For each pair, read `OldMinRunningQty` / `OldFirstNegativeAt` from the **stored** `RunningQty` of the pair's rows with `PostedAt >= From` (this is the pre-change state, D31). Then `ExecuteDeleteAsync` the old rows.
2. Insert the new rows with `RunningQty = 0` (through `db.InventoryLedger.AddRange`), then `SaveChangesAsync`.
3. (`From` was computed in step 1.)
4. For each pair:
   - `base` = the last row with `PostedAt < From` in the order `PostedAt, SourceType, SourceCode, LineSortOrder, Id`; `running = base?.RunningQty ?? 0`.
   - Load the rows with `PostedAt >= From` in the same order and accumulate `running += QtyIn − QtyOut` into `RunningQty`, tracking the minimum and the first negative.
   - Upsert `StockBalance(WarehouseId, ProductId)` to the final `running`; delete it when the pair has no rows left.
5. `SaveChangesAsync` and return the pairs.

1. **Write the failing tests:**
   - Add to `InventoryTestBase` (Phase 05 and 06 tests reuse them):
     - `protected static DateTimeOffset Vn(string local)` (parses `"2026-10-02 08:00"` as VN time and returns the **UTC instant**, `.ToUniversalTime()`, D27);
     - `protected Task AssertInvariantsAsync()` (review m17): every `StockBalance.Quantity` equals `Σ(QtyIn − QtyOut)` of its pair and no pair without rows has a balance; every `RunningQty` equals the cumulative sum in posting order; for adjacent `InventoryCostPeriod` rows of the same `(ProductId, ScopeKey)`, `next.Opening{Qty,Value} == prev.Closing{Qty,Value}`; each period's `ClosingQty` / `ClosingValue` equal the scope's ledger sums `Σ(QtyIn − QtyOut)` / `Σ(InValue − CostAmount)` up to `PeriodEnd`; each period's `OutValue == Σ CostAmount` of its outbound rows. Call it at the end of every engine, voucher, opening-stock and recalculation scenario test from here on;
     - `protected Task<Guid> CreateInventoryProductAsync(string code, PricingMode mode = PricingMode.PerUnit, bool track = true, decimal? costPrice = null, decimal? defaultPrice = null, decimal taxRate = 0, bool priceIncludesVat = false)` (DB insert, unit TAM, group EPS).
   - Create `Inventory/InventoryEngineTestBase.cs : InventoryTestBase` with:
     - `protected Task<T> InTransactionAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> work)` — new scope, resolves `ITransactionRunner`;
     - `protected LedgerEntryDraft InDraft(Guid productId, Guid warehouseId, string at, decimal qty, decimal value, Guid? sourceId = null, string code = "PN")` and `OutDraft(...)` (BranchId = `MainBranchId`, a fresh `sourceId` when null);
     - `protected Task<List<InventoryLedgerEntry>> LedgerAsync(Guid productId, Guid? warehouseId = null)` (ordered);
     - `protected Task<decimal?> BalanceAsync(Guid productId, Guid warehouseId)`.
   - `Inventory/InventoryLedgerServiceTests.cs : InventoryEngineTestBase`:
     - `Running_quantity_follows_posting_time_not_insert_order`: In 10 @ 10-02, then Out 3 @ 10-05, then In 5 @ 10-03 posted last → running 10 / 15 / 12; balance 12.
     - `Same_timestamp_orders_opening_then_in_then_out`
     - `Moving_a_source_later_recomputes_from_the_earliest_time` (move In from 10-02 to 10-06 → the 10-05 Out row has running −3; `MinRunningQty = -3`; `FirstNegativeAt = 10-05`; `OldMinRunningQty = 7`, `OldFirstNegativeAt = null`)
     - `Drafts_with_vn_offset_are_stored_as_utc` (D27): a draft whose `PostedAt` carries `+07:00` is stored and read back as the same instant with offset 0
     - `Replacing_with_empty_list_removes_rows_and_balance`
     - `Moving_a_line_between_warehouses_reports_both_pairs`
     - `Balance_equals_sum_of_ledger_after_mixed_changes`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~InventoryLedgerServiceTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** models, interface, `InventoryLedgerService`, DI (scoped, Application).
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): ledger replacement with running quantity and stock balance"`

### Task 4.6 — Cost recalculation service

```csharp
// Application/Inventory/Costing/IInventoryCostingService.cs
public sealed record CostScopeChange(Guid ProductId, Guid BranchId, Guid ScopeKey, DateOnly FromDate);

public interface IInventoryCostingService
{
    // Branch scope → group pairs by (ProductId, BranchId); Warehouse scope → (ProductId, WarehouseId).
    // FromDate = VnTime.ToVnDate(min From) of the group.
    Task<IReadOnlyList<CostScopeChange>> ScopesForAsync(IReadOnlyList<PairChange> pairs, CancellationToken ct = default);
    Task RecalculateAsync(IReadOnlyCollection<CostScopeChange> changes, CancellationToken ct = default);
}
```

`RecalculateAsync`, for each change (D7, D8, D11):
1. Load `InventorySettings` (`CostingPeriod`, `CostingScope`), `Branch.LockedUntil` and `today = VnTime.ToVnDate(IDateTime.UtcNow)`. Callers hold the locks already (D30), so the settings read here are current. All date bounds below are UTC instants from `VnTime.StartOfDay/StartOfNextDay` (D27).
2. `start = CostingPeriodCalendar.FirstUnlockedFrom(FromDate, LockedUntil, period)`.
3. Scope filter on the ledger: `ProductId` and (`BranchId == ScopeKey` for Branch scope, or `WarehouseId == ScopeKey` for Warehouse scope).
4. `end = PeriodOf(max(today, VN date of the last row in scope))`.
5. Opening, from the ledger (D7): `Σ(QtyIn − QtyOut)` and `Σ(InValue − (CostAmount ?? 0))` over rows with `PostedAt < VnTime.StartOfDay(start.Start)`.
6. Fallback: the `AvgCost` of the latest `InventoryCostPeriod` for `(ProductId, ScopeKey)` with `PeriodStart < start.Start`, otherwise `Product.CostPrice ?? 0`.
7. Load the rows in `[StartOfDay(start.Start), StartOfNextDay(end.End))` in posting order, bucket them into `CostingPeriodCalendar.Range(start.Start, end.End, period)` by VN date, and run `PeriodicAverageCalculator.Run`.
8. `ExecuteDeleteAsync` the `InventoryCostPeriod` rows for `(ProductId, ScopeKey)` with `PeriodStart >= start.Start`. Insert the results (with `BranchId`), write `UnitCost` / `CostAmount` on outbound rows (inbound rows keep null), then `SaveChangesAsync`.

1. **Write the failing test** `Inventory/InventoryCostingServiceTests.cs : InventoryEngineTestBase`. Post rows with `IInventoryLedgerService`, then call `ScopesForAsync` + `RecalculateAsync`:
   - `Branch_scope_month_matches_hand_calculation` — the Task 3.4 `Single_month_average_cost` data (October 2026, `KHO01`) → out costs 1,600,000 / 2,133,333; the October cost-period row matches.
   - `Warehouse_scope_costs_each_warehouse_separately` — W1 In 10 @ 1,000,000, W2 In 10 @ 2,000,000, Out 5 from each. Branch scope → both 750,000. After `UpdateInventorySettingsAsync(s => s.CostingScope = Warehouse)` and a recalc → W1 500,000, W2 1,000,000.
   - `Back_dated_inbound_reprices_later_periods` — Sep In 10 @ 1,000,000, Oct Out 5 → 500,000. Add Sep In 10 @ 3,000,000 → Sep AvgCost 200,000; Oct Out → 1,000,000; Oct `OpeningQty` 20 / `OpeningValue` 4,000,000 == Sep closing.
   - `Fully_locked_period_is_never_rewritten` — compute Sep and Oct, set `LockedUntil = 2026-09-30`, recalc from 2026-09-01 → the Sep row keeps the same `Id` and values; Oct is rewritten.
   - `Partially_locked_period_is_recomputed` — `LockedUntil = 2026-10-10`, Out on 10-05, add In on 10-15, recalc → the 10-05 Out cost changes.
   - `Fallback_uses_product_cost_price_when_no_history` — `CostPrice` 60,000, Out 2 → 120,000.
   - `Empty_periods_between_movements_get_rows` — Sep In, Nov Out → the Oct row has opening = closing.
   - `Year_period_averages_the_whole_year` (review m17) — `CostingPeriod = Year`: Mar In 10 @ 1,000,000, Jun Out 5, Sep In 10 @ 2,000,000, Nov Out 5 → one 2026 row, AvgCost 150,000, both Outs 750,000.
   - Every test ends with `AssertInvariantsAsync()`.
   - Every test sets the clock-independent end by posting at least one row in the last month it asserts. Do not depend on today's date beyond `end >= today`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~InventoryCostingServiceTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** interface and service (dependencies `IAppDbContext`, `IDateTime`), DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): periodic average cost recalculation per product and scope"`

### Task 4.7 — Posting orchestrator and negative-stock policy

```csharp
// Domain/Common/DomainException.cs
public sealed class NegativeStockException : DomainException
{
    public bool IsWarning { get; }
    public IDictionary<string, string[]> Shortages { get; } // key "{productCode}@{warehouseCode}"
    public NegativeStockException(bool isWarning, IDictionary<string, string[]> shortages)
        : base(isWarning ? "NEGATIVE_STOCK_WARNING" : "NEGATIVE_STOCK_BLOCKED",
               isWarning ? "Một số mặt hàng sẽ bị âm kho. Xác nhận để tiếp tục."
                         : "Không đủ tồn kho để thực hiện thao tác.")
    { IsWarning = isWarning; Shortages = shortages; }
}

// Application/Inventory/Common/StockUnit.cs — D24
public static class StockUnit { public static string NameFor(PricingMode mode, string? unitName); }

// Application/Inventory/Posting/IInventoryPostingService.cs
public interface IInventoryPostingService
{
    // Shared branch gate first, then the (productId, branchId) keys in ascending order (D30).
    Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> productIds, CancellationToken ct = default);
    // ReplaceSourceAsync → negative-stock policy → ScopesForAsync + RecalculateAsync.
    // Must be called inside ITransactionRunner after AcquireLocksAsync.
    Task<LedgerChangeResult> PostAsync(LedgerSourceType sourceType, Guid sourceId,
        IReadOnlyList<LedgerEntryDraft> entries, bool acknowledgeNegativeStock, CancellationToken ct = default);
}
```

Policy (`InventorySettings.NegativeStockPolicy`, read after the locks): `Allow` → no check. Otherwise a pair is a shortage only when the change makes it worse (D31): `MinRunningQty < 0` **and** (`OldMinRunningQty is null || MinRunningQty < OldMinRunningQty || (OldFirstNegativeAt is not null && FirstNegativeAt < OldFirstNegativeAt)`). `Warn` with shortages and no acknowledgement → throw `NegativeStockException(isWarning: true, …)`. `Block` with shortages → throw `NegativeStockException(isWarning: false, …)`, even when acknowledged. The message per key: `$"Âm {Math.Abs(min):0.######} {unit} tại {FirstNegativeAt (VN):dd/MM/yyyy HH:mm}"`.

1. **Write the failing test** `Inventory/InventoryPostingServiceTests.cs : InventoryEngineTestBase`:
   - `Allow_policy_accepts_negative_stock`
   - `Warn_policy_without_acknowledgement_throws_warning_with_details` (code `NEGATIVE_STOCK_WARNING`; key `"{productCode}@KHO01"`)
   - `Warn_policy_with_acknowledgement_succeeds`
   - `Block_policy_rejects_even_when_acknowledged`
   - `Posting_sets_outbound_cost` (In then Out → the Out row has `CostAmount`)
   - `Rejected_posting_leaves_no_trace_after_rollback` (wrap in `InTransactionAsync`, expect the exception; ledger, `StockBalance` and `InventoryCostPeriod` counts are unchanged)
   - `Reducing_an_existing_deficit_passes_under_block` (D31): under Allow post Out 10 on 10-05 from zero stock; switch to Block; post In 4 dated 10-03 → succeeds (deficit 10 → 6); then post Out 1 dated 10-04 → `NEGATIVE_STOCK_BLOCKED` (deficit grows to 7)
   - Every test ends with `AssertInvariantsAsync()`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~InventoryPostingServiceTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** exception, `StockUnit`, interface and service (dependencies: `IInventoryLock`, `IInventoryLedgerService`, `IInventoryCostingService`, `IAppDbContext`), DI. `AcquireLocksAsync` takes `AcquireBranchGateAsync([branchId], exclusive: false)` and then maps product ids to `(productId, branchId)` keys.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~InventoryPostingServiceTests|FullyQualifiedName~InventoryLedgerServiceTests|FullyQualifiedName~InventoryCostingServiceTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): posting orchestrator with negative stock policy"`

### Task 4.8 — Catalog constraints once inventory activity exists

Rules (section-03 §5, "Ràng buộc danh mục"):
- Product with ledger rows: `PricingMode`, `UnitId`, `TrackInventory` and `PriceIncludesVat` cannot change → 409 `"Hàng hóa đã phát sinh kho: không được đổi cách tính, ĐVT, theo dõi tồn, giá gồm VAT."`. A product with ledger rows or voucher lines cannot be deleted → 409. `ProductDto` gains `bool HasInventoryActivity`.
- Warehouse with ledger rows, voucher (line) references or opening stock: no delete → 409. With ledger rows: no `BranchId` change → 409.
- Partner referenced by a voucher (`StockVouchers.PartnerId`), stock reason referenced by a voucher, or payment method referenced by a voucher: no delete → 409.

1. **Write the failing test** `Inventory/CatalogConstraintTests.cs : InventoryEngineTestBase`. Seed activity via `IInventoryLedgerService`, or by inserting a minimal `StockVoucher` with `InDbAsync`.
   - `Product_with_activity_is_locked_except_its_name`:
     - changing each of `PricingMode`, `UnitId`, `TrackInventory` and `PriceIncludesVat` → 409;
     - renaming → 200;
     - delete → 409;
     - GET returns `HasInventoryActivity == true`.
   - `Catalog_entries_in_use_cannot_be_deleted`: a warehouse with activity cannot be deleted or moved to another branch; a partner, reason or payment method used by a voucher cannot be deleted (all 409).
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~CatalogConstraintTests"`. Expected: FAIL.
3. **Write the minimal implementation:** checks in `ProductService`, `WarehouseService`, `CustomerService`, `StockReasonService`, `PaymentMethodService`; the `HasInventoryActivity` projection.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CatalogConstraintTests|FullyQualifiedName~ProductCrudTests|FullyQualifiedName~WarehouseCrudTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): lock catalog changes once inventory activity exists"`

## Verification

- `cd backend && dotnet build OrderMgmt.sln`
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Inventory"`
- `dotnet test OrderMgmt.sln`

## Exit Criteria

- Schema, transaction runner, advisory locks and the atomic counter work, with the tests above.
- Ledger replacement keeps `RunningQty` and `StockBalance` consistent for any insert order.
- Cost recalculation matches the hand-calculated scenarios, respects fully locked periods and recomputes partially locked ones.
- The posting orchestrator enforces Allow/Warn/Block and leaves nothing behind on rollback.
- Catalog edits are constrained once activity exists.
