# Phase 02c — Lane C: demo branch hard delete and company scope

**Status:** [x] done
**Complexity:** L
**Runs in parallel with:** 02a, 02b, 02d · **Worktree:** `.claude/worktrees/sd-clean` on `feat/branch-sample-data-clean` from `$P01` · **Test DB:** `qldonhang_integtest_sd_clean`

## Objective

- `IDemoBranchService`: a deletion preview, and a hard delete of a demo branch with every row that belongs to it, with these side effects (SUMMARY S14):
  - users moved to the main branch;
  - real products' cost price recomputed;
  - unused sample catalog removed when the last demo branch goes.

  Endpoints: `GET api/branches/{id:guid}/demo-deletion` and `DELETE api/branches/{id:guid}/demo`.
- Company-scope TT133 reports and the company-wide executive dashboard skip demo branches (SUMMARY S15).

Tests build demo branches by hand (create a branch, flip `IsDemo` with `InDbAsync`, add documents through the API), so this lane does not wait for Lanes A and B.

## Files (this lane only — touch nothing else)

- `backend/src/OrderMgmt.Application/Organization/SampleData/Services/DemoBranchService.cs` (replace the skeleton)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/ProductCostPrices.cs` (new — extracted from `StockVoucherService.RecomputeCostPricesAsync`)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherService.cs` (modify — call `ProductCostPrices.RecomputeAsync`, delete the private copy)
- `backend/src/OrderMgmt.WebApi/Controllers/BranchesController.cs` (modify — two actions; **no** `[DashboardSignal]`, see S18)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/CashDebtReportService.cs` (modify — 3 company filters)
- `backend/src/OrderMgmt.Application/Inventory/Reports/Services/InventoryReportService.cs` (modify — `BuildStockSummaryAsync` company filter)
- `backend/src/OrderMgmt.Application/Tt133/Services/Tt133ExportService.cs` (modify — company opening date)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/ExecutiveScopeResolver.cs` (modify — company branch ids)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/DemoBranchFixture.cs` (new — builders for demo data)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/DemoBranchPreviewTests.cs`, `DemoBranchDeleteTests.cs`, `DemoBranchCostPriceTests.cs`, `DemoBranchCatalogCleanupTests.cs`, `CompanyScopeDemoTests.cs`, `ExecutiveDemoExclusionTests.cs` (new)

## Reference files (read-only)

- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/ReportDataset.cs` — main branch plus branch B, with known company figures.
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/ExecutiveDashboardTestBase.cs`:
  - `CreateFreshBranchAsync`, `SeedTrackedSaleAsync`, `PostPurchaseAsync`, `SetLimitAsync`, `GetExecAsync<T>`;
  - `BranchComparisonTests.One_row_per_branch`;
  - `ExecutiveRouteGuardTests` (route table).
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/DebtLedgerExportTests.cs` — `Tt133Sheet.Contains(ws, "- Ngày mở sổ: 15/09/2026")`.
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryInvariantTests.cs:95-101` — `IgnoreQueryFilters().ExecuteDeleteAsync()` examples.
- `backend/src/OrderMgmt.Application/Inventory/Costing/InventoryRecalcService.cs` — exclusive branch gate inside `ITransactionRunner.RunAsync`.
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` — `SetLockAsync` (lock pattern), `DeleteAsync` (messages).

## Rules

**Guards** (preview and delete):
- unknown or soft-deleted branch → `NotFoundException`;
- `!IsDemo` → `ConflictException("NOT_A_DEMO_BRANCH", "Chỉ xóa được chi nhánh demo bằng chức năng này.")`;
- `SampleDataStatus == Running` → `ConflictException("SAMPLE_DATA_RUNNING", "Dữ liệu mẫu đang được tạo. Hãy chờ xong rồi xóa.")`.

**Preview:**
- `DocumentCount` = number of `StockVouchers` + `CashVouchers` + `FundTransfers` + `DebtOffsetVouchers` of the branch, with `IgnoreQueryFilters` (automatic vouchers, cancelled and soft-deleted ones included);
- `ReassignedUserCount` = `Users.IgnoreQueryFilters().Count(u => u.DefaultBranchId == id)`.

**Delete:** one `ITransactionRunner.RunAsync`. Step 0, then steps 1–17 below.

**0. Locks and checks.**
- `IInventoryLock.AcquireBranchGateAsync(new[] { id }, exclusive: true)`, then re-check the guards.
- Collect:
  - `realStockInProducts`: distinct `ProductId` of lines of the branch's `In` stock vouchers whose product has `IsSample == false`;
  - `sampleProducts`: every `IsSample` product id.
- `IInventoryLock.AcquireProductsAsync(realStockInProducts ∪ sampleProducts)`.
- If any `StockVoucher`, `StockVoucherLine` or `OpeningStock` of **another** branch references one of this branch's warehouses, throw `ConflictException("DEMO_WAREHOUSE_IN_USE", "Kho của chi nhánh demo đang được chi nhánh khác dùng.")`. Nothing has been deleted at that point.

**Deletes and updates.** Every query uses `IgnoreQueryFilters()` and `ExecuteDeleteAsync` / `ExecuteUpdateAsync`. Capture the id sets first as `IQueryable` subqueries.
1. `DebtSettlements` whose `IncreaseEntryId` or `DecreaseEntryId` is an entry of the branch.
2. `DebtEntries` of the branch.
3. `CashDocumentActivities` whose `DocumentId` is a cash voucher, fund transfer or debt offset of the branch.
4. `CashVoucherLines` of the branch's cash vouchers.
5. `CashVouchers` of the branch. This must come before the stock vouchers: FK `source_stock_voucher_id`.
6. `FundTransfers`, then `DebtOffsetVouchers` of the branch.
7. `StockVoucherActivities`, then `StockVoucherLines` of the branch's stock vouchers.
8. `StockVouchers` of the branch.
9. `InventoryLedger`, `InventoryCostPeriods`, `StockBalances` with `BranchId == id`.
10. `OpeningStocks`, `OpeningDebts`, `CreditLimits` of the branch.
11. `Funds` of the branch.
12. `Warehouses` of the branch.
13. `DocumentCounters`, `DocumentNumberings` of the branch.
14. `Users` with `DefaultBranchId == id` → `DefaultBranchId = BranchDefaults.MainBranchId`, `UpdatedAt = clock.UtcNow`. Count them for the result.
15. `Branches` row `id`.

**Afterwards:**

16. `await ProductCostPrices.RecomputeAsync(_db, realStockInProducts, ct)`, then `SaveChangesAsync`.
17. If `!await _db.Branches.AnyAsync(b => b.IsDemo)` (query filter on; soft-deleted rows are ignored), clean the sample catalog. All checks use `IgnoreQueryFilters`.
    - **Products:** an `IsSample` product is kept when any of these reference it: `StockVoucherLines`, `OpeningStocks`, `QuotationLines`, `InventoryLedger`, `StockBalances`, `InventoryCostPeriods`. Otherwise it is hard-deleted.
    - **Partners:** an `IsSample` partner is kept when any of these reference it: `StockVouchers`, `CashVouchers`, `DebtEntries`, `DebtOffsetVouchers`, `OpeningDebts`, `CreditLimits`, `Quotations`. Otherwise its `CustomerAddresses` and then the partner are hard-deleted.
    - **Group:** `DM` is deleted when no product (`IgnoreQueryFilters`) has it.
    - Result counts: `KeptSampleProductCount`, `KeptSamplePartnerCount`. Both are 0 when the cleanup did not run.

**`ProductCostPrices.RecomputeAsync(IAppDbContext db, IEnumerable<Guid> productIds, CancellationToken ct)`** is the body of today's `StockVoucherService.RecomputeCostPricesAsync` (lines ~1057–1085), moved verbatim. `StockVoucherService` calls it with `_db`. The existing behaviour stays: a product with no active stock-in left keeps its `CostPrice`, and its `CostPriceUpdatedOn` becomes null (S17).

**Company scope (S15):**
- `CashDebtReportService`: in `BuildDebtBalanceAsync`, `BuildDebtLedgerAsync` and `BuildDebtReconciliationAsync`, where the code reads `if (request.Scope == ReportScope.Branch) X = X.Where(e => e.BranchId == branchId);`, add `else X = X.Where(e => !_db.Branches.Any(b => b.Id == e.BranchId && b.IsDemo));`.
- `InventoryReportService.BuildStockSummaryAsync`: the same on `ledger`.
- `Tt133ExportService.BuildContextAsync`: the company opening-date query gains `&& !b.IsDemo`.
- `ExecutiveScopeResolver.ResolveAsync`: the company query becomes `_db.Branches.AsNoTracking().Where(b => !b.IsDemo).OrderBy(b => b.Code).Select(b => b.Id)`. Naming a demo branch explicitly keeps working.

## Tasks

DB tests: `[Collection(nameof(PostgresCollection))]`. `ExecutiveDemoExclusionTests` inherits `ExecutiveDashboardTestBase`; the others inherit `CashDebtTestBase`.

`DemoBranchFixture` (static helpers taking the test base):
- `CreateDemoBranchAsync(code)`: `POST /api/branches`, then `InDbAsync` sets `IsDemo = true`, `SampleDataStatus = Ready`. Returns the id and an admin client with `UseBranch`.
- `FillAsync(branchId, client)`, which in that branch creates:
  - a warehouse and a bank fund;
  - opening date (`Vn` dates in `2026-09`); fund opening amounts; opening stock (2 products); two opening debts (a receivable `Debt` and a receivable `Prepayment`);
  - a credit limit;
  - a cash sale `TM` (automatic receipt and settlement); a credit sale; a receipt `TKH` allocated to the credit sale;
  - a return `NTL`; an `XKH`;
  - a credit purchase from a dual partner and a credit sale to it, plus a debt offset between them;
  - a fund transfer; an expense `CK` that is then cancelled.

  It returns the ids it created (vouchers, entries).

Reuse the existing helpers (`CreateWarehouseAsync`, `CreateBankFundAsync`, `CreatePartnerAsync`, `CreateOpeningDebtAsync`, `PostCashAsync`, `PostTransferAsync`, the stock voucher helpers in `InventoryTestBase` / `ExecutiveDashboardTestBase`); do not write new HTTP plumbing if one exists.

### Task C.1 — Preview endpoint and guards

1. **Write the failing tests** `DemoBranchPreviewTests`:
   - `Preview_counts_documents_and_users`:
     - demo branch `DP01`, filled with **only** a credit sale, a `TKH` receipt allocated to it and a fund transfer, plus a user created with `CreateTestUserAsync("dp_user", …, RoleCodes.Sales, defaultBranchId: DP01)`;
     - `GET /api/branches/{DP01}/demo-deletion` → 200 `{ documentCount: 3, reassignedUserCount: 1 }`.
   - `Preview_refuses_a_normal_branch`: a branch created without the flag → 409, `error.code == "NOT_A_DEMO_BRANCH"`.
   - `Preview_refuses_a_running_branch`: demo branch with `SampleDataStatus = Running` → 409 `SAMPLE_DATA_RUNNING`.
   - `Preview_of_unknown_branch_is_404`.
   - `Preview_needs_branches_manage`: `CreateClientWithPermissionsAsync("dp_noperm", null, Permissions.Reports.Debt)` (any permission except `branches.manage`) → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DemoBranchPreviewTests"`. Expected: FAIL — 404/405 on the route (no action).
