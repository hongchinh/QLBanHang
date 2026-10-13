# Phase 01 — Backend foundation and sales metrics

**Status:** [x] complete (2026-10-14, commits e28faf5..b2b539d; spec review compliant, quality review approved after two fix rounds)
**Complexity:** L

## Objective

- Add the permission `dashboard.executive`.
- Add the shared pieces of the module: query classes, period, scope resolver, validators, controller.
- Deliver the sales domain: `sales/summary`, `sales/series`, `sales/top` (revenue, cost, gross profit, margin from XBH and NTL stock vouchers).

## Files

Backend:
- `backend/src/OrderMgmt.Domain/Constants/Permissions.cs` (modify — `Dashboard` class)
- `backend/src/OrderMgmt.Domain/Constants/StockReasonCodes.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify — one permission definition)
- `backend/src/OrderMgmt.Application/Inventory/Common/CostProvisional.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Reports/Services/InventoryReportService.cs` (modify — `IsProvisional` delegates)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Models/ExecutiveModels.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Models/SalesModels.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Common/ExecutiveScope.cs`, `ExecutivePeriod.cs`, `ExecutiveMath.cs`, `ExecutiveSeries.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Interfaces/IExecutiveScopeResolver.cs`, `ISalesMetricsService.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/ExecutiveScopeResolver.cs`, `SalesMetricsService.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Validators/ExecutiveQueryValidators.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify — two registrations)
- `backend/src/OrderMgmt.WebApi/Controllers/ExecutiveDashboardController.cs` (new)

Tests:
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/ExecutiveDashboardTestBase.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/ExecutivePermissionSeedTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/SalesSummaryTests.cs`, `ExecutiveScopeTests.cs`, `SalesCostTests.cs`, `SalesSeriesTests.cs`, `SalesTopTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/Unit/ExecutivePeriodTests.cs`, `ExecutiveMathTests.cs`, `ExecutiveSeriesTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/Unit/CostProvisionalTests.cs` (new — pure helpers of `Inventory/Common` are tested there by convention)

## Reference files (read-only)

- `backend/src/OrderMgmt.WebApi/Controllers/CashDebtReportsController.cs` — controller shape, validators, `Success(...)`
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Validators/CashDebtReportValidators.cs` — `ReportDates.Min` / `ReportDates.Max` (2000-01-01 and 2100-12-31; internal, same assembly), messages
- `backend/src/OrderMgmt.Application/Inventory/Reports/Services/InventoryReportService.cs` — snapshot wrapper, `inventory.view_cost` masking, `IsProvisional` (lines 266–268)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherCalculator.cs` — how `Amount`, `DiscountAmount`, `OrderDiscountAllocated`, `VatAmount` are computed (lines 27–80)
- `backend/src/OrderMgmt.Application/CashDebt/Guards/FundMovementQuery.cs` (lines 51–53) — why `IgnoreQueryFilters()` must not sit inside a document query
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/DashboardService.cs` — a conditional inside `Sum` that EF translates
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtSeedTests.cs` — seed test style (`RoleCodesAsync`)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/ReportDataset.cs`, `DebtAgingReportTests.cs` — dataset and report test style
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` — `CreateClientWithPermissionsAsync`, `CreateInventoryProductAsync`, `CreatePartnerAsync`, `VoucherRequest`, `LineRequest`, `PostVoucherAsync`, `CreateVoucherAsync`, `LedgerOfAsync`, `InDbAsync`
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/StockVouchers/StockVoucherLifecycleTests.cs` — cancelling, deleting and editing a voucher in a test
- `SUMMARY.md` of this plan, sections "Shared contracts" and "Executor conventions" (posting recipe)

## Contracts

The shared types (`ExecutiveQuery`, `ExecutiveSeriesQuery`, `SalesTopQuery`, `RecentQuery`, the enums, `ExecutiveScope`, `ExecutivePeriod`, `ExecutiveMath`, `ExecutiveSeries`, `IExecutiveScopeResolver`, `Permissions.Dashboard.Executive`, `StockReasonCodes`) are written out in `SUMMARY.md`, section "Shared contracts". This phase creates all of them except `DashboardDomains`.

```csharp
// Application/Inventory/Common/CostProvisional.cs
public static class CostProvisional
{
    /// The costing period containing `date` has not ended on `today` (its last day is today or later).
    public static bool IsProvisional(DateOnly date, CostingPeriod period, DateOnly today) =>
        CostingPeriodCalendar.PeriodOf(date, period).End >= today;
}

