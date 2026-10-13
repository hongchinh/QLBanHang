# Executive dashboard — shared dashboard base + director view (Bảng điều hành)

> Created: 2026-10-13 23:13:01 · Source design: [brainstorm SUMMARY](../../brainstorms/261013-2247-executive-dashboard/SUMMARY.md), mockups [layout options](../../brainstorms/261013-2247-executive-dashboard/mockup-01-layout-options.html) and [overview tab](../../brainstorms/261013-2247-executive-dashboard/mockup-02-overview-tab.html)

## Goal

Give directors and managers one page, `/dashboard/executive`, that shows sales (revenue, cost, gross profit from stock-out vouchers), cash, debt and inventory for the whole company or one branch, lists what needs attention, and refreshes within one or two seconds after a document is saved. The backend is a new read-only module whose figures equal the existing reports; the frontend adds a tile kit that the later role dashboards (accountant, warehouse, sales) reuse.

## Scope

**In scope**

- Backend:
  - permission `dashboard.executive`;
  - read-only module `Application/Dashboards/Executive` with sales, cash, debt and inventory metrics, branch comparison, attention list and recent documents; 13 routes under `api/dashboard/executive`;
  - two small helpers moved out of `InventoryReportService` (`CostProvisional`, `StockValueSplit`);
  - the "dashboard changed" SignalR signal: `[DashboardSignal]` action filter on the document controllers, `IRealtimeNotifier.NotifyDashboardChangedAsync`, hub groups per branch.
- Frontend:
  - route `/dashboard/executive`, sidebar item, permission code;
  - range presets "Tuần này" and "Năm nay";
  - feature `features/executive-dashboard` (types, api, keys, hooks, params, live refresh);
  - shared tiles in `features/dashboard/components` (`StatTile`, `TileCard`, `LiveStatus`);
  - the page with the tabs Tổng quan, Kinh doanh, Tiền, Công nợ, Kho.
- Docs: `docs/SUMMARY.md`, `conventions.md`, `directory-structure.md`, `system-architecture.md`, `product-goals.md`.

**Out of scope** (brainstorm non-goals)

- Quotation figures; the two quotation dashboards (`/`, `/admin/dashboard`) and `DashboardService` stay as they are.
- The accountant, warehouse and sales dashboards (sub-projects 2–4).
- Slow-moving stock, cash-flow forecast, budgets, targets, PDF/Excel export, per-user tile layout.
- New tables, materialized aggregates, caching.
- Changing the existing report pages (the cash book and stock-on-hand pages keep their filters in component state, so links to them carry no filter).

## Decisions made while planning (in addition to the brainstorm)

