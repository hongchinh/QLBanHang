# Phase 04 — Frontend: labels, types and live refresh

**Status:** [x] done
**Complexity:** M

## Objective

Both overview pages show the new DTO fields and labels, name the branch in the subtitle, and refresh live. The listening core of `useExecutiveLive` becomes a shared hook, and the executive dashboard keeps its behavior.

## Files

- Create: `frontend/src/features/reports/revenue/types.ts`, `api.ts`, `keys.ts`, `hooks.ts`
- Modify: `frontend/src/pages/reports/revenue-page.tsx` (+ `revenue-page.test.tsx`, create it if missing)
- Modify: `frontend/src/features/dashboard/components/revenue-area-chart.tsx`
- Modify: `frontend/src/features/dashboard/types.ts`
- Create: `frontend/src/features/dashboard/keys.ts`
- Modify: `frontend/src/features/dashboard/hooks.ts` (use `dashboardKeys`)
- Modify: `frontend/src/features/dashboard/components/sales-leaderboard.tsx`
- Modify: `frontend/src/lib/realtime-bus.ts` (+ `realtime-bus.test.ts`)
- Modify: `frontend/src/hooks/useNotificationHub.ts` (+ `useNotificationHub.test.ts`)
- Create: `frontend/src/hooks/use-dashboard-signal.ts` (+ `use-dashboard-signal.test.tsx`)
- Modify: `frontend/src/features/executive-dashboard/use-executive-live.ts`. The existing `use-executive-live.test.tsx` must pass unchanged.
- Create: `frontend/src/features/dashboard/use-overview-live.ts` (+ `use-overview-live.test.tsx`)
- Create: `frontend/src/features/dashboard/revenue-subtitle.ts` (+ `revenue-subtitle.test.ts`)
- Modify: `frontend/src/pages/dashboard-page.tsx`, `frontend/src/pages/admin/admin-dashboard-page.tsx`
- Create: `frontend/src/features/dashboard/components/sales-leaderboard.test.tsx`

## Target API

```ts
// features/dashboard/keys.ts
export const dashboardKeys = {
  all: ['dashboard'] as const,
  quotationStats: (p?: object) => ['dashboard', 'quotation-stats', p] as const,
  summary: (p: object) => ['dashboard', 'summary', p] as const,
  revenueSeries: (p: object) => ['dashboard', 'revenue-series', p] as const,
  topCustomers: (p: object) => ['dashboard', 'top-customers', p] as const,
  topProducts: (p: object) => ['dashboard', 'top-products', p] as const,
  recentActivity: (p: object) => ['dashboard', 'recent-activity', p] as const,
  leaderboard: (p: object) => ['dashboard', 'leaderboard', p] as const,
};
// Prefixes refetched on a `sales` signal / on QuotationsChanged.
export const REVENUE_PREFIXES = ['summary', 'revenue-series', 'top-customers', 'top-products', 'leaderboard'];
export const QUOTATION_PREFIXES = ['summary', 'recent-activity'];

// lib/realtime-bus.ts (added)
emitQuotationsChanged(): void;
onQuotationsChanged(listener: () => void): () => void;

// hooks/use-dashboard-signal.ts
export interface DashboardSignalHandlers {
  // Domains collected in one 1-second window, for signals of `branchId` (any branch when undefined).
  onDomains: (domains: ReadonlySet<string>) => void;
  // Called at the end of a window that saw at least one QuotationsChanged; omitted = ignore those.
  onQuotations?: () => void;
  // The connection came back after being down: refetch everything.
  onReconnect: () => void;
}
export function useDashboardSignal(branchId: string | undefined, handlers: DashboardSignalHandlers): RealtimeStatus;

// features/dashboard/use-overview-live.ts
export function useOverviewLive(branchId: string | undefined): { status: RealtimeStatus; updatedAt: number | null };

// features/dashboard/revenue-subtitle.ts
export function revenueSubtitle(a: { branchName: string | null; ownOnly: boolean; from?: string; to?: string }): string;
```

`handlers` is read through a ref inside the hook, so a page can pass an inline object without re-subscribing.

## Tasks

### Task 4.0 — Doanh thu report on its own endpoints (D10)

Do this first: Task 4.1 renames the overview types, and the report must no longer depend on them.