// Application/Dashboards/Executive/Models/SalesModels.cs
public class SalesSummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly PrevFrom { get; set; }
    public DateOnly PrevTo { get; set; }
    public decimal Revenue { get; set; }
    public decimal PrevRevenue { get; set; }
    public decimal? RevenueDeltaPct { get; set; }
    public bool CanViewCost { get; set; }
    public decimal? Cost { get; set; }            // null without inventory.view_cost
    public decimal? GrossProfit { get; set; }     // Revenue − Cost
    public decimal? MarginPct { get; set; }       // GrossProfit / Revenue × 100
    public decimal? PrevMarginPct { get; set; }
    public bool IsProvisional { get; set; }
}
public class SalesSeriesDto
{
    public SeriesGranularity Granularity { get; set; }
    public List<SalesPointDto> Points { get; set; } = new();
}
public class SalesPointDto
{
    public DateOnly Date { get; set; }            // bucket start
    public decimal Revenue { get; set; }
    public decimal? Cost { get; set; }
    public decimal? GrossProfit { get; set; }
}
public class SalesTopRowDto
{
    public Guid Id { get; set; }                  // partner, product or user id
    public string? Code { get; set; }             // partner or product code; null for employees
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal? GrossProfit { get; set; }
}

// Application/Dashboards/Executive/Interfaces/ISalesMetricsService.cs
public interface ISalesMetricsService
{
    Task<SalesSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<SalesSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default);
    Task<List<SalesTopRowDto>> GetTopAsync(SalesTopQuery query, CancellationToken ct = default);
}
```

`SalesMetricsService` injects `IAppDbContext`, `ICurrentUser`, `IExecutiveScopeResolver`, `IDateTime`, `ITransactionRunner`.

## Rules

### Period (`ExecutivePeriod.Create`)

- `To` = the given `to`; without it, VN today, or `from` when `from` is after today.
- `From` = the given `from`; without it, day 1 of `To`'s month.
- The previous period has the same number of days and ends the day before `From`.
- `Days` = `To − From + 1`.
- `Start`, `End`, `PrevStart`, `PrevEnd` are UTC instants built with `VnTime`; `End` and `PrevEnd` are exclusive.

### Scope (`ExecutiveScopeResolver`)

- User with `branches.access_all`:
  - no `branchId` → every branch (`_db.Branches`, ordered by `Code`), `IsCompanyWide = true`;
  - a `branchId` that exists → that branch, `IsCompanyWide = false`;
  - a `branchId` that does not exist → `NotFoundException("Branch", branchId)` (404).
- Any other user: own branch = `ICurrentBranch.GetIdAsync()` (for these users it is the default branch, whatever the `X-Branch-Id` header says).
  - no `branchId`, or `branchId` equal to the own branch → that branch, `IsCompanyWide = false`;
  - another `branchId` → `ForbiddenException("Bạn không có quyền xem chi nhánh này.")` (403).

### Validation (`ExecutiveQueryValidators.cs`)

The limits apply to the resolved period, so a request with only one date is checked too. Each validator takes `IDateTime` in its constructor (validators are created by dependency injection) and resolves `ExecutivePeriod.Create(x.From, x.To, VnTime.ToVnDate(clock.UtcNow))` inside a custom rule. A failure added by hand names its property in camelCase, as the response prints it.

`ExecutiveQueryValidator : AbstractValidator<ExecutiveQuery>` (Task 1.4):
- `From`, `To`, `At`, when set, are between `ReportDates.Min` and `ReportDates.Max` — "Từ ngày không hợp lệ." / "Đến ngày không hợp lệ." / "Ngày xem số dư không hợp lệ.";
- resolved `To` before resolved `From` → key `to`, "Đến ngày phải sau hoặc bằng từ ngày.";
- otherwise resolved `Days` above 1,100 → key `to`, "Khoảng thời gian tối đa là 3 năm.".

`ExecutiveSeriesQueryValidator : AbstractValidator<ExecutiveSeriesQuery>` (Task 1.6): `Include(new ExecutiveQueryValidator(clock))`; `Granularity == Day` with resolved `Days` above 366 → key `granularity`, "Biểu đồ theo ngày tối đa 366 ngày. Hãy chọn theo tuần hoặc tháng.".

`SalesTopQueryValidator : AbstractValidator<SalesTopQuery>` (Task 1.7): `Include(new ExecutiveQueryValidator(clock))`; `Limit` from 1 to 50 → key `limit`, "Số dòng từ 1 đến 50.".

Enums: there is no "in enum" rule. An unknown enum name and a number outside the enum (`granularity=7`) both fail model binding before any validator runs, and ASP.NET answers 400 by itself. That body is the framework's `ValidationProblemDetails` (`errors` keyed by the PascalCase property name), not the `ApiResponse` envelope, so there is no `error.details`. This was measured during the Phase 01 run; two tests pin the status code.

Validators are registered by the assembly scan in `DependencyInjection.cs`; the controller injects `IValidator<T>` for each query type and calls `ValidateAndThrowAsync`.

### Sales (P3, P4, P5)

- **Counted vouchers:** `StockVouchers` with `Status == StockVoucherStatus.Active`, `BranchId` in the scope, `VoucherAt` in `[start, end)`, and `ReasonId` among the system reasons with code `StockReasonCodes.Sale` or `StockReasonCodes.SaleReturn`. Resolve those ids once per request, in their own query, from `StockReasons.IgnoreQueryFilters()` (`IsSystem` and the code).
- **Soft deletes.** The global query filters hide deleted vouchers and deleted lines (a line removed by an edit is soft-deleted). `IgnoreQueryFilters()` switches the filters off for the whole query it appears in, so it is used only in separate lookup queries by id (names of partners, products, users, reasons), never inside a query that reads vouchers, lines or ledger rows.
- **Sign:** +1 for a sale, −1 for a return, on revenue and on cost.
- **Revenue of a voucher:** the sum over its lines of `Amount − DiscountAmount − OrderDiscountAllocated − (voucher.Type == StockDirection.Out && line.PriceIncludesVat ? line.VatAmount : 0)`.
- **Cost of a voucher:** from `InventoryLedger` rows with `SourceId == voucher.Id`: a sale takes `Sum(CostAmount ?? 0)` of its `LedgerSourceType.StockOut` rows; a return takes `Sum(InValue)` of its `LedgerSourceType.StockIn` rows. The ledger holds rows of active, non-deleted vouchers only, and costs are written when a voucher is posted.
- **Gross profit** = revenue − cost. **Margin** = `ExecutiveMath.MarginPct(revenue, grossProfit)`.
- **Cost visibility:** without `Permissions.Inventory.ViewCost`, `Cost`, `GrossProfit`, `MarginPct`, `PrevMarginPct` (and the same fields of series points and top rows) are `null` and `CanViewCost` is `false`.
- **Provisional:** `CostProvisional.IsProvisional(period.To, settings.CostingPeriod, today)` with `settings = InventorySettings` row 1.
- **Series:** voucher values grouped by `ExecutiveSeries.BucketOf(VnTime.ToVnDate(voucher.VoucherAt), granularity)`; every bucket of `ExecutiveSeries.Buckets(period.From, period.To, granularity)` is returned, zero-filled.
- **Top lists** (current period only):
  - `Customer`: group by `PartnerId` (XBH and NTL always carry a customer); `Code` and `Name` from `Customers.IgnoreQueryFilters()`.
  - `Product`: group lines by `ProductId` (revenue from lines, cost from ledger rows grouped by `ProductId`); `Code` and `Name` from `Products.IgnoreQueryFilters()`.
  - `Employee`: group by the voucher's `OwnerUserId`; `Name` = `Users.IgnoreQueryFilters().FullName`; `Code` is `null`.
  - Order by the sort column descending, then `Name` (ordinal), then `Id`, so the order is total; take `Limit`. `Sort == GrossProfit` without `inventory.view_cost` orders by revenue.

### Query shapes (guidance; change the shape, never the rule, if EF refuses to translate one, and say so in the report)

```csharp
// One query for the current and the previous period: window [period.PrevStart, period.End)
var vouchers = _db.StockVouchers.AsNoTracking()
    .Where(v => v.Status == StockVoucherStatus.Active
        && scope.BranchIds.Contains(v.BranchId)
        && (saleIds.Contains(v.ReasonId) || returnIds.Contains(v.ReasonId))
        && v.VoucherAt >= from && v.VoucherAt < to);