| # | Topic | Decision |
|---|---|---|
| P1 | Workspace | Branch `feat/executive-dashboard`, created from `main` (`a76ce85`, equal to `qlbanhang/main`) in the worktree `.claude/worktrees/executive-dashboard`. The main working tree is on `feat/tt133-report-forms`, whose plan is being executed by another session, so nothing in this plan runs there. |
| P2 | Test database | This worktree uses its own base name, `qldonhang_integtest_dash`. `PostgresFixture` drops and recreates `<base>_tpl` at the start of every run, so two sessions on the same base name break each other. |
| P3 | What is a sale | A stock voucher whose reason is the system reason `XBH`; a return is the system reason `NTL` (`StockReasonCodes.Sale`, `StockReasonCodes.SaleReturn`). User-created reasons do not count, even with the same debt effect. This follows the brainstorm's wording; see Open questions. |
| P4 | Revenue before VAT | Per line: `Amount − DiscountAmount − OrderDiscountAllocated`, minus `VatAmount` when the voucher is a stock-out and the line is priced VAT-inclusive. There is no stored before-VAT column. Freight is not revenue. |
| P5 | Cost | A sale's cost is the `CostAmount` of its `StockOut` ledger rows. A return reduces cost by the `InValue` of its `StockIn` ledger rows (the value at which the goods re-enter stock). Lines that do not track inventory have no cost. Cost and gross profit are provisional while the costing period containing the last day of the range has not ended. |
| P6 | Balance date | Balance figures (fund balances, debt, attention) are "as at" `at`, default VN today. The UI never sends `at`; the tests do, because the report dataset holds documents dated after the day the tests run. |
| P7 | Company scope | `branchId` omitted: every branch for a user with `branches.access_all`, the user's own branch otherwise. The services loop over the branches and call the existing per-branch query helpers. No dependency on the `ReportScope` of the TT133 plan. |
| P8 | Routes | One route per tile group and domain (13 routes) instead of the seven names in the brainstorm, so a signal for one domain refetches only that domain. |
| P9 | Permissions | The controller needs `dashboard.executive`. Each domain also needs its report permission: sales `reports.profit`, cash `reports.cash`, debt `reports.debt`, inventory `reports.inventory`. Cost, gross profit and stock value are `null` without `inventory.view_cost`. `branches` needs `branches.access_all`. `attention` and `recent` return only the parts the user may see. |
| P10 | Debt term structure | The receivable tile shows "Trong hạn" plus the overdue buckets of the Nợ quá hạn report (default boundaries 30/60/90, so 1–30, 31–60, 61–90, 91+). The mockup drew three overdue buckets; four keeps the tile equal to the report. |
| P11 | Top customers | Revenue and gross profit. The mockup's "Còn nợ" column is dropped: it needs the debt position of every branch on each sales refresh. Debtors are on the Công nợ tab. |
| P12 | Where the signal is sent | An action filter on the document controllers sends it after a successful non-GET action, that is after the service has committed. The brainstorm put it in the services; a filter needs no change in nine services, no after-commit hook, and also covers writers that do not use `ITransactionRunner` (credit limits). |
| P13 | Signal groups | `dashboard-all` for users with `branches.access_all`, `dashboard-branch-{id}` for the others (their default branch). The payload is `{ branchId, domains }`, no figures. |
| P14 | Branch filter in the UI | The select lists "Toàn công ty" and the branches. Choosing a branch switches the working branch (`useSwitchWorkingBranch`) and sets `scope=branch`, so the header switcher, the dashboard and every report link show the same branch. Users without `branches.access_all` get no select. |
| P15 | Links | Report links go to the existing report routes and carry the parameters those pages read from the URL (debt reports: `side`, `at` or `from`/`to`). Cash book and stock on hand open without parameters. With "Toàn công ty" selected a report link opens the report of the working branch and says so in its `title`. A document code is a link only when the document belongs to the working branch: the document screens reject a document of another branch with 403. |
| P16 | Sidebar | One item "Điều hành" in the Báo cáo group, section "Tổng hợp", icon `Presentation`, first in the group. The top "Tổng quan" entry is unchanged (the landing page question is open). |
| P17 | KPI tile | A new compact `StatTile`; the existing `KpiCard` (large number plus sparkline) does not fit seven tiles in one row at 1366 px and stays for the quotation dashboards. |
| P18 | Recent documents | Receipts, payments, stock-in and stock-out vouchers created today (VN), active, newest first. Automatic cash vouchers are left out (their stock voucher is listed). |
| P19 | Execution | One phase per run, by one sonnet implementer, followed by a spec review and a quality review of the phase diff (user preference recorded on 2026-10-08 and 2026-10-12). |
| P20 | Range presets | "Tuần này" and "Năm nay" are added to the existing `lib/date-range-presets.ts`; the two private copies in the dashboard feature are removed. No third preset module. |
| P21 | Number formats | KPI tiles and the branch comparison print compact money ("2,84 tỷ", "412 tr"); tables print full numbers like the report pages; percentages and deltas on the new page use a decimal comma ("▲ 12,5%"). The existing `formatDelta` (decimal point) is left alone for the quotation dashboards. |
| P22 | Header | The mockup's comparison text is shown as "So với kỳ trước: dd/MM/yyyy – dd/MM/yyyy"; the chosen period itself is printed by the range picker. The mockup's "Chi tiết →", "Kinh doanh →" and "Xem thêm →" actions open the Kinh doanh tab. |
| P23 | Shared rules | The dashboard reads debt and fund data through the reports' own query helpers and reuses `DebtAging`, `DebtDates`, `CostProvisional` and `StockValueSplit`, but keeps its own aggregation on top (`DebtPositionMath`, the per-product value loop). The report services are not rewritten to call it: `feat/tt133-report-forms` is changing those files. Tests that compare each figure with its report are the guard; unifying the two is a follow-up after both branches are merged. |
| P24 | Reconnecting | The notification hub now retries without an end (it used to give up after about 42 seconds) and retries a failed first connection, so the "Mất kết nối" label always recovers. This also benefits the notification bell. |

