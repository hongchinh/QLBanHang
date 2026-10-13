# Phase 03 — Inventory metrics, branch comparison, attention, recent documents

**Status:** [ ] pending
**Complexity:** L

## Objective

- Inventory domain: `inventory/summary`, `inventory/breakdown` (stock value, counts, negative stock, value by warehouse and product group, top products).
- Cross-domain routes: `branches` (branch comparison), `attention` (what needs action), `recent` (documents created today).
- Stock values equal the Tồn kho report.

## Files

Backend:
- `backend/src/OrderMgmt.Application/Inventory/Common/StockValueSplit.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Reports/Services/InventoryReportService.cs` (modify — `SplitByQuantity` moves out)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Models/InventoryModels.cs`, `OverviewModels.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Interfaces/IInventoryMetricsService.cs`, `IExecutiveOverviewService.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Interfaces/ISalesMetricsService.cs`, `ICashMetricsService.cs`, `IDebtMetricsService.cs` (modify — one or two methods each for the cross-domain routes)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/InventoryMetricsService.cs`, `ExecutiveOverviewService.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/SalesMetricsService.cs`, `CashMetricsService.cs`, `DebtMetricsService.cs` (modify)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify — two registrations)
- `backend/src/OrderMgmt.WebApi/Controllers/ExecutiveDashboardController.cs` (modify — five actions)

Tests:
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/Unit/StockValueSplitTests.cs` (new — pure helpers of `Inventory/Common` are tested there by convention)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/InventorySummaryTests.cs`, `InventoryBreakdownTests.cs`, `BranchComparisonTests.cs`, `AttentionTests.cs`, `RecentDocumentsTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Inventory/Reports/Services/InventoryReportService.cs` — stock on hand (lines 36–141), `SplitByQuantity` (250–264), masking with `inventory.view_cost`
- `backend/src/OrderMgmt.Application/Inventory/Common/StockUnit.cs` — `StockUnit.NameFor(pricingMode, unitName)`
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` — `CreateBranchAsync`, `CreateWarehouseAsync`, `CreateInventoryProductAsync`, `CreatePartnerAsync`, `VoucherRequest`, `LineRequest`, `PostVoucherAsync`, `CreateVoucherAsync`, `InDbAsync`
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/StockOnHandReportTests.cs` — posting tracked stock, the two costing scopes (lines 113–135) and `PutCostingScopeAsync` (lines 142–157), which changes the scope through `PUT /api/inventory/settings` so costs are recalculated. Do not use `UpdateInventorySettingsAsync` for that: it writes the database directly and recalculates nothing
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` — `CashRequest`, `CreateCashAsync` (and the acknowledge flag of a payment that makes a fund negative)
- Phases 01 and 02 of this plan — services, private loaders, `ExecutiveDashboardTestBase`

## Contracts

```csharp
// Application/Inventory/Common/StockValueSplit.cs
public static class StockValueSplit
{
    /// Branch costing scope (D32): each warehouse gets R0(scopeValue × qty / scopeQty); the rounding residual goes to the
    /// last warehouse holding stock. A zero scope quantity gives every warehouse 0.
    public static List<decimal> ByQuantity(decimal scopeValue, IReadOnlyList<decimal> quantities);
}

// Models/InventoryModels.cs
public class InventorySummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public bool CanViewCost { get; set; }
    public decimal? StockValue { get; set; }      // now; null without inventory.view_cost
    public int ProductCount { get; set; }         // products with a non-zero quantity
    public int WarehouseCount { get; set; }       // warehouses holding stock
    public int NegativeCount { get; set; }        // product × warehouse pairs below zero
    public decimal? InValue { get; set; }         // period: value received by stock-in vouchers
    public decimal? OutCost { get; set; }         // period: cost issued by stock-out vouchers
}
public class InventoryBreakdownDto
{
    public bool CanViewCost { get; set; }
    public List<StockGroupDto> ByWarehouse { get; set; } = new();
    public List<StockGroupDto> ByProductGroup { get; set; } = new();
    public List<NegativeStockRowDto> Negative { get; set; } = new();
    public List<StockProductRowDto> TopByValue { get; set; } = new();
}
public class StockGroupDto
{
    public Guid? Id { get; set; }                 // warehouse id or product group id; null: products without a group
    public string? Code { get; set; }             // warehouse code; null for product groups
    public string? Name { get; set; }
    public string? BranchCode { get; set; }       // warehouses only
    public int ProductCount { get; set; }
    public decimal? Value { get; set; }
}
public class NegativeStockRowDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
public class StockProductRowDto
{
    public Guid ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Value { get; set; }
}