var rows = await vouchers.Select(v => new
{
    v.Id, v.BranchId, v.VoucherAt, v.PartnerId, v.OwnerUserId,
    IsReturn = returnIds.Contains(v.ReasonId),
    Revenue = v.Lines.Sum(l => l.Amount - l.DiscountAmount - l.OrderDiscountAllocated
        - (v.Type == StockDirection.Out && l.PriceIncludesVat ? l.VatAmount : 0m)),
}).ToListAsync(ct);

var costs = await _db.InventoryLedger.AsNoTracking()
    .Where(e => (e.SourceType == LedgerSourceType.StockOut || e.SourceType == LedgerSourceType.StockIn)
        && vouchers.Any(v => v.Id == e.SourceId))
    .GroupBy(e => e.SourceId)
    .Select(g => new { VoucherId = g.Key, CostOut = g.Sum(e => e.CostAmount ?? 0m), ValueIn = g.Sum(e => e.InValue) })
    .ToListAsync(ct);
```

Map both into one private row type (`VoucherId`, `BranchId`, `At`, `PartnerId`, `OwnerUserId`, signed `Revenue`, signed `Cost`) and do every grouping (period split, series, customers, employees) in memory: the row count is the number of vouchers. The product list needs its own two grouped queries (`StockVoucherLines` and `InventoryLedger` by `ProductId`, both restricted with `vouchers.Any(...)`).

## Test helpers

`ExecutiveDashboardTestBase` (abstract, `: CashDebtTestBase`, constructor `(PostgresFixture pg) : base(pg)`):

```csharp
protected const string OctoberRange = "from=2026-10-01&to=2026-10-31";
protected const string October = OctoberRange + "&at=2026-10-31";
protected HttpClient MainClient(ReportIds ids);        // CloneAdminClient() + UseBranch(client, ids.MainBranchId)
protected static async Task<T> GetExecAsync<T>(HttpClient client, string pathAndQuery);   // ReadDataAsync<T>(GET /api/dashboard/executive/{pathAndQuery})
protected static Task<HttpResponseMessage> GetExecRawAsync(HttpClient client, string pathAndQuery);
```

A query string never repeats a key: a case that needs another `at` or other dates is written in full (`{OctoberRange}&at=2026-10-14`, or its own `from`/`to`), not appended to `{October}`.

Restricted users come from `CreateClientWithPermissionsAsync(username, defaultBranchId, permissionCodes)`; their default branch is the branch the request names (without it the helper uses the seeded branch `CN01`, and the scope rule answers 403).

## Tasks

Set the test database first in every shell:
`export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_dash;Username=postgres;Password=1"`. Backend commands run from `backend/`. The posting recipe for extra vouchers is in `SUMMARY.md`, "Executor conventions".