## Assumptions

- The brainstorm is the approved spec; P1–P19 refine it.
- Its open questions are not answered. The plan proceeds on these choices, each easy to change later:
  - revenue is before VAT (P4);
  - "by employee" means the voucher's `OwnerUserId` (the creator), as in the Công nợ theo nhân viên report;
  - only ADMIN and MANAGER get `dashboard.executive` (they hold every permission); ACCOUNTANT is not granted it by default;
  - the quotation dashboards stay the landing pages.
- Until inventory, cash and debt go live (after Round 3) the page shows zeros and empty states. That is accepted.
- Volumes are small (tens of thousands of ledger rows a year), as the existing reports assume.
- Permissions are JWT claims: a user must sign in again to receive `dashboard.executive`.
- Local setup: PostgreSQL on `localhost:5432` (`postgres`/`1`). Never point tests at `qldonhang_test` or `qldonhang`.

## Risks

- **Company-wide reads are heavy.** Debt loads every counted entry of each branch and side; cash loads the full movement history of every fund. Each dashboard refresh repeats that. Mitigation: the signal is batched per second and the fallback runs once a minute; Phase 08 lists the response times of the 13 routes on real-like data as a manual check, and an index or a short cache is a separate decision if a route takes over one second. Measured in Phase 01 on a synthetic year (20,001 sales vouchers, 40,001 lines and 40,000 ledger rows in one branch; second call): `sales/summary` 0.35 s, `sales/series` 0.36–0.37 s, `sales/top` by customer 0.38 s and by product 0.13 s on a quiet machine, 0.9–1.35 s on a loaded one. The summary reads two periods and the synthetic previous year was empty, so in steady state it is about twice that (roughly 0.7 s at 20,000 vouchers a year) and it passes one second near 30,000 sales vouchers a year. That volume is the trigger for the follow-up: sum revenue with a grouped query over the lines (the shape the product list already uses) instead of shipping one row per voucher; the Phase 01 tests make that change safe.
- **Figures drift from the reports.** Mitigation: the services call the reports' own query helpers (`DebtPositionQuery`, `FundMovementQuery`) and each phase has tests that compare a dashboard figure with the report route for the same branch and date.
- **Merge with `feat/tt133-report-forms`.** Both branches edit `Permissions.cs`, `DbSeeder.cs`, `permissions.ts`, `route-permissions.ts`, `nav-config.ts`, `App.tsx`, `InventoryReportService.cs` and the docs. The edits here are small and local; whichever branch merges second resolves by keeping both sides. Known on 2026-10-14: `DependencyInjection.cs` will conflict where both branches add registrations after the report services (keep all of them); `Permissions.cs` and `DbSeeder.cs` merge cleanly; the other branch now adds `GetStockSummaryAsync` to `InventoryReportService.cs` directly above `SplitByQuantity` and calls the private `IsProvisional`, so Phase 03 Task 3.1 (moving `SplitByQuantity` out) will conflict there — keep their method, apply the move, and keep the private `IsProvisional` wrapper.
- **A signal is missed** (a controller without the attribute, a dropped connection). Mitigation: every executive query refetches every 60 seconds while the tab is visible.
- **EF translation of the sales queries** (a conditional inside `Sum`, `Any` over a filtered voucher query). Mitigation: Phase 01 gives the query shapes; if a shape does not translate, the implementer changes the shape, not the rule, and reports it.
- **Baseline failures.** `main` has 19 backend and 3 frontend known failures (`BASELINE.md`). Later runs compare test by test.

