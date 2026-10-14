# Phase 02 — Cash and debt metrics

**Status:** [x] complete (2026-10-14, commits 4f4b9fe..32d4cdc; review fixes 7136c29, 98d3c3d; spec review compliant)
**Complexity:** L

## Objective

- Cash domain: `cash/summary`, `cash/series`, `cash/breakdown` (receipts, payments, net, fund balances), read through `FundMovementQuery`.
- Debt domain: `debt/summary`, `debt/breakdown` (balances, overdue structure, due soon, top debtors, credit-limit breaches, by employee, payables due), read through `DebtPositionQuery`.
- Every figure equals the matching report for the same branch and date.

## Files

Backend:
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Models/CashModels.cs`, `DebtModels.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Common/OpenDebtItem.cs`, `DebtPositionMath.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Interfaces/ICashMetricsService.cs`, `IDebtMetricsService.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/CashMetricsService.cs`, `DebtMetricsService.cs` (new)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/ExecutiveLookups.cs` (new — the name lookups shared by every metrics service)
- `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/SalesMetricsService.cs` (modify — use `ExecutiveLookups`)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify — three registrations)
- `backend/src/OrderMgmt.WebApi/Controllers/ExecutiveDashboardController.cs` (modify — five actions)

Tests:
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/CashSummaryTests.cs`, `CashSeriesTests.cs`, `CashBreakdownTests.cs`, `DebtSummaryTests.cs`, `DebtBreakdownTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/ExecutiveRouteGuardTests.cs` (modify — one row per new route)
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/Unit/DebtPositionMathTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/CashDebt/Guards/FundMovementQuery.cs` — `FundMovementQuery.LoadAsync`, `FundMovement`, `FundMovementKind` (internal, same assembly)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/DebtPositionQuery.cs` — `DebtPositionQuery.LoadAsync`, `DebtPositionItem` (internal, same assembly)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/DebtPositionReportService.cs` — overdue (lines 118–194), aging (199–256), by salesperson (261–326)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Services/CashDebtReportService.cs` — cash book (lines 260–363), debt balance (36–126)
- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtAging.cs`, `DebtDates.cs` — `DefaultBoundaries`, `Ranges`, `BucketIndex`, `DaysOverdue`
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Models/DebtPositionReportDtos.cs` — `AgingBucketDto` (reused)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/CashBookReportTests.cs`, `DebtOverdueReportTests.cs`, `DebtBySalespersonReportTests.cs` — how the report routes are called
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/CashDebtTestBase.cs` — `CashRequest`, `CreateCashAsync`, `CashReasonIdAsync`
- Phase 01 of this plan — `ExecutiveDashboardTestBase` (`October`, `MainClient`, `GetExecAsync`, `GetExecRawAsync`), the controller, the common first lines of a `Build…` method

## Contracts

```csharp
// Models/CashModels.cs
public class CashSummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly PrevFrom { get; set; }
    public DateOnly PrevTo { get; set; }
    public DateOnly At { get; set; }
    public decimal Receipts { get; set; }        // period
    public decimal Payments { get; set; }        // period, positive
    public decimal Net { get; set; }             // Receipts − Payments
    public decimal PrevNet { get; set; }
    public decimal? NetDeltaPct { get; set; }
    public decimal Balance { get; set; }         // as at At, every fund of the scope
    public decimal CashBalance { get; set; }     // FundKind.Cash
    public decimal BankBalance { get; set; }     // FundKind.Bank
    public List<FundBalanceDto> Funds { get; set; } = new();
}
public class FundBalanceDto
{
    public Guid FundId { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FundKind Kind { get; set; }
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
}
public class CashSeriesDto
{
    public SeriesGranularity Granularity { get; set; }
    public List<CashPointDto> Points { get; set; } = new();
}
public class CashPointDto
{
    public DateOnly Date { get; set; }
    public decimal Receipts { get; set; }
    public decimal Payments { get; set; }
}
public class CashBreakdownDto
{
    public List<CashReasonTotalDto> ByReason { get; set; } = new();
    public List<CashDocumentDto> Largest { get; set; } = new();
}
public class CashReasonTotalDto
{
    public Guid ReasonId { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonName { get; set; }
    public CashVoucherType Type { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
}
public class CashDocumentDto
{
    public Guid Id { get; set; }
    public CashVoucherType Type { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string? PartnerOrPerson { get; set; }
    public string? Description { get; set; }
    public decimal Total { get; set; }           // positive
}

// Common/OpenDebtItem.cs — a public copy of the open part of DebtPositionItem, so the math is testable without a database
public sealed record OpenDebtItem(Guid BranchId, Guid PartnerId, DebtDirection Direction, DebtSourceType SourceType,
    Guid SourceId, string DocCode, DateOnly DocDate, DateOnly? DueDate, Guid? OwnerUserId, decimal Open)
{
    public DateOnly Due => DueDate ?? DocDate;
}

// Common/DebtPositionMath.cs
public sealed record DebtSideTotals(decimal TotalOpen, decimal Unallocated, decimal Overdue, int OverduePartnerCount,
    IReadOnlyList<decimal> OverdueAmounts, decimal DueSoon, int DueSoonCount)
{
    public decimal Balance => TotalOpen - Unallocated;
    public decimal NotDue => TotalOpen - Overdue;
}
public static class DebtPositionMath
{
    public const int DueSoonDays = 7;
    /// Totals of one side. `items` are the open items (Open > 0) of that side.
    public static DebtSideTotals Totals(IReadOnlyCollection<OpenDebtItem> items, DateOnly at, IReadOnlyList<int> boundaries);
    public static bool IsOverdue(OpenDebtItem item, DateOnly at);                  // Increase and Due < at
    public static bool IsDueWithin(OpenDebtItem item, DateOnly at, int days);      // Increase and at <= Due <= at + days
    /// Σ Open of Increase items − Σ Open of Decrease items.
    public static decimal Balance(IEnumerable<OpenDebtItem> items);
}

// Models/DebtModels.cs
public class DebtSummaryDto
{
    public DateOnly At { get; set; }
    public List<AgingBucketDto> OverdueBuckets { get; set; } = new();   // DebtAging.Ranges(DebtAging.DefaultBoundaries, firstFrom: 1)
    public DebtSideSummaryDto Receivable { get; set; } = new();
    public DebtSideSummaryDto Payable { get; set; } = new();
}
public class DebtSideSummaryDto
{
    public decimal Balance { get; set; }          // TotalOpen − Unallocated
    public decimal TotalOpen { get; set; }
    public decimal Unallocated { get; set; }
    public decimal NotDue { get; set; }           // TotalOpen − Overdue
    public decimal Overdue { get; set; }
    public int OverduePartnerCount { get; set; }
    public List<decimal> OverdueAmounts { get; set; } = new();   // one per bucket
    public decimal DueSoon { get; set; }          // due within the next 7 days
    public int DueSoonCount { get; set; }         // documents
}
public class DebtBreakdownDto
{
    public DateOnly At { get; set; }
    public List<DebtPartnerRowDto> TopDebtors { get; set; } = new();
    public List<CreditLimitBreachDto> OverLimit { get; set; } = new();
    public List<DebtEmployeeRowDto> ByEmployee { get; set; } = new();
    public List<PayableDueRowDto> PayablesDue { get; set; } = new();
}
public class DebtPartnerRowDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public decimal Balance { get; set; }
    public decimal Overdue { get; set; }
}
public class CreditLimitBreachDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal Limit { get; set; }
    public decimal Over { get; set; }             // Balance − Limit
}
public class DebtEmployeeRowDto
{
    public Guid? OwnerUserId { get; set; }        // null: documents without an owner
    public string? OwnerName { get; set; }
    public decimal Open { get; set; }
    public decimal Overdue { get; set; }
}
public class PayableDueRowDto
{
    public Guid PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public DebtSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string DocCode { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public decimal Open { get; set; }
}

// Interfaces
public interface ICashMetricsService
{
    Task<CashSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<CashSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default);
    Task<CashBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);
}
public interface IDebtMetricsService
{
    Task<DebtSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default);
    Task<DebtBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default);
}
```

Both services inject `IAppDbContext`, `IExecutiveScopeResolver`, `ExecutiveLookups`, `IDateTime`, `ITransactionRunner`. Routes, permissions and query classes are in the route table of `SUMMARY.md`.

```csharp
// Services/ExecutiveLookups.cs — scoped, injects IAppDbContext. Each method is ONE separate query by id with
// IgnoreQueryFilters() (soft-deleted rows keep their names); an id that is not found is simply absent from the dictionary.
public sealed class ExecutiveLookups
{
    public Task<Dictionary<Guid, string>> BranchCodesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    public Task<Dictionary<Guid, (string Code, string Name)>> PartnersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    public Task<Dictionary<Guid, (string Code, string Name)>> ProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    public Task<Dictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
```

## Rules

### Shared lookups and guard rows

- Task 2.1 creates `ExecutiveLookups` by moving the private name lookups of `SalesMetricsService` into it (same queries, same results; the sales tests of Phase 01 must stay green). Every later service resolves branch codes, partner, product and user names through it: wherever the rules below say a name comes from `Branches`, `Customers` or `Users` with `IgnoreQueryFilters()`, that means the matching `ExecutiveLookups` method. Cash reason codes and names are specific to cash and are looked up inside `CashMetricsService` (one separate query by id with `IgnoreQueryFilters()`). A caller reads a name with `TryGetValue`, never with the indexer.
- Every task that adds a route adds its row to the table of `ExecutiveRouteGuardTests` (Phase 01 Task 1.8): the route with its query string, its domain permission, how to read the checked total, and the expected `OwnBranch` and `Company` totals. The four theories of that class then cover the route. This replaces a separate "requires permission" case per test class. The rows of this phase:

  | Route | Permission | Checked total | Own branch | Company |
  |---|---|---|---|---|
  | `cash/summary?{October}` | `reports.cash` | `Payments` | 5,700 | 5,823 |
  | `cash/series?{October}&granularity=Day` | `reports.cash` | Σ `Payments` of the points | 5,700 | 5,823 |
  | `cash/breakdown?{October}` | `reports.cash` | Σ `Total` of the `Payment` rows of `ByReason` | 5,700 | 5,823 |
  | `debt/summary?at=2026-10-31` | `reports.debt` | `Receivable.Balance` | 1,500 | 11,499 |
  | `debt/breakdown?at=2026-10-31` | `reports.debt` | Σ `Balance` of `TopDebtors` | 1,500 | 11,499 |

  The dataset has no cash document in branch B, so the guard class's arrange method also posts one payment of 123 from branch B's cash fund on 10-06 with the reason `CK` (no partner, a person name, `AcknowledgeNegativeFund = true`, posted with a client on branch B). That reason has no debt effect, so the sales and debt rows keep the dataset figures.

### Cash

- **Funds of the scope:** `_db.Funds` with `BranchId` in the scope (inactive funds included, as in the cash book).
- **Movements:** `FundMovementQuery.LoadAsync(_db, fundIds, ct)`. It is the only source: it already leaves out cancelled and deleted documents and includes automatic vouchers. With no fund in the scope, skip the call and return zeros.
- **Balance of a fund at `at`:** the sum of `Delta` of its movements with `Kind == Opening` or `At < atEnd`.
- **Receipts of a window:** the sum of `Delta` of `Receipt` movements with `At` in `[start, end)`. **Payments:** the sum of `−Delta` of `Payment` movements. Transfers and openings are neither.
- **Net** = receipts − payments; `PrevNet` is the same on the previous period; `NetDeltaPct = ExecutiveMath.DeltaPct(Net, PrevNet)`.
- **Funds list:** one row per fund, ordered by `BranchCode`, `Kind`, `Code` (ordinal). Branch codes come from `Branches.IgnoreQueryFilters()`.
- **Series:** receipts and payments per `ExecutiveSeries.BucketOf(VnTime.ToVnDate(movement.At), granularity)`, zero-filled over `ExecutiveSeries.Buckets(...)`.
- **By reason:** `CashVouchers` with `Status == DocumentStatus.Active`, `FundId` among the funds of the scope and `VoucherAt` in `[start, end)`, grouped by `ReasonId` and `Type`: `Total = Sum(Total)`, `Count`. Code and name from `CashReasons.IgnoreQueryFilters()`. Order: receipts first, then `Total` descending, then `ReasonCode`.
- **Largest:** the `Receipt` and `Payment` movements of the period ordered by `|Delta|` descending, then `At`, then `Code`; the first 10. `Id = SourceId`, `Total = |Delta|`.

### Debt

- **Open items of a side:** for each branch of the scope, `DebtPositionQuery.LoadAsync(_db, branchId, side, null, atEnd, ct)`, keep `Open > 0`, map to `OpenDebtItem` with that `branchId`.
- **Totals** (`DebtPositionMath.Totals`):
  - `TotalOpen` = Σ `Open` of `Increase` items; `Unallocated` = Σ `Open` of `Decrease` items;
  - an item is overdue when it is an `Increase` and `Due < at`; its bucket is `DebtAging.BucketIndex(DebtDates.DaysOverdue(at, Due), boundaries)`; `OverdueAmounts` has `boundaries.Count + 1` entries;
  - `OverduePartnerCount` = distinct partners with an overdue item;
  - an item is due soon when it is an `Increase` and `at <= Due <= at.AddDays(DueSoonDays)`.
- The summary uses `DebtAging.DefaultBoundaries` (30/60/90) for both sides.
- **Top debtors:** receivable items grouped by partner over all branches of the scope; `Balance = DebtPositionMath.Balance(group)`, `Overdue` = Σ `Open` of its overdue items; keep `Balance > 0`; order by `Balance` descending, then `PartnerCode`; the first 10. Names from `Customers.IgnoreQueryFilters()`.
- **Over limit:** for each `CreditLimits` row with `BranchId` in the scope: `Balance` = `DebtPositionMath.Balance` of the receivable items of that branch and customer; a row when `Balance > Amount`. Order by `Over` descending.
- **By employee:** receivable `Increase` items grouped by `OwnerUserId`: `Open` = Σ `Open`, `Overdue` = Σ `Open` of overdue items. Names from `Users.IgnoreQueryFilters()` (`FullName`). Order by `Open` descending; the row without an owner is last.
- **Payables due:** payable items with `IsDueWithin(item, at, 30)`, ordered by `Due`, then `DocCode`; at most 50 rows.

## Tasks

Set the test database first in every shell:
`export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_dash;Username=postgres;Password=1"`. Backend commands run from `backend/`. Every integration test class inherits `ExecutiveDashboardTestBase` and seeds `ReportDataset.SeedAsync(this)` unless the case says otherwise.

### Task 2.1 — `cash/summary`

1. Write the failing test `CashSummaryTests`:
   - `Summary_of_main_branch`: `cash/summary?{October}&branchId={main}` → `Receipts 5000`, `Payments 5700`, `Net -700`, `PrevNet 0`, `NetDeltaPct null`, `Balance 29300`, `CashBalance 19300`, `BankBalance 10000`, `At 2026-10-31`; `Funds` has the fund `QtmId` with `Balance 19300` and `Nh01Id` with `Balance 10000`.
   - `Balance_is_as_at_the_given_date`: `cash/summary?{OctoberRange}&at=2026-10-14&branchId={main}` → fund `QtmId` 15,000, `Nh01Id` 15,000, `Balance 30000` (the transfer of 10-15 and the payment of 10-16 are not counted yet), while `Receipts 5000` and `Payments 5700` are unchanged.
   - `Summary_equals_the_cash_book`: main client, `GET /api/reports/cash-book?kind=All&from=2026-10-01&to=2026-10-31` → `GrandIn == Receipts`, `GrandOut == Payments`, `GrandClosing == Balance`, and each group's `Closing` equals the fund's `Balance`.
   - `Company_wide_includes_every_branch`: no `branchId` → `Balance 29300`, and `Funds` contains a fund of `BranchBId`.
   - the `cash/summary` row of `ExecutiveRouteGuardTests` (with the branch-B payment arrangement).
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashSummaryTests|FullyQualifiedName~ExecutiveRouteGuardTests"`. Expected: FAIL — build error, `CashSummaryDto` does not exist.
3. Implement `ExecutiveLookups` (moving the name lookups out of `SalesMetricsService`, which now uses it), `Models/CashModels.cs`, `ICashMetricsService`, `CashMetricsService.GetSummaryAsync`, the action `GET cash/summary` and the `AddScoped` lines.
4. Run the same command, then `--filter "FullyQualifiedName~Sales"` (the lookups moved). Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): cash summary endpoint"`.