### Task 1.1 — Permission `dashboard.executive`

1. Write the failing test `ExecutivePermissionSeedTests` (integration, modelled on `CashDebtSeedTests`):
   - `Permission_exists_in_report_module`: the `permissions` row with code `Permissions.Dashboard.Executive` has module `Permissions.ReportModule`;
   - `Admin_and_manager_hold_it`: the role codes of `RoleCodes.Admin` and `RoleCodes.Manager` contain it;
   - `Other_roles_do_not`: `RoleCodes.Accountant`, `RoleCodes.Sales`, `RoleCodes.Warehouse` do not contain it.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutivePermissionSeedTests"`. Expected: FAIL — build error, `Permissions.Dashboard` does not exist.
3. Implement: the `Dashboard` class in `Permissions.cs` (next to `Reports`); in `DbSeeder.SeedPermissionsAsync` add `(Permissions.Dashboard.Executive, Permissions.ReportModule, "Bảng điều hành")` after the `Permissions.Reports.Cash` line. Do not touch `roleDefs`: ADMIN and MANAGER receive every permission.
4. Run the same command, then `--filter "FullyQualifiedName~CashDebtSeedTests|FullyQualifiedName~DbSeederUpgradeTests"`. Expected: PASS.
5. Commit: `git add backend/src/OrderMgmt.Domain/Constants/Permissions.cs backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/ExecutivePermissionSeedTests.cs` and `git commit -m "feat(dashboard): permission dashboard.executive"`.

### Task 1.2 — Period, math and series helpers

1. Write the failing unit tests in `ExecutiveDashboard/Unit/`:
   - `ExecutivePeriodTests`:
     - `Create(null, null, 2026-10-13)` → `From 2026-10-01`, `To 2026-10-13`, `PrevFrom 2026-09-18`, `PrevTo 2026-09-30`, `Days 13`;
     - `Create(2026-10-05, 2026-10-08, 2026-10-13)` → previous `2026-10-01 … 2026-10-04`;
     - `Create(null, 2026-09-20, 2026-10-13)` → `From 2026-09-01`;
     - `Create(2026-11-02, null, 2026-10-13)` → `To 2026-11-02`;
     - `Create(2026-10-13, 2026-10-13, 2026-10-13)` → previous `2026-10-12 … 2026-10-12`;
     - for `From 2026-10-01`, `To 2026-10-13`: `Start == 2026-09-30T17:00:00Z`, `End == 2026-10-13T17:00:00Z`, `PrevEnd == Start`.
   - `ExecutiveMathTests`: `DeltaPct(110, 100) == 10`; `DeltaPct(90, 100) == -10`; `DeltaPct(5, 0) == null`; `DeltaPct(-500, 3000) == -116.67`; `DeltaPct(100, -200) == 150`; `MarginPct(600, 200) == 33.33`; `MarginPct(0, 0) == null`; `MarginPct(-100, -20) == 20`.
   - `ExecutiveSeriesTests` (2026-10-12 is a Monday):
     - `BucketOf(2026-10-14, Week) == 2026-10-12`; `BucketOf(2026-10-18, Week) == 2026-10-12`; `BucketOf(2026-10-19, Week) == 2026-10-19`; `BucketOf(2026-10-14, Month) == 2026-10-01`; `BucketOf(2026-10-14, Day) == 2026-10-14`;
     - `Buckets(2026-10-01, 2026-10-03, Day)` → three days;
     - `Buckets(2026-09-30, 2026-10-13, Week)` → `09-28, 10-05, 10-12`;
     - `Buckets(2026-08-15, 2026-10-13, Month)` → `08-01, 09-01, 10-01`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard.Unit"`. Expected: FAIL — build error, `ExecutivePeriod` does not exist.
