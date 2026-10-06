# Phase 06 — Opening stock, cost recalculation & report APIs

**Status:** [ ] pending
**Complexity:** L

## Objective

Finish the Round 1 backend: the opening stock grid API, the manual "Tính lại giá vốn" endpoint, automatic recalculation when costing settings change (D11), the stock-on-hand and stock-card reports, and an invariant suite proving that back-dated edits, deletes and cancels give the same ledger and costs as re-entering the final data from scratch.

## Files

- `backend/src/OrderMgmt.Application/Inventory/OpeningStocks/{Interfaces/IOpeningStockService.cs,Models/OpeningStockDtos.cs,Services/OpeningStockService.cs,Validators/OpeningStockValidators.cs}` (new)
- `backend/src/OrderMgmt.Application/Inventory/Costing/{IInventoryRecalcService.cs,InventoryRecalcService.cs,RecalcModels.cs}` (new)
- `backend/src/OrderMgmt.Application/Inventory/Settings/Services/InventorySettingsService.cs` (modify)
- `backend/src/OrderMgmt.Application/Inventory/Reports/{Interfaces/IInventoryReportService.cs,Models/InventoryReportDtos.cs,Services/InventoryReportService.cs,Validators/InventoryReportValidators.cs}` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/{OpeningStocksController,InventoryCostController,InventoryReportsController}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/{OpeningStockTests,RecalcCostTests,SettingsRecalcTests,StockOnHandReportTests,StockCardReportTests,InventoryInvariantTests}.cs` (new)

## Tasks

### Task 6.1 — Opening stock grid

Contract (`OpeningStocksController`, route `api/inventory/opening-stock`, guard `inventory.opening_stock` on both actions):

```csharp
// Every member is a public auto-property: System.Text.Json ignores public fields (review m2).
public class OpeningStockGridDto { public Guid WarehouseId { get; set; } public DateOnly? OpeningDate { get; set; } public List<OpeningStockLineDto> Lines { get; set; } = new(); }
public class OpeningStockLineDto { public Guid ProductId { get; set; } public string ProductCode { get; set; } = ""; public string ProductName { get; set; } = ""; public string UnitName { get; set; } = ""; public decimal Quantity { get; set; } public decimal Amount { get; set; } }
public class SaveOpeningStockRequest
{
    public Guid WarehouseId { get; set; }
    public DateOnly OpeningDate { get; set; }
    public bool AcknowledgeNegativeStock { get; set; }
    public List<SaveOpeningStockLine> Lines { get; set; } = new(); // { ProductId, Quantity, Amount }
}
// GET  ?warehouseId=  → OpeningStockGridDto
// PUT  body SaveOpeningStockRequest → OpeningStockGridDto
```

Rules: the warehouse belongs to the working branch (otherwise 403). Products are distinct, exist and are tracked (400 with key `lines[i].productId`, Vietnamese message). `Quantity > 0`, `Amount >= 0`. The new date and the existing rows' date must be after `LockedUntil` (400 `PERIOD_LOCKED`). Values are visible to every holder of `inventory.opening_stock` (D36). In one transaction:
1. `AcquireLocksAsync(branch, products old ∪ new)` (shared branch gate, then product keys — D30);
2. upsert `OpeningStock` rows by `ProductId` (new rows through `_db.OpeningStocks.Add`; soft-delete rows that were removed);
3. `PostAsync(LedgerSourceType.Opening, warehouseId, drafts, ack)` with `PostedAt = VnTime.StartOfDay(OpeningDate)` (a UTC instant, D27), `SourceLineId = row.Id`, `SourceCode = "TDK"`, `LineSortOrder = index`, `QtyIn = Quantity`, `InValue = Amount` (D20).

An empty list removes all opening stock of the warehouse.

1. **Write the failing test** `Inventory/OpeningStockTests.cs : InventoryTestBase`:
   - `Save_load_posting_order_and_costing`:
     - save then load the grid → it roundtrips;
     - opening on 10-01 and an In voucher at 10-01 08:00 → the opening row comes first in ledger order;
     - a SquareMeter product with opening 100 m² = 5,000,000 and Out 10 m² in the same month → cost 500,000.
   - `Policy_and_period_lock`:
     - reducing opening below what was issued under Block → 422;
     - a new date on or before the lock → `PERIOD_LOCKED`;
     - an unlocked new date while the old date is locked → `PERIOD_LOCKED`.
   - `Validation_and_permissions`:
     - untracked or duplicate products → 400;
     - SALES → 403; WAREHOUSE → 200 and the grid includes `Amount` (D36);
     - a warehouse of another branch → 403.
   - `Raising_opening_stock_passes_under_block_even_with_a_later_deficit` (D31): an existing later deficit that a larger opening quantity reduces → 200.
   - Every test ends with `AssertInvariantsAsync()`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~OpeningStockTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** DTOs, validator, service, controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): opening stock grid posted to the ledger"`