1. Write the failing test `revenue-page.test.tsx` (check `frontend/src/pages/reports/` for an existing one first and extend it). Mock `apiGet` and render the page: it requests `/reports/revenue/summary`, `/reports/revenue/series` and `/reports/revenue/top-customers`, and no `/dashboard/*` path. The **Tổng doanh thu** figure (or whichever label the page shows for `rangeRevenue`; read the page) comes from the mocked summary.
2. Run `npm run test -- src/pages/reports/revenue-page.test.tsx`. Expected: FAIL (it calls `/dashboard/*`).
3. Implement:
   - `features/reports/revenue/types.ts`: `QuotationRevenueSummary { from, to, prevFrom, prevTo, rangeRevenue: Kpi, totalCount: Kpi, cancelledCount: Kpi }`, `QuotationRevenuePoint { date, total, confirmedCount }`, `QuotationRevenueSeries { points }`, `QuotationTopCustomer { customerId, customerName, revenue, quotationCount }`. `Kpi` is imported from `features/dashboard/types`.
   - `keys.ts`: `revenueReportKeys = { all: ['reports', 'revenue'], summary(p), series(p), topCustomers(p) }`, like `salesRevenueKeys`. They are outside `['dashboard']`, so the overview live refresh never touches the report.
   - `api.ts` and `hooks.ts`: `useRevenueReportSummary`, `useRevenueReportSeries`, `useRevenueReportTopCustomers`, with the same parameters and `staleTime` as the dashboard hooks they replace.
   - `revenue-page.tsx`: use the new hooks and types. Labels and figures stay as they are.
   - `revenue-area-chart.tsx`: `points: Pick<RevenuePoint, 'date' | 'total'>[]`, and drop the unused `count` from its data. Both the report and the overview pages pass their points as they are.
4. Run the test and `npm run typecheck`. Expected: PASS.
5. Commit: `git commit -m "feat(reports): Doanh thu report reads its own quotation revenue endpoints"`

### Task 4.1 — Types and leaderboard labels

1. Write the failing test `sales-leaderboard.test.tsx`. Rendering one item `{ voucherCount: 3, revenue: 1000, deltaPct: null, … }` shows "3 phiếu" and no text matching `/CR /`.
2. Run `npm run test -- src/features/dashboard/components/sales-leaderboard.test.tsx`. Expected: FAIL (it shows "BG").
3. In `types.ts`:
   - `RevenuePoint.confirmedCount` → `voucherCount`.
   - `TopCustomer.quotationCount` → `voucherCount`.
   - `SalesLeaderboardItem.confirmedCount` → `voucherCount`, and remove `conversionRate`.

   In `sales-leaderboard.tsx`, render `{formatNumber(it.voucherCount)} phiếu` and drop the CR span. Fix every compile error that `npm run typecheck` reports, such as the chart tooltip that reads `confirmedCount`.
4. Run the test and `npm run typecheck`. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): voucher counts in the overview types and leaderboard"`

### Task 4.2 — Realtime bus and hub: QuotationsChanged

1. Write failing tests:
   - In `realtime-bus.test.ts`: `emitQuotationsChanged` calls the listeners, and unsubscribing stops them.
   - In `useNotificationHub.test.ts`: the `QuotationsChanged` handler emits on the bus and calls `invalidateQueries({ queryKey: ['dashboard'], refetchType: 'none' })`. The `DashboardChanged` handler now also invalidates `['dashboard']` with `refetchType: 'none'`.
2. Run `npm run test -- src/lib/realtime-bus.test.ts src/hooks/useNotificationHub.test.ts`. Expected: FAIL.
3. Implement the two bus methods (a separate listener set) and `connection.on('QuotationsChanged', …)` in `useNotificationHub.ts`, plus the extra invalidate in the `DashboardChanged` handler.
4. Run. Expected: PASS. The known baseline failure "invalidates unread-count" may stay as it was before.
5. Commit: `git commit -m "feat(realtime): client receives QuotationsChanged"`

### Task 4.3 — Shared useDashboardSignal; executive uses it

1. Write the failing test `use-dashboard-signal.test.tsx` with fake timers. Cases:
   - Two signals for branch A within 1 s produce one `onDomains` call with the union of their domains.
   - A signal for branch B is ignored when `branchId` = A, and accepted when `branchId` is undefined.
   - `QuotationsChanged` alone produces one `onQuotations` call and no `onDomains` call.
   - Status going disconnected and then connected calls `onReconnect` once.
   - Unmounting clears the timer.
2. Run `npm run test -- src/hooks/use-dashboard-signal.test.tsx`. Expected: FAIL (module missing).
3. Create `use-dashboard-signal.ts`. Move into it the window, branch filter and reconnect logic from `use-executive-live.ts` without changing them, and add the quotation listener. Then rewrite `useExecutiveLive` to call it:
   - `onDomains`: invalidate `executiveKeys.domain(d)` for each known domain, then `cross`.
   - `onReconnect`: invalidate `executiveKeys.all`.
   - No `onQuotations`.
   - Keep the `updatedAt` store as it is.