3. Implement `ExecutiveScope.cs`, `ExecutivePeriod.cs`, `ExecutiveMath.cs`, `ExecutiveSeries.cs` and `Models/ExecutiveModels.cs` (the query classes and enums of `SUMMARY.md`). Rounding is 2 decimals, `MidpointRounding.AwayFromZero`.
4. Run the same command. Expected: PASS.
5. Commit the files of this task: `git commit -m "feat(dashboard): period, delta and series helpers"`.

### Task 1.3 — `sales/summary`: revenue, controller, permissions

1. Write `ExecutiveDashboardTestBase` and the failing test `SalesSummaryTests` (each case seeds `ReportDataset`):
   - `Revenue_of_main_branch`: admin `sales/summary?{October}&branchId={main}` → `Revenue 2500`, `PrevRevenue 0`, `RevenueDeltaPct null`, `From 2026-10-01`, `To 2026-10-31`, `PrevFrom 2026-08-31`, `PrevTo 2026-09-30`.
   - `Revenue_company_wide`: admin without `branchId` → `Revenue 12499` (branch B adds 9,999).
   - `Delta_against_previous_period`: `from=2026-10-05&to=2026-10-08&branchId={main}` → `Revenue -500`, `PrevRevenue 3000`, `RevenueDeltaPct -116.67`.
   - `Cancelled_voucher_is_not_counted`: post one more unpaid XBH of 700 to `Kh1Id` in the main branch on 10-20 (an untracked product, 1 × 700) → `Revenue 3200`; cancel it (`POST /api/stock-vouchers/{id}/cancel` with `StockVoucherActionRequest { Version }`) → `Revenue 2500`.
   - `Deleted_voucher_and_removed_line_are_not_counted`: post an unpaid XBH on 10-20 with two lines of untracked products (1 × 700 and 1 × 300) → `Revenue 3500`; update it keeping only the 700 line → `Revenue 3200`; delete it (`DELETE /api/stock-vouchers/{id}`) → `Revenue 2500`.
   - `Vat_inclusive_price_is_reduced_to_before_vat`: an untracked product created with `track: false, priceIncludesVat: true`; XBH on 10-20 in the main branch with one line, quantity 1, unit price 1,100 and `VatRate = 10` set on the line (the server takes the rate from the line, not from the product) → `Revenue 3500` (2,500 + 1,000).
   - `Vat_exclusive_price_is_taken_as_is`: the same with `priceIncludesVat: false`, unit price 1,000 and `VatRate = 10` on the line → `Revenue 3500`.
   - `Requires_dashboard_executive`: a user with only `reports.profit` → 403.
   - `Requires_reports_profit`: a user with only `dashboard.executive` → 403.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SalesSummaryTests"`. Expected: FAIL — build error, `SalesSummaryDto` does not exist.
3. Implement:
   - `StockReasonCodes.cs`, `Models/SalesModels.cs`, `ISalesMetricsService`, `IExecutiveScopeResolver`;
   - `ExecutiveScopeResolver` with the two `branches.access_all` cases that return a scope (no `branchId` → all branches; a `branchId` → that branch). The other scope rules belong to Task 1.4;
   - `SalesMetricsService.GetSummaryAsync` with the revenue fields only; `CanViewCost = false` and the cost fields `null` until Task 1.5; `IsProvisional = false`;
   - `ExecutiveDashboardController` with the class attributes of `SUMMARY.md` and the action `GET sales/summary` (`[HasPermission(Permissions.Reports.Profit)]`); no validator call yet (Task 1.4);
   - the two `AddScoped` lines in `DependencyInjection.cs`, next to the report services.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): sales summary endpoint"`.