## Open questions (for the BA / accountant; none blocks development)

- Should a user-created stock reason that increases the receivable count as a sale (P3)?
- **Gross profit on returns (P5) — answer before the figures are shown to users (Phase 06).** A return re-enters stock at the price typed on the return voucher, which is normally the selling price, and the dashboard subtracts that value from cost. Example: buy 10 × 100, sell 4 × 150, return 1 × 150 → the dashboard shows revenue 450, cost 268, gross profit 182; valued at goods cost the gross profit is 150. Returning all four gives revenue 0 and gross profit +143. The stock ledger already books returns this way; the question is whether the accountant wants returns valued at cost instead (a change to stock posting, not only to the dashboard).
- **Input VAT inside cost (P4/P5) — same deadline.** With the seeded setting `PurchaseCostIncludesVat = true`, the cost of goods includes input VAT while revenue excludes output VAT. Example: buy 10 × 100 plus 10 % VAT, sell 4 × 150 → the margin shows 26.67 % instead of 33.33 %. Either the setting is switched off at go-live for a company that deducts VAT, or the dashboard should compare like with like.
- Revenue before or after VAT; freight as revenue (P4)?
- Should ACCOUNTANT get `dashboard.executive` now or wait for the accountant dashboard?
- Should `/dashboard/executive` replace `/admin/dashboard` as the landing page of ADMIN and MANAGER?
- Should report links follow the company scope once the TT133 `scope=Company` parameter is merged?

## Phases

- [x] Phase 00 — Workspace, docs commit and failure baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Backend foundation and sales metrics (L) — `phase-01-foundation-sales.md`
- [ ] Phase 02 — Cash and debt metrics (L) — `phase-02-cash-debt.md`
- [ ] Phase 03 — Inventory metrics, branch comparison, attention, recent documents (L) — `phase-03-inventory-overview.md`
- [ ] Phase 04 — Real-time signal (M) — `phase-04-realtime-signal.md`
- [ ] Phase 05 — Frontend foundation: route, presets, data layer, live refresh, tile kit (L) — `phase-05-frontend-foundation.md`
- [ ] Phase 06 — Frontend page shell and the Tổng quan tab (L) — `phase-06-overview-tab.md`
- [ ] Phase 07 — Frontend deep-dive tabs (M) — `phase-07-deep-dive-tabs.md`
- [ ] Phase 08 — Docs, final verification, execution report (M) — `phase-08-docs-and-verification.md`

Phases run in order. Phase 01 freezes the query classes, the scope resolver, the period and the controller shape; Phases 02 and 03 add services and routes to them. Phase 05 freezes the frontend data layer and the tile kit; Phases 06 and 07 only compose them.

## Shared contracts

### Backend (frozen in Phase 01 unless a phase says otherwise)