// Models/OverviewModels.cs
public class BranchComparisonRowDto
{
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public decimal? Revenue { get; set; }         // period; null without reports.profit
    public decimal? MarginPct { get; set; }       // null without reports.profit or inventory.view_cost
    public decimal? Receivable { get; set; }      // balance at At; null without reports.debt
    public decimal? StockValue { get; set; }      // now; null without reports.inventory or inventory.view_cost
}
public class AttentionDto
{
    public DateOnly At { get; set; }
    public AttentionItemDto? OverdueReceivables { get; set; }   // Count = customers, Amount = overdue amount
    public AttentionItemDto? OverCreditLimit { get; set; }      // Count = customer × branch pairs, Amount = Σ Over
    public AttentionItemDto? PayablesDueSoon { get; set; }      // Count = documents due within 7 days, Amount
    public AttentionItemDto? NegativeStock { get; set; }        // Count = product × warehouse pairs, Amount = null
    public AttentionItemDto? NegativeFunds { get; set; }        // Count = funds, Amount = Σ |balance|
}
public class AttentionItemDto
{
    public int Count { get; set; }
    public decimal? Amount { get; set; }
}
public enum RecentDocumentKind { Receipt = 1, Payment = 2, StockIn = 3, StockOut = 4 }
public class RecentDocumentDto
{
    public RecentDocumentKind Kind { get; set; }
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string? PartnerName { get; set; }
    public string? OwnerName { get; set; }
    public decimal Amount { get; set; }
}

// Interfaces — new
public interface IInventoryMetricsService
{
    Task<InventorySummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<InventoryBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);
    /// Stock value now per branch of the scope. No permission check: the caller masks.
    Task<IReadOnlyDictionary<Guid, decimal>> GetValueByBranchAsync(ExecutiveScope scope, CancellationToken ct = default);
    /// Number of product × warehouse pairs below zero in the scope.
    Task<int> CountNegativeStockAsync(ExecutiveScope scope, CancellationToken ct = default);
}
public interface IExecutiveOverviewService
{
    Task<List<BranchComparisonRowDto>> GetBranchComparisonAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<AttentionDto> GetAttentionAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<List<RecentDocumentDto>> GetRecentAsync(RecentQuery query, CancellationToken ct = default);
}

