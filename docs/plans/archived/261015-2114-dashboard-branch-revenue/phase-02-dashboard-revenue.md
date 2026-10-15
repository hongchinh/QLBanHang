# Phase 02 — Overview dashboard revenue from stock-outs

**Status:** [x] done
**Complexity:** L

## Objective

First, the quotation revenue logic moves, unchanged, into `QuotationRevenueReportService` with its own endpoints, so the Doanh thu report keeps its figures (D10). Then `DashboardService` reads every revenue figure from `SalesRevenueSource` for the working branch, with the owner rule A2 of the plan SUMMARY. The quotation counts, the funnel and recent activity are unchanged. The DTOs are renamed to match.

## Files

Task 2.0:

- Create: `backend/src/OrderMgmt.Application/Reports/QuotationRevenue/Interfaces/IQuotationRevenueReportService.cs`
- Create: `backend/src/OrderMgmt.Application/Reports/QuotationRevenue/Models/QuotationRevenueReportDtos.cs`
- Create: `backend/src/OrderMgmt.Application/Reports/QuotationRevenue/Services/QuotationRevenueReportService.cs`
- Modify: `backend/src/OrderMgmt.Application/DependencyInjection.cs` (register the service as scoped)
- Modify: `backend/src/OrderMgmt.WebApi/Controllers/ReportsController.cs` (three endpoints)
- Create: `backend/tests/OrderMgmt.IntegrationTests/Reports/QuotationRevenueReportTests.cs`

Tasks 2.1–2.5:

- Modify: `backend/src/OrderMgmt.Application/Sales/Quotations/Services/DashboardService.cs`
- Modify: `backend/src/OrderMgmt.Application/Sales/Quotations/Models/DashboardModels.cs`
- Create: `backend/tests/OrderMgmt.IntegrationTests/Dashboard/DashboardRevenueTests.cs`
- Modify: `backend/tests/OrderMgmt.IntegrationTests/Dashboard/DashboardEndpointsTests.cs`
- Modify: `backend/tests/OrderMgmt.IntegrationTests/Dashboard/DashboardScopingTests.cs`

## DTO changes (`DashboardModels.cs`)

- `RevenuePointDto.ConfirmedCount` → `VoucherCount` (number of XBH vouchers in the bucket).
- `TopCustomerDto.QuotationCount` → `VoucherCount` (number of XBH vouchers). `CustomerName` comes from `ExecutiveLookups.PartnersAsync`.
- `SalesLeaderboardItemDto.ConfirmedCount` → `VoucherCount`. Remove `ConversionRate`.
- `TopProductDto`: unchanged shape. `ProductId` is now always set.

## Service shape

New private helpers in `DashboardService`:

```csharp
// Branch = ICurrentBranch.GetIdAsync; owner per plan assumption A2.
private async Task<(Guid BranchId, Guid? OwnerUserId)> RevenueScopeAsync(Guid? requestedSaleUserId, CancellationToken ct);

// [VnTime.StartOfDay(from), VnTime.StartOfNextDay(to)) in UTC.
private async Task<CountedSales> CountedAsync(DateOnly from, DateOnly to, Guid? saleUserId, CancellationToken ct);
```

New constructor dependencies: `SalesRevenueSource`, `ICurrentBranch` and `ExecutiveLookups`. Today is `VnTime.ToVnDate(_clock.UtcNow)`, replacing `DateOnly.FromDateTime(_clock.Now.DateTime)`, for the revenue parts. The quotation counts keep their current date logic.

Remove `GroupByRevenueDateAsync` and `GetPeriodRevenuesAsync`. `RevenueFilterHelper` stays because the reports use it. Also drop the `GetDateModeAsync` calls from the methods that no longer use quotations.

## Tasks

### Task 2.0 — Quotation revenue report service

Target API:

```csharp
namespace OrderMgmt.Application.Reports.QuotationRevenue;

/// Revenue of confirmed quotations for the Doanh thu report: the rule, the "Ngày ghi nhận doanh thu" setting and the
/// owner scope of the old overview dashboard, moved here unchanged.
public interface IQuotationRevenueReportService
{
    Task<QuotationRevenueSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, Guid? saleUserId, CancellationToken ct);
    Task<QuotationRevenueSeriesDto> GetSeriesAsync(DateOnly from, DateOnly to, string granularity, Guid? saleUserId, CancellationToken ct);
    Task<IReadOnlyList<QuotationTopCustomerDto>> GetTopCustomersAsync(DateOnly from, DateOnly to, int limit, Guid? saleUserId, CancellationToken ct);
}

// QuotationRevenueReportDtos.cs. KpiDto is reused from DashboardModels.
public class QuotationRevenueSummaryDto { DateOnly From, To, PrevFrom, PrevTo; KpiDto RangeRevenue, TotalCount, CancelledCount; }
public class QuotationRevenuePointDto { DateOnly Date; decimal Total; int ConfirmedCount; }
public class QuotationRevenueSeriesDto { IReadOnlyList<QuotationRevenuePointDto> Points; }
public class QuotationTopCustomerDto { Guid? CustomerId; string CustomerName; decimal Revenue; int QuotationCount; }
```

Field types follow the current `DashboardModels.cs` DTOs; copy them, do not guess. The summary leaves out `TodayRevenue` and `Funnel`, which the report does not read.

Endpoints on `ReportsController` (`api/reports`), each `[HasPermission(Permissions.Reports.Revenue)]` (plan assumption A6), with the same query parameters and defaults as the `DashboardController` actions they replace:

- `GET revenue/summary` (`from`, `to`, `saleUserId`)
- `GET revenue/series` (`from`, `to`, `granularity = "day"`, `saleUserId`)
- `GET revenue/top-customers` (`from`, `to`, `limit = 5`, `saleUserId`)

1. Write the failing tests `Reports/QuotationRevenueReportTests.cs`, deriving from `Dashboard.DashboardTestBase` (for `SeedQuotationAsync`) with `[Collection(nameof(PostgresCollection))]`:
   - Move here, unchanged except for the URL, `DashboardEndpointsTests.RevenueSeries_Day_FillsZeroGaps`, `TopCustomers_OrdersByRevenueDesc_LimitsCorrectly` and `RevenueRule_ExcludesCancelled`. They assert `ConfirmedCount` and `QuotationCount`. Delete them from `DashboardEndpointsTests` in the same commit.
   - `Summary_counts_confirmed_quotations`: seed a confirmed quotation of 100 (`confirmedAt: 2031-03-15 03:00 UTC`, so it counts in every date mode, as the moved `RevenueRule_ExcludesCancelled` does) and a draft of 50, both dated 2031-03-15 (a day no other test uses, because the database is shared), and query `from=to=2031-03-15` as admin → `RangeRevenue.Value == 100`, `TotalCount.Value == 2`.
   - `Sales_role_without_reports_revenue_gets_403`: a `RoleCodes.Sales` user calling `revenue/summary` gets 403.
2. Run and confirm they fail: `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~QuotationRevenueReportTests"`. Expected: FAIL, 404 on the new routes.
3. Implement:
   - Move `ApplyScope`, `GroupByRevenueDateAsync`, `GetPeriodRevenuesAsync` and a copy of `DeltaOf` from `DashboardService` into `QuotationRevenueReportService`.
   - Copy the bodies of `GetSummaryAsync` (revenue, `TotalCount` with its spark, `CancelledCount`), `GetRevenueSeriesAsync` and `GetTopCustomersAsync`, mapped to the new DTOs. Do not change any query.
   - Register the service and add the three endpoints.
   - `DashboardService` is not changed in this task; Tasks 2.1–2.4 remove its copies.
4. Run the same filter, then `--filter "FullyQualifiedName~OrderMgmt.IntegrationTests.Dashboard"`. Expected: PASS.
5. Commit: `git commit -m "feat(reports): quotation revenue report endpoints"`

Test fixture for Tasks 2.1–2.4: `DashboardRevenueTests : ExecutiveDashboardTestBase` with `[Collection(nameof(PostgresCollection))]`. Helper `SeedAsync()`:

- `CreateFreshBranchAsync()` → `branch`.
- Tracked product P: NMH 20 × 100 on 2026-10-02.
- XBH 4 × 150 on 10-04 (revenue 600, owner admin).
- XBH 2 × 150 on 10-06, owned by sales user S, created with `CreateClientForRoleAsync("dr_s_"+suffix, RoleCodes.Sales, branch.BranchId)` and set through `configure: r => r.OwnerUserId = s` (revenue 300).
- NTL 1 × 150 on 10-07 (revenue −150, owner admin).
- A second fresh branch with XBH 1 × 1,000 on 10-04.

Expected in branch: total 750; S alone 300. Requests use `branch.Client`, an admin whose working branch is `branch`, unless a case says otherwise.

### Task 2.1 — Summary revenue from stock-outs

1. Write failing tests in `DashboardRevenueTests.cs`:
   - `Range_revenue_is_the_working_branch_net_of_returns`: `GET /api/dashboard/summary?from=2026-10-01&to=2026-10-31` → `RangeRevenue.Value == 750`.
   - `Range_revenue_equals_executive_sales_summary`: also `GET /api/dashboard/executive/sales/summary?from=2026-10-01&to=2026-10-31&branchId={branch}` → `Revenue` equals `RangeRevenue.Value`.
   - `Sales_user_sees_own_vouchers_only`: the S client (no `view_all`) gets 300, and passing `saleUserId={admin}` still gives 300.
   - `Admin_sale_filter_uses_voucher_owner`: `saleUserId={S}` → 300.
   - `Previous_period_delta`: `from=2026-10-06&to=2026-10-07` gives 150 (300 − 150). The previous period 10-04..10-05 gives 600. `DeltaPct == -75.00`.
   - In `DashboardEndpointsTests` (it has `SeedQuotationAsync`): `TotalCount_spark_counts_quotations_by_date`. Create a fresh `RoleCodes.Sales` user (as `Summary_AsSales_ReturnsOnlyOwnQuotations` does, so other tests' quotations in the shared database are out of scope), seed two of their draft quotations dated today and one dated today − 2, and call the summary as that user → `TotalCount.Spark` is `[0,0,0,0,1,0,2]`. Today this spark counts only quotations that pass the revenue filter, so drafts give zeros and the test fails.
2. Run and confirm they fail: `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DashboardRevenueTests|FullyQualifiedName~TotalCount_spark"`. Expected: FAIL, because revenue is 0 when no quotations are seeded, and the spark is all zeros.
3. Implement `RevenueScopeAsync` and `CountedAsync`, and rewrite the revenue part of `GetSummaryAsync`:
   - Use `ExecutivePeriod.Create(from, to, today)` for the current and previous periods, keeping the existing `rangeTo < rangeFrom` clamp first.
   - Read one `CountedAsync(prevFrom, to)` and split by `VoucherAt >= period.Start` in memory, as `SalesMetricsService.BuildSummaryAsync` does.
   - `TodayRevenue`: the sum where the VN date == today.
   - `RangeRevenue.Spark`: 7 VN days ending today, from a separate `CountedAsync(today-6, today)`.
   - `TotalCount.Spark`: the quotation count per `QuotationDate` over the 7 days ending on the `today` that the quotation counts use, through `ApplyScope(saleUserId)`, the same rule as `TotalCount` itself.
   - `DeltaPct` keeps the existing `DeltaOf`.
4. Run the same filter. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): summary revenue from stock-outs of the working branch"`

### Task 2.2 — Revenue series

1. Write failing tests:
   - `Series_by_day_fills_gaps`: `GET /api/dashboard/revenue-series?from=2026-10-01&to=2026-10-07&granularity=day` → 7 points, sum 750, the 10-04 point is `Total 600, VoucherCount 1`, the 10-05 point is 0.
   - `Series_by_month`: `from=2026-10-01&to=2026-10-31&granularity=month` → 1 point, total 750.