```csharp
// Domain/Constants/Permissions.cs
public static class Dashboard { public const string Executive = "dashboard.executive"; }

// Domain/Constants/StockReasonCodes.cs
public static class StockReasonCodes { public const string Sale = "XBH"; public const string SaleReturn = "NTL"; }

// Application/Dashboards/Executive/Models/ExecutiveModels.cs
public class ExecutiveQuery
{
    public DateOnly? From { get; set; }      // default: first day of To's month
    public DateOnly? To { get; set; }        // default: VN today
    public Guid? BranchId { get; set; }      // omitted: every branch the user may see (P7)
    public DateOnly? At { get; set; }        // as-at date of balance figures, default VN today (P6)
}
public enum SeriesGranularity { Day = 0, Week = 1, Month = 2 }
public class ExecutiveSeriesQuery : ExecutiveQuery { public SeriesGranularity Granularity { get; set; } }
public enum SalesTopBy { Customer = 0, Product = 1, Employee = 2 }
public enum SalesTopSort { Revenue = 0, GrossProfit = 1 }
public class SalesTopQuery : ExecutiveQuery
{
    public SalesTopBy By { get; set; }
    public SalesTopSort Sort { get; set; }
    public int Limit { get; set; } = 10;
}
public class RecentQuery { public Guid? BranchId { get; set; } public int Limit { get; set; } = 10; }

// Application/Dashboards/Executive/Common/
public sealed record ExecutiveScope(IReadOnlyList<Guid> BranchIds, bool IsCompanyWide);
public sealed record ExecutivePeriod(DateOnly From, DateOnly To, DateOnly PrevFrom, DateOnly PrevTo)
{
    public static ExecutivePeriod Create(DateOnly? from, DateOnly? to, DateOnly today);
    public DateTimeOffset Start { get; }      // VnTime.ToUtc(VnTime.StartOfDay(From))
    public DateTimeOffset End { get; }        // VnTime.ToUtc(VnTime.StartOfNextDay(To)), exclusive
    public DateTimeOffset PrevStart { get; }
    public DateTimeOffset PrevEnd { get; }    // equals Start
}
public static class ExecutiveMath
{
    public static decimal? DeltaPct(decimal current, decimal previous);     // null when previous == 0; divides by |previous|; 2 decimals
    public static decimal? MarginPct(decimal revenue, decimal grossProfit); // null when revenue == 0; 2 decimals
}
public static class ExecutiveSeries
{
    public static DateOnly BucketOf(DateOnly date, SeriesGranularity granularity);                 // Day: date; Week: its Monday; Month: day 1
    public static IReadOnlyList<DateOnly> Buckets(DateOnly from, DateOnly to, SeriesGranularity granularity);  // distinct, ascending
}

// Application/Dashboards/Executive/Interfaces/IExecutiveScopeResolver.cs
public interface IExecutiveScopeResolver { Task<ExecutiveScope> ResolveAsync(Guid? branchId, CancellationToken ct = default); }

// Application/Dashboards/Executive/Realtime/DashboardDomains.cs (Phase 04)
[Flags] public enum DashboardDomains { None = 0, Sales = 1, Cash = 2, Debt = 4, Inventory = 8, All = 15 }
```

Every service method starts the same way and wraps its queries in one snapshot:

```csharp
public Task<XDto> GetXAsync(ExecutiveQuery query, CancellationToken ct = default) =>
    _transaction.RunSnapshotAsync(c => BuildXAsync(query, c), ct);

// inside BuildXAsync
var today = VnTime.ToVnDate(_clock.UtcNow);
var scope = await _scopes.ResolveAsync(query.BranchId, ct);
var period = ExecutivePeriod.Create(query.From, query.To, today);   // period figures
var at = query.At ?? today;                                         // balance figures
var atEnd = VnTime.ToUtc(VnTime.StartOfNextDay(at));                // exclusive instant for balance figures
```

### Routes (`ExecutiveDashboardController`, `[Route("api/dashboard/executive")]`, class-level `[HasPermission(Permissions.Dashboard.Executive)]`)