// Interfaces — additions (no permission check inside; the overview service masks)
public sealed record SalesTotals(decimal Revenue, decimal Cost);
// ISalesMetricsService
Task<IReadOnlyDictionary<Guid, SalesTotals>> GetByBranchAsync(ExecutiveScope scope, ExecutivePeriod period, CancellationToken ct = default);
// IDebtMetricsService
public sealed record DebtAttention(int OverduePartners, decimal OverdueAmount, int OverLimitCount, decimal OverLimitAmount, int PayablesDueSoonCount, decimal PayablesDueSoonAmount);
Task<IReadOnlyDictionary<Guid, decimal>> GetReceivableByBranchAsync(ExecutiveScope scope, DateOnly at, CancellationToken ct = default);
Task<DebtAttention> GetAttentionAsync(ExecutiveScope scope, DateOnly at, CancellationToken ct = default);
// ICashMetricsService
public sealed record NegativeFunds(int Count, decimal Amount);
Task<NegativeFunds> GetNegativeFundsAsync(ExecutiveScope scope, DateOnly at, CancellationToken ct = default);
```

`InventoryMetricsService` injects `IAppDbContext`, `ICurrentUser`, `IExecutiveScopeResolver`, `IDateTime`, `ITransactionRunner`. `ExecutiveOverviewService` injects `IAppDbContext`, `ICurrentUser`, `IExecutiveScopeResolver`, `IDateTime`, `ITransactionRunner` and the four metrics services.

## Rules

### Inventory

- **Pairs:** `InventoryLedger` rows with `BranchId` in the scope, grouped by `BranchId`, `ProductId`, `WarehouseId`: `Qty = Sum(QtyIn − QtyOut)`, `Value = Sum(InValue − (CostAmount ?? 0))`. All rows count (no date filter), as in the Tồn kho report without `at`.
- **Quantities now:** `StockBalances` with `BranchId` in the scope.
- **`StockValue`** = the sum of `Value` over all pairs.
- **Counts:** `ProductCount` = distinct products with a non-zero balance quantity; `WarehouseCount` = distinct warehouses with a non-zero balance quantity; `NegativeCount` = balances with `Quantity < 0`.
- **Period:** over ledger rows with `PostedAt` in `[period.Start, period.End)`: `InValue` = Σ `InValue` of `LedgerSourceType.StockIn` rows (opening stock is not included); `OutCost` = Σ `CostAmount ?? 0` of `LedgerSourceType.StockOut` rows.
- **Value of a pair for the warehouse list:** with `InventorySettings.CostingScope == CostingScope.Branch`, for each branch and product take its pairs ordered by warehouse `Code` (ordinal) and split the product's total value with `StockValueSplit.ByQuantity(total, ledger quantities)`; with `CostingScope.Warehouse` the pair keeps its own `Value`.
- **By warehouse:** per warehouse, `Value` = Σ of the pair values above, `ProductCount` = its balances with a non-zero quantity. Drop rows with `ProductCount == 0` and `Value == 0`. Order by `BranchCode`, `Code`.
- **By product group:** per product, the sum of its pair `Value`s; group by `Product.ProductGroupId` (`Name` from the group, `Id` and `Name` null for products without one); `ProductCount` = products of the group with a non-zero total quantity. Order by `Value` descending, then `Name`.
- **Negative:** balances with `Quantity < 0`, with product and warehouse names (`IgnoreQueryFilters()`), `UnitName = StockUnit.NameFor(pricingMode, unitName)`; order by `BranchCode`, `WarehouseCode`, `ProductCode`; at most 50 rows.
- **Top by value:** per product, Σ `Value` descending, then `Code`; the first 10; `Quantity` = Σ of its balance quantities.
- **Cost visibility:** without `inventory.view_cost`: `StockValue`, `InValue`, `OutCost` and every `Value` are `null`, `TopByValue` is empty, the product-group list is ordered by `ProductCount` descending, and `CanViewCost` is `false`. Counts and the negative list are always returned.

### Branch comparison

- Scope: `ResolveAsync(query.BranchId)` (the route needs `branches.access_all`, so without `branchId` it is every branch). One row per branch of the scope, ordered by `BranchCode`.
- `Revenue` and `MarginPct` from `ISalesMetricsService.GetByBranchAsync` (`MarginPct = ExecutiveMath.MarginPct(revenue, revenue − cost)`); `Receivable` from `IDebtMetricsService.GetReceivableByBranchAsync`; `StockValue` from `IInventoryMetricsService.GetValueByBranchAsync`. A branch missing from a dictionary has 0.
- Masking by the caller's permissions, as the comments on `BranchComparisonRowDto` say. A domain the user may not see is not queried.

### Attention

- `OverdueReceivables`, `OverCreditLimit`, `PayablesDueSoon` come from `IDebtMetricsService.GetAttentionAsync(scope, at)` and are `null` without `reports.debt`:
  - overdue receivables: the receivable totals' `OverduePartnerCount` and `Overdue`;
  - over limit: the rows of the breakdown's over-limit rule (count and Σ `Over`);
  - payables due soon: payable items with `IsDueWithin(item, at, DebtPositionMath.DueSoonDays)` (count and Σ `Open`).
- `NegativeStock` = `CountNegativeStockAsync(scope)` with `Amount = null`; `null` without `reports.inventory`.
- `NegativeFunds` = funds of the scope whose balance at `at` is below zero (count and Σ `|balance|`); `null` without `reports.cash`.

### Recent documents

- "Today" starts at `VnTime.ToUtc(VnTime.StartOfDay(today))`.
- Cash rows (need `reports.cash`): `CashVouchers` with `Status == DocumentStatus.Active`, `SourceStockVoucherId == null`, `BranchId` in the scope, `CreatedAt` today. `Kind` from `Type`, `PartnerName = PartnerName ?? PersonName`, `Amount = Total`.
- Stock rows (need `reports.inventory`): `StockVouchers` with `Status == StockVoucherStatus.Active`, `BranchId` in the scope, `CreatedAt` today. `Kind` from `Type`, `Amount = Total`.
- Take `Limit` rows of each source by `CreatedAt` descending, merge, order by `CreatedAt` descending then `Code`, keep `Limit`.
- `OwnerName` from `Users.IgnoreQueryFilters()` by `OwnerUserId`; `BranchCode` from `Branches.IgnoreQueryFilters()`.

## Tasks

Set the test database first in every shell:
`export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_dash;Username=postgres;Password=1"`. Backend commands run from `backend/`.