### Task 1.4 — Scope and validation rules

1. Write the failing test `ExecutiveScopeTests` (all through `sales/summary`, dataset seeded; "the branch user" has default branch = main and `dashboard.executive`, `reports.profit`):
   - `User_without_access_all_sees_own_branch`: the branch user, `{OctoberRange}` and no `branchId` → `Revenue 2500`.
   - `User_without_access_all_may_name_own_branch`: `branchId={main}` → `Revenue 2500`.
   - `User_without_access_all_cannot_name_another_branch`: `branchId={branchB}` → 403.
   - `Header_does_not_widen_the_scope`: the branch user with `X-Branch-Id: {branchB}` and no `branchId` → `Revenue 2500`.
   - `Unknown_branch_returns_404`: admin, `branchId={Guid.NewGuid()}` → 404.
   - `To_before_from_is_rejected`: admin, `from=2026-10-10&to=2026-10-01` → 400 with a `to` detail.
   - `Range_over_three_years_is_rejected`: admin, `from=2020-01-01&to=2026-10-01` → 400 with a `to` detail.
   - `Only_from_far_in_the_past_is_rejected`: admin, `from=2000-01-01` → 400 with a `to` detail.
   - `Only_to_is_accepted`: admin, `to=2026-09-20` → 200 with `From 2026-09-01`.
   - `Only_from_is_accepted`: admin, `from=2026-10-01&branchId={main}` → 200 with `From 2026-10-01`.
   - `Defaults_to_the_current_month`: admin, no dates → 200 and `From` is day 1 of `To`'s month.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveScopeTests"`. Expected: FAIL — the 403, 404 and 400 cases return 200.
3. Implement the remaining scope rules in `ExecutiveScopeResolver`, `ExecutiveQueryValidator` in `ExecutiveQueryValidators.cs`, and the `ValidateAndThrowAsync` call in the controller action.
4. Run `--filter "FullyQualifiedName~ExecutiveScopeTests|FullyQualifiedName~SalesSummaryTests"`. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): branch scope and query validation"`.

### Task 1.5 — Cost, gross profit, margin, provisional flag

1. Write the failing tests:
   - `Inventory/Unit/CostProvisionalTests`: `(2026-10-31, Month, today 2026-10-13)` → true; `(2026-09-30, Month, 2026-10-13)` → false; `(2026-09-30, Quarter, 2026-10-13)` → false; `(2026-10-01, Quarter, 2026-10-13)` → true; `(2026-10-13, Month, 2026-10-31)` → true (the period ends today).
   - `SalesCostTests` (integration; a fresh branch with a warehouse, a customer and a supplier, see the posting recipe):
     - `Cost_and_gross_profit_of_a_tracked_sale`: a tracked product (VAT 0), NMH 10 × 100 on 10-02, XBH 4 × 150 on 10-04 → `sales/summary?{October}&branchId=…`: `Revenue 600`, `Cost 400`, `GrossProfit 200`, `MarginPct 33.33`, `CanViewCost true`.
     - `Return_reduces_cost_by_its_inbound_value`: add NTL 1 × 150 on 10-06 → `Revenue 450` and `Cost == Σ CostAmount of the XBH ledger rows − Σ InValue of the NTL ledger rows` (read both with `LedgerOfAsync`).
     - `Untracked_line_has_no_cost`: the dataset main branch (untracked products) → `Cost 0`, `GrossProfit 2500`, `MarginPct 100`.
     - `Cost_is_hidden_without_view_cost`: the first scenario for a user with `dashboard.executive` and `reports.profit` only (default branch = that branch) → `Revenue 600`, `CanViewCost false`, `Cost`, `GrossProfit`, `MarginPct`, `PrevMarginPct` all null.
     - `Provisional_follows_the_costing_period`: `from=2020-01-01&to=2020-01-31` → `IsProvisional false`; `from=2099-12-01&to=2099-12-31` → `IsProvisional true`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CostProvisionalTests|FullyQualifiedName~SalesCostTests"`. Expected: FAIL — build error, `CostProvisional` does not exist.
3. Implement:
   - `CostProvisional.cs`; in `InventoryReportService` the private `IsProvisional` becomes `CostProvisional.IsProvisional(date, period, VnTime.ToVnDate(_clock.UtcNow))` (signature unchanged);
   - the cost side of `SalesMetricsService` (ledger query, sign, masking, margin of both periods, provisional flag).
4. Run the same command, then `--filter "FullyQualifiedName~StockOnHandReportTests|FullyQualifiedName~StockCardReportTests|FullyQualifiedName~InventoryReportReviewFixTests|FullyQualifiedName~SalesSummaryTests"`. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): sales cost, gross profit and provisional flag"`.