### Task 6.2 — Manual cost recalculation

Contract (`InventoryCostController`, route `api/inventory/recalc-cost`, `POST`, guard `inventory.recalc_cost`):

```csharp
public class RecalculateCostRequest { public DateOnly FromPeriodStart { get; set; } public Guid? WarehouseId { get; set; } public Guid? ProductId { get; set; } }
public class RecalculateCostResult { public DateOnly FromPeriodStart { get; set; } public int ScopeCount { get; set; } }
public interface IInventoryRecalcService
{
    Task<RecalculateCostResult> RecalculateAsync(RecalculateCostRequest request, CancellationToken ct = default);
    Task ApplySettingsChangeAsync(InventorySettings before, InventorySettings after, CancellationToken ct = default); // Task 6.3
}
```

Rules:
- `FromPeriodStart` must equal `PeriodOf(FromPeriodStart).Start` (400, key `fromPeriodStart`, "Phải là ngày đầu kỳ tính giá").
- That period must not be fully locked for the working branch (`PeriodEnd > LockedUntil`, otherwise 400 `PERIOD_LOCKED`).
- Changes are every `(ProductId, ScopeKey)` with ledger rows in the working branch, filtered by `ProductId`. `WarehouseId` restricts to products with rows in that warehouse; under Warehouse scope it also restricts `ScopeKey = WarehouseId`. `FromDate = FromPeriodStart`.
- One transaction: `IInventoryLock.AcquireBranchGateAsync([branch], exclusive: true)` (no product keys — D30) → read settings → `IInventoryCostingService.RecalculateAsync(changes)`.

1. **Write the failing test** `Inventory/RecalcCostTests.cs : InventoryTestBase`:
   - `Recalc_is_idempotent_and_repairs_tampered_costs`: snapshot cost periods and outbound costs, recalc, compare. Then set an Out row's `CostAmount = 0` and delete its period row through the DB, recalc → restored.
   - `Recalc_filters_by_product_and_warehouse` (tamper two products, recalc one → only that one restored)
   - `Recalc_validation_and_permission` (non-period-start → 400; fully locked period → `PERIOD_LOCKED`; SALES → 403)
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~RecalcCostTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** models, `InventoryRecalcService.RecalculateAsync`, controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): manual cost recalculation endpoint"`

### Task 6.3 — Recalculate when costing settings change (D11)

`InventorySettingsService.UpdateAsync` runs in `ITransactionRunner`. It captures `before`, applies `after`, saves, and calls `IInventoryRecalcService.ApplySettingsChangeAsync(before, after)` when `CostingPeriod`, `CostingScope` or `PurchaseCostIncludesVat` changed **and** the ledger has rows.

`ApplySettingsChangeAsync`:
0. Take the **exclusive** branch gate for every branch with ledger rows, in ascending branch id (`AcquireBranchGateAsync(branchIds, exclusive: true)` — no per-product keys, D30). This waits for in-flight voucher postings (they hold the shared gate) and blocks new ones until the transaction ends.
1. Validate all branches before writing anything (each failure → `ValidationDomainException(errors, message)` with the Vietnamese message as `error.message`):
   - `CostingPeriod` changed and `LockedUntil` is not null and `!CostingPeriodCalendar.IsPeriodEnd(LockedUntil, after.CostingPeriod)` → key `costingPeriod`, message `"Ngày khóa sổ của chi nhánh {Code} ({dd/MM/yyyy}) phải là ngày cuối kỳ khi đổi kỳ tính giá."`;
   - `PurchaseCostIncludesVat` changed and `LockedUntil` is not null and `!CostingPeriodCalendar.IsPeriodEnd(LockedUntil, before.CostingPeriod)` → key `purchaseCostIncludesVat`, message `"Ngày khóa sổ của chi nhánh {Code} ({dd/MM/yyyy}) phải là ngày cuối kỳ khi đổi cách tính VAT vào giá nhập."` (D33 — a period never mixes the two policies).