**Stock scenario** (used by Tasks 3.2 and 3.3; build it in a private helper of the test class with the posting recipe of `SUMMARY.md`): a fresh branch X with two warehouses W1 and W2, a customer and a supplier; tracked products A and B (VAT 0); A keeps the group the helper gives it (call it G), B has its `ProductGroupId` cleared with `InDbAsync`; NMH A 10 × 100 into W1 and NMH A 10 × 100 into W2 on 10-02; NMH B 5 × 200 into W1 on 10-02; XBH A 4 × 150 from W1 on 10-04. Costs are written on posting: value of A = 2,000 − 400 = 1,600, value of B = 1,000, total 2,600; quantities A: W1 6, W2 10; B: W1 5. Every stock-out that goes below zero in these tasks sets `AcknowledgeNegativeStock = true`. Restricted users have default branch X.

### Task 3.1 — `StockValueSplit`

1. Write the failing unit test `Inventory/Unit/StockValueSplitTests`: `ByQuantity(100, [1, 1, 1]) == [33, 33, 34]`; `ByQuantity(100, [1, 3]) == [25, 75]`; `ByQuantity(100, [2, 0]) == [100, 0]`; `ByQuantity(100, [0, 0]) == [0, 0]`; `ByQuantity(1600, [6, 10]) == [600, 1000]`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~StockValueSplitTests"`. Expected: FAIL — build error, `StockValueSplit` does not exist.
3. Implement `StockValueSplit.ByQuantity` by moving the body of `InventoryReportService.SplitByQuantity` (lines 250–264, with its `R0`); delete the private method and call `StockValueSplit.ByQuantity` at its call site.
4. Run the same command, then `--filter "FullyQualifiedName~StockOnHandReportTests|FullyQualifiedName~StockCardReportTests|FullyQualifiedName~InventoryReportReviewFixTests"`. Expected: PASS.
5. Commit: `git commit -m "refactor(reports): share the stock value split"`.

### Task 3.2 — `inventory/summary`

1. Write the failing test `InventorySummaryTests` (stock scenario, admin):
   - `Stock_value_equals_the_stock_on_hand_report`: `inventory/summary?{October}&branchId={X}` → `StockValue 2600`, `ProductCount 2`, `WarehouseCount 2`, `NegativeCount 0`, `CanViewCost true`; and `StockValue` equals `TotalValue` of `GET /api/reports/stock-on-hand` called with a client on branch X.
   - `Period_in_and_out`: same request → `InValue 3000`, `OutCost 400`; `inventory/summary?from=2026-11-01&to=2026-11-30&branchId={X}` → both 0 and `StockValue 2600`.
   - `Company_wide_sums_branches`: a second fresh branch Y with a warehouse and NMH of a tracked product E 2 × 50; without `branchId` → `StockValue 2700`, `ProductCount 3`, `WarehouseCount 3`; with `branchId={Y}` → `StockValue 100`.
   - `Negative_stock_is_counted`: XBH 3 of a tracked product C that has no stock, from W1, with `AcknowledgeNegativeStock = true` → `NegativeCount 1`.
   - `Values_are_hidden_without_view_cost`: a user with `dashboard.executive` and `reports.inventory` (default branch X) → `StockValue`, `InValue`, `OutCost` null, `CanViewCost false`, `ProductCount 2`.
   - `Requires_reports_inventory`: 403 for a user with only `dashboard.executive`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~InventorySummaryTests"`. Expected: FAIL — build error, `InventorySummaryDto` does not exist.
3. Implement `Models/InventoryModels.cs`, `IInventoryMetricsService` (the two public DTO methods), `InventoryMetricsService.GetSummaryAsync`, the action `GET inventory/summary` and the `AddScoped` line.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): inventory summary endpoint"`.