### Task 1.6 — `sales/series`

1. Write the failing test `SalesSeriesTests` (dataset seeded, admin, main branch):
   - `Daily_points_are_zero_filled`: `sales/series?{October}&granularity=Day&branchId={main}` → 31 points, `Granularity Day`, 10-03 → `Revenue 3000`, 10-08 → `Revenue -500`, every other point 0, `Cost 0` on each point.
   - `Weekly_points_start_on_monday`: `granularity=Week` → dates `09-28, 10-05, 10-12, 10-19, 10-26`; 09-28 → 3,000; 10-05 → −500.
   - `Monthly_point`: `granularity=Month` → one point `2026-10-01` with 2,500.
   - `Buckets_follow_vn_dates`: two more unpaid XBH of 100 at `2026-10-20 23:30` and `2026-10-21 00:30` (VN time) → the 10-20 point and the 10-21 point each hold 100.
   - `Daily_series_over_a_year_is_rejected`: `from=2025-01-01&to=2026-10-01&granularity=Day` → 400 with a `granularity` detail; the same range with `granularity=Month` → 200.
   - `Undefined_granularity_number_is_rejected`: `{October}&granularity=7` → status 400 (the framework's own response; assert the status only). It passes before any implementation, so it has no RED step.
   - `Requires_reports_profit`: 403 for a user with only `dashboard.executive`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SalesSeriesTests"`. Expected: FAIL — 404 (no route).
3. Implement `GetSeriesAsync`, `ExecutiveSeriesQueryValidator` and the action `GET sales/series` (validator call, `[HasPermission(Permissions.Reports.Profit)]`).
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): sales series endpoint"`.

### Task 1.7 — `sales/top`

1. Write the failing test `SalesTopTests`:
   - `Top_customers_of_main_branch`: dataset, `sales/top?{October}&by=Customer&branchId={main}` → one row: `Id = Kh1Id`, `Code = Kh1Code`, `Revenue 2500`.
   - `Top_customers_company_wide_merge_branches`: no `branchId` → one row with `Revenue 12499`.
   - `Top_products_ordered_by_revenue`: `by=Product&branchId={main}` → two rows, the first with `Revenue 3000`, the second with `Revenue -500`.
   - `Top_employees`: `by=Employee&branchId={main}` → one row, `Id` = the admin user id, `Name` = the admin's full name, `Code null`, `Revenue 2500`.
   - `Sort_by_gross_profit`: a fresh branch with two tracked products (VAT 0), each bought with NMH 10 × 100; A sold with XBH 2 × 150 (revenue 300, cost 200, profit 100), B sold with XBH 4 × 110 (revenue 440, cost 400, profit 40). `by=Product&sort=Revenue` → B first; `by=Product&sort=GrossProfit` → A first, with `GrossProfit 100` and `40`.
   - `Gross_profit_sort_without_view_cost_falls_back_to_revenue`: the same data for a user without `inventory.view_cost` (default branch = that branch) and `sort=GrossProfit` → B first and `GrossProfit null` on each row.
   - `Limit_is_applied_and_validated`: `by=Product&limit=1` → one row; `limit=0` and `limit=51` → 400 with a `limit` detail.
   - `Undefined_by_or_sort_number_is_rejected`: `{October}&by=7` and `{October}&by=Customer&sort=7` → status 400 (the framework's own response; assert the status only; no RED step).
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SalesTopTests"`. Expected: FAIL — 404 (no route).
3. Implement `GetTopAsync`, `SalesTopQueryValidator` and the action `GET sales/top`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): sales top lists endpoint"`.

### Task 1.8 — Review follow-up (added after the quality review of Tasks 1.1–1.7)

The review found no wrong figure, but several one-line changes to the revenue and cost code passed every test, and `sales/top` had no guard test. These tests cover existing behaviour, so each is proven by a mutation instead of a RED run: break the line it guards, see the test fail, restore the line.