### Task 2.2 — `cash/series`

1. Write the failing test `CashSeriesTests` (`cash/series?{October}&granularity=Day&branchId={main}`):
   - `Daily_points`: 31 points; 10-03 → `Receipts 1000`; 10-05 → `Receipts 4000`; 10-10 → `Payments 2000`; 10-12 → `Payments 3000`; 10-16 → `Payments 700`; 10-15 (the transfer) and 10-18 (the cancelled receipt) → both 0.
   - `Monthly_point`: `granularity=Month` → one point with `Receipts 5000`, `Payments 5700`.
   - the `cash/series` row of `ExecutiveRouteGuardTests`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashSeriesTests"`. Expected: FAIL — 404 (no route).
3. Implement `GetSeriesAsync` and the action `GET cash/series`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): cash series endpoint"`.

### Task 2.3 — `cash/breakdown`

1. Write the failing test `CashBreakdownTests` (`cash/breakdown?{October}&branchId={main}`):
   - `By_reason`: five rows; receipts first: `TKH` (`Receipt`, `Total 4000`, `Count 1`), `TPX` (`Receipt`, `Total 1000`, the automatic receipt); then payments: `TNCC` (`Total 3000`), `CPN` (`Total 2000`, the automatic payment), `CK` (`Total 700`); the receipt rows sum to 5,000 and the payment rows to 5,700.
   - `Largest_documents`: `Largest` codes in order `PT00002, PC00002, PC00001, PT00001, PC00003` with totals `4000, 3000, 2000, 1000, 700`; `PT00003` (cancelled) is absent.
   - the `cash/breakdown` row of `ExecutiveRouteGuardTests`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashBreakdownTests"`. Expected: FAIL — 404 (no route).