2. (Locks were taken in step 0.)
3. If `PurchaseCostIncludesVat` changed: for every `StockIn` ledger row of the branch with `PostedAt >= VnTime.StartOfNextDay(LockedUntil)` (all rows if no lock), set `InValue = line.InboundValue + (after.PurchaseCostIncludesVat ? line.VatAmount : 0)`, joining `StockVoucherLines` on `SourceLineId`. Opening rows are untouched.
4. If `CostingPeriod` or `CostingScope` changed: `ExecuteDeleteAsync` the branch's `InventoryCostPeriod` rows with `PeriodEnd > (LockedUntil ?? DateOnly.MinValue)` (every scope key of the branch, so old-scope snapshots of unlocked periods disappear).
5. `RecalculateAsync` for every `(ProductId, ScopeKey under the after-scope)` of the branch, with `FromDate = LockedUntil?.AddDays(1) ?? earliest VN ledger date of the branch`.

1. **Write the failing test** `Inventory/SettingsRecalcTests.cs : InventoryTestBase` (use the API: `PUT /api/inventory/settings`; API notation `In q × p` = unit price p):
   - `Purchase_vat_toggle_reprices_outbound_after_the_lock_only`:
     - In 10 × 100,000 with VAT 10% on 10-05 → Out 5 on 10-06 costs 550,000;
     - set `PurchaseCostIncludesVat` off → 500,000;
     - an In on 09-10 with lock 09-30 (a period end) keeps its `InValue`.
   - `Purchase_vat_toggle_requires_lock_on_period_end` (D33): lock 2026-10-10 → toggling the flag → 400 with `details.purchaseCostIncludesVat` and the Vietnamese `error.message`; nothing changes.
   - `Switching_scope_to_warehouse_recomputes_costs` (W1 In 10 × 100,000, W2 In 10 × 200,000, Out 5 each → 750,000 / 750,000 under the branch average 150,000, then after the switch 500,000 / 1,000,000)
   - `Switching_period_to_quarter_uses_quarter_average` — Oct In 10 × 100,000, Oct Out 5, Nov In 10 × 200,000, Nov Out 5. Monthly: Oct Out 500,000; Nov AvgCost 166,666.6667 → Out 833,333. Quarter: both Outs 750,000.
   - `Changing_period_requires_lock_on_new_period_end` (lock 2026-08-31 → Quarter → 400; lock 2026-09-30 → 200)
   - `Settings_change_without_ledger_data_only_saves`
   - Every test ends with `AssertInvariantsAsync()`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~SettingsRecalcTests"`. Expected: FAIL (costs unchanged after the PUT).
3. **Write the minimal implementation:** `ApplySettingsChangeAsync` and the `UpdateAsync` wiring (the PUT returns the updated `InventorySettingsDto`).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~SettingsRecalcTests|FullyQualifiedName~InventorySettingsTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): recalculate costs when costing settings change"`

### Task 6.4 — Stock-on-hand report

Contract (`InventoryReportsController`, route `api/reports`, guard `reports.inventory`):

```csharp
// GET /api/reports/stock-on-hand?at=&warehouseId=&productGroupId=&search=
public class StockOnHandReportDto
{
    public DateTimeOffset At { get; set; }
    public bool IsProvisional { get; set; }   // period containing VN date of At has not ended (End >= today VN)
    public bool CanViewCost { get; set; }     // inventory.view_cost
    public decimal? TotalValue { get; set; }
    public List<StockOnHandRowDto> Rows { get; set; } = new();
}
// auto-properties (shorthand below): Guid ProductId, string ProductCode, string ProductName, string? ProductGroupName, string UnitName,
// Guid WarehouseId, string WarehouseCode, string WarehouseName, decimal Quantity, decimal? Value
public class StockOnHandRowDto { /* members above, each { get; set; } */ }
```