3. **Write the minimal implementation:**
   - `BranchesController` gets `IDemoBranchService demoBranches` in its constructor and two actions, both `[HasPermission(Permissions.Branches.Manage)]`:
     - `[HttpGet("{id:guid}/demo-deletion")]` → `Success(await _demoBranches.PreviewDeletionAsync(id, ct))`;
     - `[HttpDelete("{id:guid}/demo")]` → `Success(await _demoBranches.DeleteAsync(id, ct))`.
   - `DemoBranchService.PreviewDeletionAsync` with the guards. Leave `DeleteAsync` throwing until C.2.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(branches): demo branch deletion preview"` (after `git add`).

### Task C.2 — Hard delete

1. **Write the failing tests** `DemoBranchDeleteTests`:
   - `Delete_removes_every_row_of_the_demo_branch_and_nothing_else`:
     1. `ReportDataset.SeedAsync(this)` (main branch and branch B).
     2. Take a snapshot `S0`: for every table that has a `branch_id` column, and for `debt_settlements`, `cash_voucher_lines`, `stock_voucher_lines`, `stock_voucher_activities`, `cash_document_activities`, the row count and (where present) `sum(amount)` / `sum(quantity)` per branch other than the demo. The table list comes from:
        ```sql
        select table_name from information_schema.columns
        where table_schema = 'public' and column_name = 'branch_id'
        ```
        It is read at run time, so a future table is covered too (R4). Children are counted through their parent's `branch_id`.
     3. Create demo branch `DD01` with `FillAsync`, and a user `dd_user` whose default branch is `DD01`.
     4. `DELETE /api/branches/{DD01}/demo` → 200 with `reassignedUserCount == 1`.

     Then:
     - every `branch_id` table has 0 rows for `DD01`, and the children captured from the fill have 0 rows (`IgnoreQueryFilters`, or raw SQL counts);
     - `branches` has no `DD01` row (raw SQL, so soft-deleted rows count);
     - snapshot `S1` == `S0`;
     - `dd_user.DefaultBranchId == BranchDefaults.MainBranchId`;
     - `AssertInvariantsAsync()` and `AssertCashDebtInvariantsAsync()` pass.
   - `Delete_refuses_normal_running_and_unknown_branches`:
     - a normal branch with a warehouse → 409 `NOT_A_DEMO_BRANCH` and the warehouse still exists;
     - a `Running` demo branch → 409 `SAMPLE_DATA_RUNNING`;
     - unknown id → 404;
     - a client without `branches.manage` → 403.
   - `Delete_refuses_when_another_branch_uses_its_warehouse`:
     - demo branch `DD02` with a warehouse;
     - insert, through `InDbAsync`, an `OpeningStock` row of the **main** branch on that warehouse (a test-only corruption that stands in for a warehouse moved between branches);
     - delete → 409 `DEMO_WAREHOUSE_IN_USE`, and `DD02` still has its warehouse.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DemoBranchDeleteTests"`. Expected: FAIL — `NotImplementedException` (500).