### Task 3.3 — `inventory/breakdown`

1. Write the failing test `InventoryBreakdownTests` (stock scenario, admin, `inventory/breakdown?branchId={X}`):
   - `By_warehouse_under_branch_costing`: W1 → `Value 1600`, `ProductCount 2`; W2 → `Value 1000`, `ProductCount 1` (A's 1,600 is split 600 / 1,000 by quantity).
   - `By_warehouse_equals_the_report_under_both_scopes`: add a tracked product D to the scenario: NMH D 10 × 100 into W1 and NMH D 10 × 200 into W2 on 10-02, XBH D 10 × 250 from W1 on 10-04. Under the default branch scope D's average cost is 150 and its value of 1,500 goes to W2 by quantity → W1 `Value 1600`, W2 `Value 2500`. After `PUT /api/inventory/settings` to `CostingScope.Warehouse` (copy `PutCostingScopeAsync`) → W1 `Value 1600`, W2 `Value 3000`. In both states each warehouse's `Value` equals the sum of `Value` of the stock-on-hand report rows of that warehouse, and `StockValue` of `inventory/summary` equals the report's `TotalValue` (4,100, then 4,600).
   - `By_product_group`: group G → `Value 1600`, `ProductCount 1`; the row with `Id null` → `Value 1000`, `ProductCount 1`.
   - `Top_by_value`: first row product A, `Value 1600`, `Quantity 16`; second row product B, `Value 1000`, `Quantity 5`.
   - `Negative_rows`: after an XBH 3 of a tracked product C without stock from W1 (`AcknowledgeNegativeStock = true`) → one row: product C, warehouse W1, `Quantity -3`.
   - `Values_are_hidden_without_view_cost`: a user without `inventory.view_cost` → every `Value` null, `TopByValue` empty, `CanViewCost false`, the warehouse rows still present with their `ProductCount`.
   - `Requires_reports_inventory`: 403.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~InventoryBreakdownTests"`. Expected: FAIL — 404 (no route).
3. Implement `GetBreakdownAsync` and the action `GET inventory/breakdown`.
4. Run `--filter "FullyQualifiedName~InventoryBreakdownTests|FullyQualifiedName~InventorySummaryTests"`. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): inventory breakdown endpoint"`.

### Task 3.4 — `branches`

1. Write the failing test `BranchComparisonTests` (`ReportDataset`, `branches?{October}`):
   - `One_row_per_branch`: admin → the row of `MainBranchId` has `Revenue 2500`, `MarginPct 100`, `Receivable 1500`, `StockValue 0`; the row of `BranchBId` has `Revenue 9999`, `Receivable 9999`; the seeded branch `CN01` has `Revenue 0`, `MarginPct null`, `Receivable 0`, `StockValue 0`; rows are ordered by `BranchCode`.
   - `One_branch_when_named`: `branchId={main}` → exactly one row.
   - `Requires_access_all`: a user with `dashboard.executive`, `reports.profit`, `reports.debt`, `reports.inventory` but without `branches.access_all` → 403.
   - `Columns_follow_the_domain_permissions`: a user with `dashboard.executive`, `branches.access_all`, `reports.debt` → `Receivable` set, `Revenue`, `MarginPct`, `StockValue` null on every row.
   - `Margin_needs_view_cost`: a user with `dashboard.executive`, `branches.access_all`, `reports.profit` → `Revenue` set, `MarginPct` null.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~BranchComparisonTests"`. Expected: FAIL — build error, `BranchComparisonRowDto` does not exist.
3. Implement `Models/OverviewModels.cs`, `IExecutiveOverviewService`, `GetByBranchAsync`, `GetReceivableByBranchAsync`, `GetValueByBranchAsync`, `ExecutiveOverviewService.GetBranchComparisonAsync`, the action `GET branches` (`[HasPermission(Permissions.Branches.AccessAll)]`) and the `AddScoped` line.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): branch comparison endpoint"`.

### Task 3.5 — `attention`