Rules: the working branch only; `at` is normalized with `VnTime.ToUtc` (D27). Quantities: when `at` is omitted, `At = UtcNow` and `Quantity` comes from `StockBalances` (D21); when `at` is given, `Quantity = Σ(QtyIn − QtyOut)` over ledger rows with `PostedAt <= at`. Values follow the costing scope (D32):
- **Warehouse scope:** `Value = Σ(InValue − CostAmount)` over the pair's rows (up to `at` when given).
- **Branch scope:** per product, `scopeValue = Σ(InValue − CostAmount)` and `scopeQty = Σ(QtyIn − QtyOut)` over all the branch's rows (up to `at`); each warehouse row gets `R0(scopeValue × rowQty / scopeQty)`, the rounding residual goes to the product's last row by `WarehouseCode`; when `scopeQty == 0`, row values are 0. `TotalValue` is always `Σ scopeValue`.

`Value` and `TotalValue` are null without `inventory.view_cost`. Rows with `Quantity == 0 && (Value ?? 0) == 0` are dropped. Order by `ProductCode`, then `WarehouseCode`. `UnitName = StockUnit.NameFor`.

1. **Write the failing test** `Inventory/StockOnHandReportTests.cs : InventoryTestBase`:
   - `Current_and_point_in_time_snapshots` (no `at` → `StockBalance` quantities equal the ledger sums; with `at` → ledger sums up to `at`)
   - `Filters_branch_scope_and_provisional_flag`:
     - filters by warehouse, group and search;
     - only the working branch;
     - `at` = now → `IsProvisional` true; `at` = 2026-01-15 → false.
   - `Values_hidden_without_view_cost_and_permission_required` (WAREHOUSE user → values null; SALES → 403)
   - `Branch_scope_values_are_split_by_quantity` (D32, default Branch scope): KHO01 In 10 × 100,000, KHO02 In 10 × 200,000, Out 10 from KHO01 → KHO01 row dropped (qty 0, value 0), KHO02 qty 10 value 1,500,000, `TotalValue` 1,500,000 (not −500,000 / 2,000,000). Under Warehouse scope the same data gives KHO02 2,000,000.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockOnHandReportTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** DTOs, `InventoryReportService.GetStockOnHandAsync`, the controller action, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock-on-hand report"`

### Task 6.5 — Stock card report

Contract (same controller):

```csharp
// GET /api/reports/stock-card?productId=&warehouseId=&from=&to=   (from/to are VN dates, required, from <= to)
// Shorthand: every member below is a public auto-property { get; set; } (System.Text.Json ignores fields).
public class StockCardDto
{
    Guid ProductId; string ProductCode; string ProductName; string UnitName;
    DateOnly From; DateOnly To; bool IsProvisional; bool CanViewCost; bool ValuesAtScopeOnly;
    decimal OpeningQty; decimal? OpeningValue;
    decimal InQty; decimal OutQty; decimal? InValue; decimal? OutValue;
    decimal ClosingQty; decimal? ClosingValue;
    List<StockCardRowDto> Rows;
}
public class StockCardRowDto
{
    DateTimeOffset PostedAt; LedgerSourceType SourceType; Guid SourceId; string SourceCode;
    string? ReasonName; string? PartnerName; string WarehouseCode;
    decimal QtyIn; decimal QtyOut; decimal? UnitCost; decimal? InValue; decimal? CostAmount;
    decimal RunningQty; decimal? RunningValue;
}
```

Rules: the warehouse is optional (null = every warehouse of the working branch); the product must exist (404). Opening = sums before `VnTime.StartOfDay(from)`. Rows are in `[StartOfDay(from), StartOfNextDay(to))` (UTC instants, D27) in posting order, with `RunningQty` / `RunningValue` accumulated in memory over the selection. `UnitCost` = the row's `UnitCost` for outbound rows and `R4(InValue / QtyIn)` for inbound rows. `ReasonName` / `PartnerName` come from the voucher; opening rows have `SourceCode = "TDK"` and `ReasonName = "Tồn đầu kỳ"` (the frontend shows `ReasonName` and no link for them). Every value field is null without `inventory.view_cost`. **D32:** when `warehouseId` is given and the costing scope is Branch, `OpeningValue`, `RunningValue` and `ClosingValue` are null and `ValuesAtScopeOnly = true` (per-row `UnitCost` / `InValue` / `CostAmount` stay); value totals are only meaningful per costing scope. `IsProvisional` uses the period containing `to`.