3. **Write the minimal implementation:** steps 0–15 of the rules (no cost price recompute, no catalog cleanup yet).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(branches): hard delete of a demo branch"` (after `git add`).

### Task C.3 — Real product cost price after the delete

1. **Write the failing test** `DemoBranchCostPriceTests.Delete_recomputes_cost_price_of_real_products`:
   - a real product `CP-REAL` (`IsSample == false`);
   - a stock-in in the **main** branch at 40,000 on `Vn("2026-09-10 09:00")`;
   - demo branch `DC01` with a warehouse, opening date `2026-09-01`, and a stock-in of `CP-REAL` at 55,000 on `Vn("2026-09-20 09:00")`. `Product.CostPrice` is now 55,000.
   - Delete `DC01` → `CostPrice == 40,000` and `CostPriceUpdatedOn == Vn("2026-09-10 09:00")`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~DemoBranchCostPriceTests"`. Expected: FAIL — `CostPrice` is still 55,000.
3. **Write the minimal implementation:**
   - Move the method into `ProductCostPrices.RecomputeAsync` (public static) and make `StockVoucherService` call it.
   - In `DeleteAsync`, collect `realStockInProducts` and lock them in step 0, then run step 16.
4. **Run the tests to verify they pass:** same filter, then `--filter "FullyQualifiedName~CostPrice|FullyQualifiedName~StockVoucher"`. Expected: PASS, and the stock voucher tests as in `BASELINE.md`.
5. **Commit:** `git commit -m "feat(branches): restore real cost prices after a demo delete"` (after `git add`).

### Task C.4 — Sample catalog cleanup

1. **Write the failing tests** `DemoBranchCatalogCleanupTests`.

   Setup through `InDbAsync`:
   - the group `DM` (`Name = "Dữ liệu mẫu"`);
   - sample products `DM-SPA`, `DM-SPB`, `DM-SPC` (`IsSample`, group `DM`);
   - sample partners `DM-KHA` (with one `CustomerAddress`) and `DM-KHB` (`IsSample`, customers).

   Use through the API:
   - two demo branches `DK01` and `DK02` each post a sale of `DM-SPA` to `DM-KHA` (after opening stock);
   - the **main** branch posts a stock-in of `DM-SPB` and an opening debt for `DM-KHB`.

   Tests:
   - `First_delete_keeps_the_catalog_while_a_demo_branch_remains`: delete `DK01` → every sample row still exists; `keptSampleProductCount == 0`, `keptSamplePartnerCount == 0`.
   - `Last_delete_removes_unused_sample_rows`: delete `DK01`, then `DK02`.
     - `DM-SPA`, `DM-SPC`, `DM-KHA` and its address are gone (`IgnoreQueryFilters` counts 0);
     - `DM-SPB` and `DM-KHB` remain; the result has `keptSampleProductCount == 1`, `keptSamplePartnerCount == 1`;
     - group `DM` remains (it still holds `DM-SPB`).
   - `Group_goes_when_no_product_is_left`: with only `DM-SPA` (used by one demo branch) and the group, deleting that branch removes the product and the group.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DemoBranchCatalogCleanupTests"`. Expected: FAIL — sample rows are still there after the last delete.
3. **Write the minimal implementation:** step 17 and the result counts; add `sampleProducts` to the locks of step 0.
4. **Run the tests to verify they pass:** same filter, then `--filter "FullyQualifiedName~DemoBranch"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(branches): remove unused sample catalog with the last demo branch"` (after `git add`).

### Task C.5 — Company-scope reports skip demo branches