4. Run `npm run test -- src/hooks/use-dashboard-signal.test.tsx src/features/executive-dashboard`. Expected: PASS, with `use-executive-live.test.tsx` unchanged.
5. Commit: `git commit -m "refactor(realtime): shared dashboard signal hook"`

### Task 4.4 — useOverviewLive and dashboardKeys

1. Write the failing test `use-overview-live.test.tsx`. Seed a `QueryClient` with the keys `['dashboard','summary',{}]`, `['dashboard','recent-activity',{}]` and `['dashboard','top-products',{}]`, then spy on `invalidateQueries`. Cases:
   - A `sales` signal for the branch invalidates the 5 revenue prefixes and not recent-activity.
   - `QuotationsChanged` invalidates summary and recent-activity only.
   - A reconnect invalidates `['dashboard']`.
   - `updatedAt` is the newest `dataUpdatedAt` of `['dashboard']` queries.
2. Run `npm run test -- src/features/dashboard/use-overview-live.test.tsx`. Expected: FAIL.
3. Create `keys.ts` and switch `hooks.ts` to `dashboardKeys.*` (same key values). Create `use-overview-live.ts`: `useDashboardSignal(branchId, { onDomains: d => d.has('sales') && invalidate REVENUE_PREFIXES, onQuotations: invalidate QUOTATION_PREFIXES, onReconnect: invalidate dashboardKeys.all })`, plus an `updatedAt` store built the same way as `useExecutiveLive`'s, over `dashboardKeys.all`.
4. Run. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): live refresh of the overview dashboards"`

### Task 4.5 — Subtitle and page wiring

1. Write the failing test `revenue-subtitle.test.ts`:
   - `revenueSubtitle({ branchName: 'Hà Nội', ownOnly: false, from: '2026-10-01', to: '2026-10-31' })` → `'Doanh thu xuất bán của chi nhánh Hà Nội, từ 01/10/2026 đến 31/10/2026.'`
   - With `ownOnly: true` → `'Doanh thu xuất bán (phiếu của tôi) của chi nhánh Hà Nội, từ 01/10/2026 đến 31/10/2026.'`
   - Without dates → `'Doanh thu xuất bán của chi nhánh Hà Nội.'`
   - With `branchName: null` → the same text without `của chi nhánh …`.

   Use `format(parseISO(d), 'dd/MM/yyyy')` from date-fns.
2. Run `npm run test -- src/features/dashboard/revenue-subtitle.test.ts`. Expected: FAIL.
3. Implement `revenue-subtitle.ts`. Wire the pages:
   - Both pages: `const { workingBranch } = useBranchContext(); const live = useOverviewLive(workingBranch?.id);`. Render `<LiveStatus status={live.status} updatedAt={live.updatedAt} />` beside the `RangePicker`. Replace the `<p>` subtitle with `revenueSubtitle({ branchName: workingBranch?.name ?? null, ownOnly, from: summary.data?.from, to: summary.data?.to })`.
   - `dashboard-page.tsx`: `ownOnly = !useAuthStore(s => s.hasPermission)('quotations.view_all')`. Top khách hàng secondary text: `` `${formatNumber(c.voucherCount)} phiếu` ``.
   - `admin-dashboard-page.tsx`: `ownOnly = false`, and the same secondary text.
4. Run `npm run test -- src/features/dashboard src/pages` and `npm run typecheck` and `npm run lint`. Expected: PASS.
5. Commit: `git commit -m "feat(dashboard): branch subtitle and live status on the overview pages"`

## Verification

- `npm run typecheck`, `npm run lint`
- `npm run test -- --testTimeout=30000 --maxWorkers=3`. Only the known baseline failures from the plan SUMMARY are allowed.

## Exit Criteria

- `grep -rn "quotationCount\|conversionRate" frontend/src/features/dashboard frontend/src/pages/dashboard-page.tsx frontend/src/pages/admin/admin-dashboard-page.tsx` prints nothing. In `features/dashboard`, `confirmedCount` stays only on `QuotationStats`, the legacy quotation-stats endpoint; the report keeps `confirmedCount` and `quotationCount` in `features/reports/revenue`. `grep -rn "features/dashboard/hooks" frontend/src/pages/reports` prints nothing.
- `/reports/revenue` calls only `/reports/revenue/*` and shows the same labels.
- The executive live tests pass unchanged, and both overview pages refresh on `sales` and `QuotationsChanged` signals.