| Route | Method permission | Query | Returns | Phase |
|---|---|---|---|---|
| `GET sales/summary` | `reports.profit` | `ExecutiveQuery` | `SalesSummaryDto` | 01 |
| `GET sales/series` | `reports.profit` | `ExecutiveSeriesQuery` | `SalesSeriesDto` | 01 |
| `GET sales/top` | `reports.profit` | `SalesTopQuery` | `List<SalesTopRowDto>` | 01 |
| `GET cash/summary` | `reports.cash` | `ExecutiveQuery` | `CashSummaryDto` | 02 |
| `GET cash/series` | `reports.cash` | `ExecutiveSeriesQuery` | `CashSeriesDto` | 02 |
| `GET cash/breakdown` | `reports.cash` | `ExecutiveQuery` | `CashBreakdownDto` | 02 |
| `GET debt/summary` | `reports.debt` | `ExecutiveQuery` | `DebtSummaryDto` | 02 |
| `GET debt/breakdown` | `reports.debt` | `ExecutiveQuery` | `DebtBreakdownDto` | 02 |
| `GET inventory/summary` | `reports.inventory` | `ExecutiveQuery` | `InventorySummaryDto` | 03 |
| `GET inventory/breakdown` | `reports.inventory` | `ExecutiveQuery` | `InventoryBreakdownDto` | 03 |
| `GET branches` | `branches.access_all` | `ExecutiveQuery` | `List<BranchComparisonRowDto>` | 03 |
| `GET attention` | — | `ExecutiveQuery` | `AttentionDto` | 03 |
| `GET recent` | — | `RecentQuery` | `List<RecentDocumentDto>` | 03 |

Two `[HasPermission]` attributes (class and method) are both required. Every action validates its query with the injected FluentValidation validator (`ValidateAndThrowAsync`) and returns `Success(...)`, as `CashDebtReportsController` does. The DTOs are defined in the phase that adds the route.

### Frontend (frozen in Phase 05)

```ts
// features/executive-dashboard/types.ts
export type ExecutiveDomain = 'sales' | 'cash' | 'debt' | 'inventory';
export type ExecutiveTab = 'overview' | ExecutiveDomain;
export type SeriesGranularity = 'Day' | 'Week' | 'Month';
export interface ExecutiveParams { from: string; to: string; branchId?: string }
// one interface per backend DTO, same property names in camelCase; enums are their names as strings

// features/executive-dashboard/keys.ts
export const executiveKeys = {
  all: ['executive'] as const,
  domain: (d: ExecutiveDomain | 'cross') => [...executiveKeys.all, d] as const,
  // one factory per route, e.g. salesSummary: (p) => [...executiveKeys.domain('sales'), 'summary', p] as const
};

// lib/realtime-bus.ts
export type RealtimeStatus = 'connecting' | 'connected' | 'disconnected';
export interface DashboardChangedSignal { branchId: string; domains: string[] }   // names the client does not know are skipped
```

`branches`, `attention` and `recent` live under `executiveKeys.domain('cross')` and are refetched on every signal.

## Executor conventions

- **Where to work.** Everything happens in `D:\Projects\QLBanHang\.claude\worktrees\executive-dashboard` (branch `feat/executive-dashboard`). Paths in the phase files are relative to that directory. Never edit, stage or run commands in `D:\Projects\QLBanHang` itself: another session works there on another branch. Start every Bash command with `cd /d/Projects/QLBanHang/.claude/worktrees/executive-dashboard` (or its `backend` / `frontend` folder).
- **Commits.** One commit per task, conventional commits with scope `dashboard`, `reports`, `realtime` or `frontend` (e.g. `feat(dashboard): sales summary endpoint`). End every commit message with the attribution line from the session's system reminder. Stage files explicitly: `git add <paths>` with the files the task names (frontend tasks list them; a backend task's files are the ones of the phase's "Files" section that the task created or changed). Never `git add -A` or `git add .`; after the commit `git status --short` must be empty.
- **Shell.** Commands are written for Git Bash (the Bash tool). If NuGet restore fails with "path1 null", set `APPDATA` and `LOCALAPPDATA` in that shell.
- **Test database** — set before any backend test command (P2):
  - `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_dash;Username=postgres;Password=1"`