1. **Write the failing tests** `CompanyScopeDemoTests`. For each test: `ReportDataset.SeedAsync(this)`; read the **company** result, then the **main-branch** result; set branch B `IsDemo = true` with `InDbAsync`; read the company result again.
   - `Debt_balance_company_skips_demo_branch`: `GET /api/reports/debt-balance?side=Receivable&scope=Company` (same query parameters as the existing company tests in `CashDebt/Reports`). Before: the total differs from main's (B's 9,999 sale is in). After: the totals equal main's.
   - `Debt_ledger_company_skips_demo_branch`: the shared customer's ledger with `scope=Company`. After: no row has `branchCode == B's code`, and the closing balance equals main's.
   - `Debt_reconciliation_company_skips_demo_branch`: the same comparison on the reconciliation totals.
   - `Stock_summary_company_skips_demo_branch`: `GET /api/reports/stock-summary?scope=Company&from=…&to=…`. After: the totals equal the main branch's.
   - `Tt133_company_opening_date_skips_demo_branch`:
     - set B's `OpeningDate` one month earlier than main's (`InDbAsync`), so the company export shows B's date;
     - export S12 company scope (`POST /api/reports/debt-ledger/export` as in `Tt133/DebtLedgerExportTests.cs`) → `"- Ngày mở sổ: <B's date>"`;
     - mark B demo, export again → `"- Ngày mở sổ: <main's date>"`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CompanyScopeDemoTests"`. Expected: FAIL — the "after" figures still include B.
3. **Write the minimal implementation:** the four filters and the opening-date filter (rules, Company scope).
4. **Run the tests to verify they pass:** same filter, then `--filter "FullyQualifiedName~CashDebt.Reports|FullyQualifiedName~Tt133|FullyQualifiedName~StockSummary"`. Expected: PASS, and the existing report tests as in `BASELINE.md`.
5. **Commit:** `git commit -m "feat(reports): company scope skips demo branches"` (after `git add`).

### Task C.6 — Executive dashboard company scope skips demo branches

1. **Write the failing tests** `ExecutiveDemoExclusionTests` (`: ExecutiveDashboardTestBase`), each with `ReportDataset.SeedAsync(this)` as in `BranchComparisonTests`:
   - `Branch_comparison_skips_demo_branch`: `GET api/dashboard/executive/branches` → B's row is present; mark B demo → B's row is gone, and the main branch row is unchanged.
   - `Company_sales_summary_skips_demo_branch`: the sales summary route (`ExecutiveRouteGuardTests` route table) with no `branchId` → after marking B demo it equals the same route with `branchId = main`.
   - `Company_debt_and_inventory_skip_demo_branch`: the same before/after comparison on the debt summary and the inventory summary routes.
   - `Demo_branch_can_still_be_viewed_on_its_own`: after marking B demo, the sales summary with `branchId = B` → 200 with B's figures (as before marking).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~ExecutiveDemoExclusionTests"`. Expected: FAIL — B still in the company figures and the comparison.
3. **Write the minimal implementation:** the `.Where(b => !b.IsDemo)` in `ExecutiveScopeResolver`.
4. **Run the tests to verify they pass:** same filter, then `--filter "FullyQualifiedName~ExecutiveDashboard"`. Expected: PASS, the executive tests as in `BASELINE.md`.
5. **Commit:** `git commit -m "feat(dashboard): company-wide executive figures skip demo branches"` (after `git add`).

### Task C.7 — Lane checkpoint

1. `dotnet build OrderMgmt.sln`; `--filter "FullyQualifiedName~SampleData|FullyQualifiedName~ExecutiveDashboard|FullyQualifiedName~CashDebt|FullyQualifiedName~Tt133|FullyQualifiedName~Inventory"`. Compare the failures with `BASELINE.md` by name.
2. `grep -n "RecomputeCostPricesAsync" backend/src -r` is empty (the private copy is gone).
3. Report: test counts, the measured delete time of `DD01` (log `Stopwatch` in the test output).

## Verification

```bash
cd /D/Projects/QLBanHang/.claude/worktrees/sd-clean/backend
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_sd_clean;Username=postgres;Password=1"
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DemoBranch|FullyQualifiedName~CompanyScopeDemoTests|FullyQualifiedName~ExecutiveDemoExclusionTests"
```

## Exit Criteria

- The preview and the delete work only on demo branches that are not running, and need `branches.manage`.
- After a delete:
  - no row of any `branch_id` table or child table belongs to the branch;
  - other branches are unchanged;
  - users are moved to the main branch;
  - real cost prices are recomputed;
  - the last delete removes the unused sample catalog.
- Company-scope debt and stock reports, the TT133 company opening date and the company-wide executive dashboard ignore demo branches; a demo branch named explicitly still works.
- Only the files listed above changed.