3. Implement `GetBreakdownAsync` and the action `GET cash/breakdown`.
4. Run `--filter "FullyQualifiedName~CashBreakdownTests|FullyQualifiedName~CashSummaryTests|FullyQualifiedName~CashSeriesTests"`. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): cash breakdown endpoint"`.

### Task 2.4 — `DebtPositionMath`

1. Write the failing unit test `Unit/DebtPositionMathTests` (`at = 2026-10-31`, boundaries 30/60/90, partners P1 and P2):
   - `Totals_split_open_overdue_and_due_soon`, items: P1 Increase 1,000 due 09-20; P1 Increase 2,000 due 10-03; P2 Increase 500 due 10-31; P2 Increase 700 due 11-07; P2 Increase 900 due 11-08; P1 Decrease 1,500 →
     `TotalOpen 5100`, `Unallocated 1500`, `Balance 3600`, `Overdue 3000`, `OverdueAmounts [2000, 1000, 0, 0]`, `NotDue 2100`, `OverduePartnerCount 1`, `DueSoon 1200`, `DueSoonCount 2`.
   - `Due_falls_back_to_the_document_date`: an Increase with `DueDate null` and `DocDate 10-01` is overdue at 10-31 (30 days → the first bucket).
   - `Bucket_edges`: overdue by 30 days → index 0; 31 → index 1; 90 → index 2; 91 → index 3.
   - `Decrease_items_are_never_overdue_or_due_soon`.
   - `Balance_subtracts_decrease_items`: `Balance([Increase 300, Decrease 100]) == 200`.
   - `Empty_input_gives_zero_filled_amounts`: `Totals([], at, boundaries).OverdueAmounts == [0, 0, 0, 0]`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DebtPositionMathTests"`. Expected: FAIL — build error, `DebtPositionMath` does not exist.