- **Backend commands** (from `backend/`):
  - Build: `dotnet build OrderMgmt.sln`.
  - Targeted tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`; join classes with `|`.
  - Pure unit tests of this plan: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard.Unit"`.
  - Full suite (21 minutes): `dotnet test OrderMgmt.sln --logger "trx;LogFileName=dash.trx" --results-directory "$TEMP/qlbh-dash-results"`. It runs only where a phase says so. Never write a `.trx` into the repository: five old ones are tracked.
- **Frontend commands** (from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`. After a build run `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo` from the worktree root (skip a path that is not tracked).
- **Test locations.**
  - Pure backend tests: `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/Unit/` (no collection attribute, no database). Types they test are `public`: the Application project has no `InternalsVisibleTo`.
  - Backend integration tests: `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/`, `[Collection(nameof(PostgresCollection))]`, class `: CashDebtTestBase` with the constructor `(PostgresFixture pg) : base(pg)`. Each test method gets its own database clone.
  - Frontend tests sit next to the file under test (`*.test.ts` / `*.test.tsx`).
- **Rules carried over from Rounds 1–2B.**
  - DTOs use public `{ get; set; }` auto-properties; lists default to `new()`.
  - Every `DateTimeOffset` sent to EF is UTC; VN dates become instants only through `VnTime.StartOfDay` / `StartOfNextDay`; never compare `.Date` of an instant.
  - Read names of partners, products, warehouses, reasons, funds and users with `IgnoreQueryFilters()` so soft-deleted ones keep their history — in a separate lookup query by id, never inside a query that reads vouchers, lines or other documents: it switches every filter of that query off, and deleted documents would be counted.
  - Debt "as at a date" is read only through `DebtPositionQuery`; fund movements only through `FundMovementQuery`.
  - Validation `details` keys are camelCase.
  - **RED can be a build error.** A test that references a type that does not exist yet fails to compile; report it as "FAIL — build error, `<Type>` does not exist" and paste the error line.
  - A happy-path task must not implement the validation or permission rules of a later task, or that task's RED step cannot fail.
- **Report dataset.** `ReportDataset.SeedAsync(this)` (in `CashDebt/Reports/ReportDataset.cs`) creates a fresh main branch and a branch B. The expectations used in this plan, for the main branch with `from=2026-10-01&to=2026-10-31&at=2026-10-31`:

  | Figure | Value | From |
  |---|---|---|
  | Sales revenue | 2,500 | XBH 3,000 on 10-03, NTL −500 on 10-08 (untracked products, VAT 0, so cost 0) |
  | Sales revenue of branch B | 9,999 | XBH on 10-03 |
  | Receipts | 5,000 | PT00001 1,000 (automatic, 10-03), PT00002 4,000 (10-05); PT00003 is cancelled |
  | Payments | 5,700 | PC00001 2,000 (automatic, 10-10), PC00002 3,000 (10-12), PC00003 700 (10-16) |
  | Fund balances | QTM 19,300 · NH01 10,000 | openings 10,000 / 20,000, transfer 5,000 NH01 → QTM on 10-15 |
  | Receivable of KH1 | open 3,000 · unallocated 1,500 · balance 1,500 | HD-OLD 1,000 left (due 09-20), sale 2,000 left, TT-OLD 1,000 and NTL 500 unallocated |
  | Payable of NCC1 | open 4,000 · unallocated 3,000 · balance 1,000 | NMH 6,000 less 2,000 paid; PC00002 3,000 not matched |
  | Receivable of KH1 in branch B | 9,999 | unpaid sale |

  Facts behind those numbers:
  - the partners have no credit days and the dataset sets no due date, so a voucher's debt is due on its document date; at 10-31 the receivable overdue is 3,000 with buckets `[2000, 1000, 0, 0]` (sale 28 days, HD-OLD 41 days);
  - the automatic receipt uses the cash reason `TPX` and the automatic payment `CPN`; the other reasons are `TKH` (PT00002), `TNCC` (PC00002) and `CK` (PC00003);
  - the receivable by employee has two rows: the admin user with open 2,000 / overdue 2,000 (the sale) and no owner with 1,000 / 1,000 (HD-OLD);
  - the documents are created during the test, so all of them are "created today".

- **Posting recipe** (for the extra documents the tests of Phases 01–04 post):
  - A branch from `CreateBranchAsync` has its numbering and one cash fund `QTM` with balance 0. Add a warehouse with `CreateWarehouseAsync`. Vouchers need no opening date.
  - Partners: `CreatePartnerAsync(code, isCustomer, isSupplier)`. XBH and NTL need a customer, NMH a supplier.
  - Products: `CreateInventoryProductAsync(code, track: …)` tracks inventory by default. An untracked product never touches stock or cost. The helper puts every product in a group; clear `ProductGroupId` with `InDbAsync` for a product without one.
  - A stock voucher: `VoucherRequest(type, await ReasonIdAsync("XBH"), "2026-10-20 09:00", LineRequest(productId, quantity, unitPrice))`, then set `WarehouseId` (the default is `KHO01` of the seeded branch `CN01`), `PartnerId` and, when the case needs them, `DueDate`, `PaymentMethodId` with `PaidAmount`, `AcknowledgeNegativeStock = true`; post it with `PostVoucherAsync(client, request)` through a client whose working branch is the voucher's branch (`CloneAdminClient()` + `UseBranch`). `ReportDataset.PostStockAsync` cannot set a due date.
  - VAT comes from the request line (`VatRate`), never from the product; "price includes VAT" comes from the product.
  - Costs are written when a voucher is posted; no recalculation call is needed.
  - Cancel: `POST /api/stock-vouchers/{id}/cancel` with `StockVoucherActionRequest { Version }`.
  - A cash voucher: `CashRequest(...)` and `CreateCashAsync`; set `AcknowledgeNegativeFund = true` when it makes the fund negative.
  - A fund balance without an opening date: `SetFundOpeningAmountAsync(fundId, amount)`; a second fund: `CreateBankFundAsync(code, branchId)`.
  - A credit limit: `PUT /api/credit-limits/{customerId}` with `{ amount }`, for the client's working branch.
  - A restricted user's default branch is the branch its request names.

- **Notation:** `10-05` = 2026-10-05. "Main client" = `CloneAdminClient()` with `UseBranch(client, ids.MainBranchId)`. A request written as `sales/summary?…` means `GET /api/dashboard/executive/sales/summary?…`.

## Final Verification

```bash
cd /d/Projects/QLBanHang/.claude/worktrees/executive-dashboard
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_dash;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln --logger "trx;LogFileName=dash.trx" --results-directory "$TEMP/qlbh-dash-results"

cd ../frontend
npm run typecheck
npm run lint
npm run test
npm run build
```

Only the failures listed in `BASELINE.md` may remain, compared test by test. The manual checks (two browsers for the live refresh, 1366×768 and 375 px) are in Phase 08.

## Rollback / Recovery

- **Code:** every task is one commit on `feat/executive-dashboard`; `git revert` single commits or delete the branch and the worktree (`git worktree remove .claude/worktrees/executive-dashboard`).
- **Database:** no migration. The seeder inserts the permission row `dashboard.executive` and grants it to ADMIN and MANAGER; reverting the code leaves an unused row, which is harmless (delete it from `permissions` and `role_permissions` by hand if wanted).
- **Behaviour:** the page, its routes and the signal are additions. `InventoryReportService` keeps its results (two private helpers become shared ones). `useNotificationHub` keeps its `NewNotification` handling.
- **Test databases:** `qldonhang_integtest_dash_tpl` and its clones are dropped by the fixture at the end of each run; a crashed run can leave `qldonhang_integtest_dash_*` databases, which are safe to drop.