1. Figure tests:
   - `SalesSummaryTests.Line_and_order_discounts_reduce_revenue`: one more XBH on 10-20 in the main branch, 10 × 100 with a 10 % line discount and an order discount of 90 → `Revenue 3310`; its `by=Product` row has `Revenue 810`.
   - `SalesTopTests.Vat_inclusive_price_is_reduced_on_the_product_list`: 1 × 1,100 VAT-inclusive at 10 % → product `Revenue 1000`.
   - `SalesCostTests.Previous_period_has_its_own_cost_and_margin`: NMH 20 × 100 on 10-01, XBH 4 × 150 on 10-04, XBH 2 × 180 on 10-07; `from=2026-10-06&to=2026-10-08` → `Revenue 360`, `PrevRevenue 600`, `RevenueDeltaPct -40`, `Cost 200`, `GrossProfit 160`, `MarginPct 44.44`, `PrevMarginPct 33.33` (the two periods must have different gross profits, or a previous margin computed from the current profit would pass).
   - `SalesTopTests.Tracked_return_reduces_product_cost`: NMH 10 × 100, XBH 4 × 150, NTL 1 × 150 → product `Revenue 450`, `GrossProfit` = 450 − (Σ `CostAmount` of the XBH rows − Σ `InValue` of the NTL rows).
   - `SalesSeriesTests.Tracked_sale_has_cost_and_gross_profit_points`: the 10-04 point has `Revenue 600`, `Cost 400`, `GrossProfit 200`; every other point 0.
   - `SalesTopTests.Customers_sorted_by_gross_profit`: C1 buys 2 × 150 and C2 buys 4 × 110 of a product costing 100 → by revenue C2 first, by gross profit C1 first (100, then 40).
   - `SalesTopTests.Employee_gross_profit_adds_up_the_sales_of_both_customers`: the same scenario with `by=Employee` → one row with `Revenue 740`, `GrossProfit 140`.
   - `SalesTopTests.Limit_keeps_the_first_row_of_the_order`: equal revenue → the name that sorts first (given to the product with the larger id, so the name decides); equal names too → the smaller `Id`, the same on every call.
   - `SalesTopTests.Customers_with_the_same_name_and_revenue_are_ordered_by_id`: two customers with the same name and revenue, the larger id sold to first → the smaller id comes first.
   - Product-list VAT conditions: a VAT-exclusive product 1 × 1,000 at 10 % → `Revenue 1000`; a VAT-inclusive product sold 2 × 1,100 and returned 1 × 1,100 → `Revenue 900` (a stock-in line is priced before VAT, so nothing is taken off the return).
   - `SalesCostTests.Provisional_follows_the_costing_period` also covers a range from the first day of last month to today → `IsProvisional true` (the flag follows the end of the range).
2. Guard test `ExecutiveRouteGuardTests`, one table row per route (`sales/summary`, `sales/series`, `sales/top`), all with `{OctoberRange}`. A row carries the route, its extra query string, its domain permission, how to read the checked total, and the expected totals `OwnBranch` (2,500) and `Company` (12,499). Four theories run over the table: `Requires_the_domain_permission` (403 with only `dashboard.executive`), `Foreign_branch_is_refused` (403 for `branchId={branchB}`), `Own_branch_by_default` (the branch user gets `OwnBranch`), `Company_wide_for_access_all` (the admin without `branchId` gets `Company`). All seeding sits in one private arrange method. **Phases 02 and 03 add one row for every route they create** and put their extra documents in that arrange method.
3. Test fixes: `Only_from_is_accepted` uses the first day of the current VN month (the fixed date would exceed the 1,100-day cap from October 2029); `Defaults_to_the_current_month` also asserts `To` is VN today; `GetSummaryAsync` moves to the test base; tests that read no dataset figure do not seed the dataset.
4. Code fixes: the total order of the top lists (rule above); explicit enum cases with `ArgumentOutOfRangeException` instead of a default branch in `ExecutiveSeries` and in the `SalesTopBy` switch; a clearer name for the method that builds the counted-vouchers query; comments name the rule instead of a plan decision id (ids such as P3 already mean Round 2A decisions in this codebase); `DbSeeder` uses `StockReasonCodes` for the XBH and NTL rows.
5. Three commits: `test(dashboard): cover discounts, VAT, previous-period cost and route guards`, `refactor(dashboard): total order for top lists, explicit enum cases, reason code constants`, and `test(dashboard): close the remaining review gaps on sales figures and guards`.

## Verification

From `backend/`, with `TEST_DB_CONNECTION` set:

- `dotnet build OrderMgmt.sln` — 0 errors, no new warnings in the new files.
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard|FullyQualifiedName~CostProvisionalTests"` — all pass.
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebtSeedTests|FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~StockOnHandReportTests|FullyQualifiedName~StockCardReportTests|FullyQualifiedName~InventoryReportReviewFixTests"` — pass.

## Exit Criteria

- The three sales routes return the figures of the report dataset given in `SUMMARY.md`.
- A user without `branches.access_all` can never read another branch.
- Cancelled and deleted vouchers and removed lines are never counted.
- `InventoryReportService` returns the same results as before (its tests pass).
- One commit per task for Tasks 1.1–1.7, plus the follow-up commits of Task 1.8, on `feat/executive-dashboard`.