1. Write the failing test `AttentionTests` (`ReportDataset`, `attention?at=2026-10-31&branchId={main}` unless stated):
   - `Debt_items_of_the_dataset`: admin → `OverdueReceivables { Count 1, Amount 3000 }`, `OverCreditLimit { Count 0, Amount 0 }`, `PayablesDueSoon { Count 0, Amount 0 }`, `NegativeStock { Count 0, Amount null }`, `NegativeFunds { Count 0, Amount 0 }`.
   - `Over_credit_limit`: set the limit of `Kh1Id` to 1,000 in the main branch → `OverCreditLimit { Count 1, Amount 500 }`.
   - `Payables_due_soon`: one more unpaid NMH of 800 from `Ncc1Id` on 10-20 with `DueDate 2026-10-25` (an untracked product, built by hand as in the posting recipe); `attention?at=2026-10-21&branchId={main}` → `PayablesDueSoon { Count 1, Amount 800 }`.
   - `Negative_fund`: a payment of 50,000 from the fund `QtmId` on 10-20 with `AcknowledgeNegativeFund = true` → `NegativeFunds { Count 1, Amount 30700 }`.
   - `Negative_stock`: an XBH 3 of a tracked product without stock from `MainWarehouseId` to `Kh1Id`, with `AcknowledgeNegativeStock = true` → `NegativeStock { Count 1, Amount null }`.
   - `Parts_follow_the_permissions`: a user with `dashboard.executive` and `reports.cash` (default branch main) → the three debt items and `NegativeStock` are null, `NegativeFunds` is not.
   - `Needs_only_dashboard_executive`: a user with only `dashboard.executive` → 200 with all five items null.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~AttentionTests"`. Expected: FAIL — 404 (no route).
3. Implement `IDebtMetricsService.GetAttentionAsync`, `ICashMetricsService.GetNegativeFundsAsync`, `IInventoryMetricsService.CountNegativeStockAsync`, `ExecutiveOverviewService.GetAttentionAsync` and the action `GET attention`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): attention endpoint"`.

### Task 3.6 — `recent`

1. Write the failing test `RecentDocumentsTests` (`ReportDataset`; every dataset document is created during the test, so it is "today"):
   - `Documents_created_today_company_wide`: admin, `recent?limit=50` → 7 rows: codes `PT00002`, `PC00002`, `PC00003` and the four stock vouchers (`SaleId`, `Return6Id`, `Purchase7Id`, `BranchBSaleId`); `PT00001` and `PC00001` (automatic) and `PT00003` (cancelled) are absent; rows are ordered by `CreatedAt` descending; the row of `SaleId` has `Kind StockOut`, `Amount 3000`, `PartnerName` of KH1 and `OwnerName` of the admin.
   - `One_branch`: `recent?limit=50&branchId={main}` → 6 rows, none of `BranchBSaleId`.
   - `Deleted_documents_are_left_out`: post one more unpaid XBH of 700 in the main branch and one more receipt of 200, then delete both (`DELETE /api/stock-vouchers/{id}`, `DeleteCashAsync`) → `recent?limit=50&branchId={main}` still has 6 rows.
   - `Limit`: `recent?limit=2` → 2 rows; `recent?limit=0` → 400 with a `limit` detail; `recent` without `limit` → at most 10 rows.
   - `Rows_follow_the_permissions`: a user with `dashboard.executive` and `reports.cash` (default branch main) → only `Receipt` and `Payment` rows; a user with only `dashboard.executive` → 200 and an empty list.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~RecentDocumentsTests"`. Expected: FAIL — 404 (no route).
3. Implement `ExecutiveOverviewService.GetRecentAsync`, `RecentQueryValidator` in `ExecutiveQueryValidators.cs` (`Limit` from 1 to 50 → key `limit`, "Số dòng từ 1 đến 50.") and the action `GET recent` (with `IValidator<RecentQuery>`).
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): recent documents endpoint"`.

## Verification

From `backend/`, with `TEST_DB_CONNECTION` set:

- `dotnet build OrderMgmt.sln` — 0 errors.
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard|FullyQualifiedName~StockValueSplitTests|FullyQualifiedName~CostProvisionalTests"` — all pass (Phases 01–03).
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~StockOnHandReportTests|FullyQualifiedName~StockCardReportTests|FullyQualifiedName~InventoryReportReviewFixTests"` — pass.

## Exit Criteria

- All 13 routes of `SUMMARY.md` exist and return the dataset figures.
- Stock values equal the Tồn kho report under both costing scopes.
- `InventoryReportService` returns the same results as before.
- Six commits, one per task.