3. Implement `OpenDebtItem.cs` and `DebtPositionMath.cs`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): debt position math"`.

### Task 2.5 — `debt/summary`

1. Write the failing test `DebtSummaryTests`:
   - `Receivable_and_payable_of_main_branch`: `debt/summary?at=2026-10-31&branchId={main}` →
     `OverdueBuckets` = (1, 30), (31, 60), (61, 90), (91, null);
     `Receivable`: `TotalOpen 3000`, `Unallocated 1500`, `Balance 1500`, `Overdue 3000`, `OverdueAmounts [2000, 1000, 0, 0]`, `NotDue 0`, `OverduePartnerCount 1`;
     `Payable`: `TotalOpen 4000`, `Unallocated 3000`, `Balance 1000`.
   - `Summary_equals_the_reports` (main client, both sides):
     - `GET /api/reports/debt-overdue?side={side}&at=2026-10-31` → `TotalOverdue == Overdue` and `TotalAmounts == OverdueAmounts`;
     - `GET /api/reports/debt-aging?side={side}&at=2026-10-31` → `Totals.TotalOpen`, `Totals.Unallocated`, `Totals.Balance` equal the summary;
     - `GET /api/reports/debt-balance?side={side}&from=2026-10-01&to=2026-10-31` → `Totals.ClosingDebit − Totals.ClosingCredit == Balance` for `Receivable` and `Totals.ClosingCredit − Totals.ClosingDebit == Balance` for `Payable`.
   - `Position_is_as_at_the_given_date`: `at=2026-10-04&branchId={main}` → `Receivable.TotalOpen 7000`, `Unallocated 1000`, `Balance 6000` (the receipt of 10-05 and the return of 10-08 are not counted yet).
   - `Company_wide_adds_branches`: `at=2026-10-31` without `branchId` → `Receivable.Balance 11499`.
   - `Due_soon_counts_the_next_seven_days`: post one more unpaid NMH of 800 from `Ncc1Id` on 10-20 with `DueDate 2026-10-25` into `MainWarehouseId` (an untracked product, 1 × 800; build the request by hand as the posting recipe of `SUMMARY.md` says); `debt/summary?at=2026-10-21&branchId={main}` → `Payable.DueSoon 800`, `DueSoonCount 1`; `at=2026-10-26` → `Payable.DueSoon 0` and `Payable.Overdue` is 800 more than at 10-21.
   - the `debt/summary` row of `ExecutiveRouteGuardTests`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DebtSummaryTests"`. Expected: FAIL — build error, `DebtSummaryDto` does not exist.