2. Run `--filter "FullyQualifiedName~DashboardRevenueTests.Series"`. Expected: FAIL (compile error on `VoucherCount`, or a total of 0).
3. Rename the DTO field. Rewrite `GetRevenueSeriesAsync`: group `VoucherRevenues()` by `VnTime.ToVnDate(VoucherAt)` in memory, then fold into week and month buckets with the existing loops. `VoucherCount` counts rows where `!IsReturn`.
4. Run. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): revenue series from stock-outs"`

### Task 2.3 — Top customers and top products

1. Write failing tests:
   - `Top_customers_by_voucher_revenue`: the branch customer has revenue 750 and `VoucherCount == 2`, the XBH count. Its name equals the partner's name. The other branch's customer is absent.
   - `Top_products_signed_quantity`: P has revenue 750 and quantity 5 (4 + 2 − 1).
2. Run `--filter "FullyQualifiedName~DashboardRevenueTests.Top"`. Expected: FAIL.
3. Rename `QuotationCount` → `VoucherCount`.
   - `GetTopCustomersAsync`: group `VoucherRevenues()` by `PartnerId` (skip nulls), order by revenue desc then name, `Take(limit)`, names from `ExecutiveLookups.PartnersAsync`.
   - `GetTopProductsAsync`: group `LineRevenues(_db.StockVoucherLines)` by `ProductId`, names from `ExecutiveLookups.ProductsAsync`.
4. Run. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): top customers and products from stock-outs"`

### Task 2.4 — Sales leaderboard

1. Write failing tests:
   - `Leaderboard_groups_by_voucher_owner`: admin row revenue 450 (600 − 150) and `VoucherCount 1`; S row 300 and `VoucherCount 1`; admin is ranked first.
   - `Leaderboard_forbidden_without_view_all`: the S client gets 403 (keep the existing check).
2. Run `--filter "FullyQualifiedName~DashboardRevenueTests.Leaderboard"`. Expected: FAIL.
3. Rename `ConfirmedCount` → `VoucherCount` and remove `ConversionRate`. Rewrite `GetSalesLeaderboardAsync`: current and previous period from `CountedAsync(prevFrom, to, null)` in the working branch, grouped by `OwnerUserId`, names from `ExecutiveLookups.UserNamesAsync`, and `DeltaPct` through `DeltaOf`. Remove the total-quotation-count query.
4. Run. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): sales leaderboard from stock-outs"`

### Task 2.5 — Rewrite the old quotation-revenue tests

1. Close the remaining gaps. The three quotation revenue tests of `DashboardEndpointsTests` already moved to the report in Task 2.0.
   - Add `limit=1` to `DashboardRevenueTests.Top_customers_by_voucher_revenue`.
   - Add `DashboardRevenueTests.Cancelled_voucher_is_left_out`: cancel the 10-04 XBH through `POST /api/stock-vouchers/{id}/cancel`, expect range revenue 150.
   - `DashboardScopingTests.Leaderboard_AsAdmin_OrdersByRevenueDesc`: delete it, now covered by Task 2.4. `/reports/sales-performance` also called the leaderboard: D10 was extended to a fourth endpoint, `GET /api/reports/revenue/sales-leaderboard` (quotation rule, `quotations.view_all`), and that report uses it.
   - Keep the funnel, recent-activity and `TotalCount` tests unchanged. They still test quotations.
2. Run `--filter "FullyQualifiedName~OrderMgmt.IntegrationTests.Dashboard"`. Expected: PASS, including `Cancelled_voucher_is_left_out`. Before the deletions this run fails to compile on the renamed fields, which is the RED evidence.
3. Commit: `git commit -m "test(dashboard): replace quotation revenue tests with stock-out tests"`

## Verification

- `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Dashboard|FullyQualifiedName~ExecutiveDashboard|FullyQualifiedName~Reports"` passes.
- `grep -n "GetDateModeAsync\|RevenueFilterHelper" backend/src/OrderMgmt.Application/Sales/Quotations/Services/DashboardService.cs` prints nothing.

## Exit Criteria

- Every revenue figure of `/api/dashboard/*` follows D2–D6, and `RangeRevenue` equals the executive `Revenue` for the same branch and period.
- No remaining test asserts quotation revenue on these endpoints; the quotation revenue tests run against `/api/reports/revenue/*` and pass unchanged.