1. **Write the failing test** `Inventory/StockCardReportTests.cs : InventoryTestBase`:
   - `Card_shows_opening_rows_running_and_closing`:
     - opening stock plus vouchers, with `from` in mid-month so the opening includes earlier rows;
     - a second call without `warehouseId` accumulates across warehouses.
   - `Excludes_cancelled_and_deleted_and_validates_range` (cancelled and deleted vouchers do not appear; `from > to` → 400)
   - `Values_hidden_without_view_cost_and_permission_required` (WAREHOUSE → values null; SALES → 403)
   - `Warehouse_filter_under_branch_scope_hides_running_values` (D32): with `warehouseId` and Branch scope → `ValuesAtScopeOnly == true`, running/opening/closing values null, row costs present; without `warehouseId` → running values present and `ClosingValue` equals the branch value.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockCardReportTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:** `GetStockCardAsync`, its validator, the controller action.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock card report"`

### Task 6.6 — Invariant suite (back-dated operations = re-entry from scratch)

1. **Write the failing test** `Inventory/InventoryInvariantTests.cs : InventoryTestBase`:
   - `AssertInvariantsAsync()` already lives in `InventoryTestBase` (Phase 04 Task 4.5) and is called by every scenario test.
   - Helper `SnapshotAsync()`: ledger rows projected to `(ProductId, WarehouseId, PostedAt, QtyIn, QtyOut, InValue, RunningQty, CostAmount)` and sorted; cost periods projected without ids; balances. Give every voucher of the script a distinct `VoucherAt` so the re-entered codes (which differ) never decide an ordering tie.
   - `Back_dated_edits_deletes_and_cancels_match_re_entry_from_scratch` (policy Allow, Branch scope, 2 products, `KHO01` + a second warehouse, Sep–Oct 2026):
     1. create about 8 vouchers via the API;
     2. edit an In from 10-10 to 09-05 with a new price; delete one Out; cancel one In and restore it; cancel another Out; change an Out quantity;
     3. take snapshot A and call `AssertInvariantsAsync()`;
     4. read the final Active vouchers (`GET` list + detail), then hard-delete every row via `InDbAsync` with `IgnoreQueryFilters().ExecuteDeleteAsync()` (query filters would otherwise skip soft-deleted rows and the Restrict FKs would fail), in this order: `stock_voucher_activities`, `stock_voucher_lines`, `stock_vouchers`, `inventory_ledger`, `inventory_cost_periods`, `stock_balances`, `document_counters`;
     5. re-create those vouchers via the API in `VoucherAt` order, take snapshot B, assert `A` equals `B` and call `AssertInvariantsAsync()`.
   - `Invariants_hold_under_warehouse_scope` (the same script with `CostingScope = Warehouse`)
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~InventoryInvariantTests"`. Expected: FAIL only if the engine has a defect. If it already passes, keep it as a regression guard and note that in the commit (no deliberate RED for a pure invariant suite).
3. **Write the minimal implementation:** fix any defect the suite exposes, in the Phase 04 / 05 services.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~Inventory"`. Expected: PASS.
5. **Commit:** `git commit -m "test(inventory): invariant suite for back-dated changes versus re-entry"`

## Verification

- `cd backend && dotnet build OrderMgmt.sln`
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Inventory"`
- `dotnet test OrderMgmt.sln`
- Optional manual check: `dotnet run --project src/OrderMgmt.WebApi`, then exercise `/swagger` for `/api/inventory/opening-stock`, `/api/inventory/recalc-cost`, `/api/reports/stock-on-hand` and `/api/reports/stock-card`.

## Exit Criteria

- Opening stock is posted to the ledger, honours lock and policy, and feeds costing.
- Manual recalculation is idempotent and repairs tampered costs; settings changes recalculate per D11.
- Both reports return correct quantities, value per costing scope (D32), mask values without `inventory.view_cost`, and flag provisional periods.
- Settings changes and manual recalculation hold only the exclusive branch gate (D30); a VAT-flag change with a mid-period lock is rejected (D33).
- The invariant suite passes under Branch and Warehouse scope.
- The full backend suite is green. The Round 1 backend is complete.