3. Implement `Models/DebtModels.cs`, `IDebtMetricsService`, `DebtMetricsService.GetSummaryAsync`, the action `GET debt/summary` and the `AddScoped` line.
4. Run the same command. Expected: PASS. If an identity with a report fails, read that report's code in `DebtPositionReportService` and mirror its rule; do not change the report.
5. Commit: `git commit -m "feat(dashboard): debt summary endpoint"`.

### Task 2.6 — `debt/breakdown`

1. Write the failing test `DebtBreakdownTests` (`at=2026-10-31` unless stated):
   - `Top_debtors`: `branchId={main}` → one row: `PartnerId Kh1Id`, `PartnerCode Kh1Code`, `Balance 1500`, `Overdue 3000`; without `branchId` → `Balance 11499`.
   - `Over_limit`: set the limit of `Kh1Id` to 1,000 in the main branch (`PUT /api/credit-limits/{Kh1Id}` with the main client) → one row: `Balance 1500`, `Limit 1000`, `Over 500`, `BranchId MainBranchId`; with the limit at 2,000 → no row.
   - `By_employee_equals_the_report`: `branchId={main}` → two rows: the admin user with `Open 2000`, `Overdue 2000`, then the row without an owner (`OwnerUserId null`) with `Open 1000`, `Overdue 1000`; for each row of `GET /api/reports/debt-by-salesperson?side=Receivable&from=2026-10-01&to=2026-10-31` (main client), the breakdown row with the same `OwnerUserId` has `Open == ClosingOpen` and `Overdue == ClosingOverdue`.
   - `Payables_due_within_thirty_days`: one more unpaid NMH of 800 from `Ncc1Id` on 10-20 with `DueDate 2026-11-15` (built by hand, as above); `debt/breakdown?at=2026-10-21&branchId={main}` → one row with that voucher's code, `DueDate 2026-11-15`, `Open 800`, `SourceType StockIn`; in a second test with `DueDate 2026-11-25` → no row.
   - the `debt/breakdown` row of `ExecutiveRouteGuardTests`.
2. Run `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DebtBreakdownTests"`. Expected: FAIL — 404 (no route).
3. Implement `GetBreakdownAsync` and the action `GET debt/breakdown`.
4. Run `--filter "FullyQualifiedName~DebtBreakdownTests|FullyQualifiedName~DebtSummaryTests"`. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): debt breakdown endpoint"`.

## Verification

From `backend/`, with `TEST_DB_CONNECTION` set:

- `dotnet build OrderMgmt.sln` — 0 errors.
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard"` — all pass (Phase 01 tests included).

## Exit Criteria

- The five routes return the dataset figures of `SUMMARY.md`.
- The identities with the cash book, overdue, aging, balance and by-salesperson reports hold for both sides.
- No existing report file was changed (decision P23 of `SUMMARY.md`: the dashboard keeps its own aggregation and the identity tests guard it).
- Six commits, one per task.
