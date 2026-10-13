# Phase 05 — Frontend foundation: route, presets, data layer, live refresh, tile kit

**Status:** [ ] pending
**Complexity:** L

## Objective

Everything the page needs except the page itself: the permission code, the route and sidebar item, the range presets, the typed data layer for the 13 routes, the live refresh over SignalR, the URL state, and the shared tiles. Phases 06 and 07 only compose these.

## Files

- `frontend/src/lib/permissions.ts` (modify — `'dashboard.executive'`)
- `frontend/src/lib/route-permissions.ts`, `route-permissions.test.ts` (modify)
- `frontend/src/components/layout/nav-config.ts`, `nav-config.test.ts` (modify)
- `frontend/src/components/layout/sidebar/sidebar.test.tsx`, `sidebar-rail.test.tsx`, `nav-search.test.ts` (modify — they assert the report sections and the item count of the real config)
- `frontend/src/App.tsx` (modify — one route)
- `frontend/src/pages/executive-dashboard/executive-dashboard-page.tsx`, `executive-dashboard-page.test.tsx` (new — placeholder page)
- `frontend/src/lib/date-range-presets.ts` (modify), `date-range-presets.test.ts` (new)
- `frontend/src/pages/quotations/components/quotation-date-filter.tsx` (modify — one call)
- `frontend/src/features/dashboard/use-dashboard-params.ts`, `components/range-picker.tsx` (modify — use `lib/date-range-presets.ts`; `presets` prop)
- `frontend/src/features/dashboard/components/range-picker.test.tsx` (new)
- `frontend/src/features/dashboard/format.ts` (modify), `format.test.ts` (new)
- `frontend/src/features/dashboard/components/tile-card.tsx`, `stat-tile.tsx`, `live-status.tsx` and their `.test.tsx` files (new)
- `frontend/src/hooks/useCanOpenLink.ts`, `useCanOpenLink.test.ts` (new)
- `frontend/src/features/executive-dashboard/types.ts`, `api.ts`, `keys.ts`, `hooks.ts`, `hooks.test.tsx`, `presets.ts` (new)
- `frontend/src/features/executive-dashboard/use-executive-live.ts`, `use-executive-live.test.tsx` (new)
- `frontend/src/features/executive-dashboard/use-executive-params.ts`, `use-executive-params.test.tsx` (new)
- `frontend/src/features/executive-dashboard/use-executive-scope.ts`, `use-executive-scope.test.tsx` (new)
- `frontend/src/features/executive-dashboard/use-executive-access.ts`, `use-executive-access.test.tsx` (new)
- `frontend/src/features/branches/use-branch-context.ts` (modify — one comment)
- `frontend/src/lib/realtime-bus.ts`, `realtime-bus.test.ts` (new)
- `frontend/src/hooks/useNotificationHub.ts`, `useNotificationHub.test.ts` (modify)

## Reference files (read-only)

- `frontend/src/features/dashboard/` — `format.ts`, `use-dashboard-params.ts`, `components/range-picker.tsx`, `components/kpi-card.tsx` and its test (test style)
- `frontend/src/features/cash-reports/` — `api.ts`, `keys.ts`, `hooks.ts`, `hooks.test.tsx` (data-layer and hook-test style), `types.ts` (`AgingBucket`)
- `frontend/src/features/cash-common/types.ts` — `FundKind`, `CashVoucherType`, `DebtSourceType`
- `frontend/src/lib/api-client.ts` (`apiGet`, `getErrorMessage`), `frontend/src/lib/use-search-param-state.ts` (`useSearchParamString`), `frontend/src/hooks/useCanAccessRoute.ts`
- `frontend/src/features/branches/hooks.ts` (`useMyBranches`), `types.ts` (`Branch`, `MyBranches`), `use-switch-working-branch.ts`, `frontend/src/stores/branch-store.ts`, `frontend/src/stores/auth-store.ts`
- `frontend/src/components/layout/app-layout.tsx` — the outlet is keyed by the working branch (line 67), the hub hook is mounted here (line 26)
- `frontend/src/components/layout/header/header-branch-switcher.tsx` — the other caller of `useSwitchWorkingBranch`
- `frontend/src/pages/cash-reports/cash-book-page.test.tsx` — page test style (mocked hooks, `useAuthStore.setState`, `MemoryRouter`, asserting `enabled`)
- Backend DTOs of Phases 01–03 (`backend/src/OrderMgmt.Application/Dashboards/Executive/Models/*.cs`) — the source of `types.ts`

## Contracts

```ts
// lib/date-range-presets.ts (extended)
export type RangePreset = 'today' | '7d' | '30d' | 'this-week' | 'this-month' | 'last-month' | 'this-year';
export interface PresetOption { key: RangePreset; label: string }
export function computePreset(preset: RangePreset, today?: Date): { from: string; to: string };
export function matchActivePreset(from: string, to: string, presets: readonly RangePreset[], today?: Date): RangePreset | null;

// features/dashboard/components/range-picker.tsx
export const DEFAULT_PRESETS: PresetOption[];     // Hôm nay, 7N, 30N, Tháng này, Tháng trước (the picker's current chips)
// RangePicker gets the optional prop `presets?: PresetOption[]` (default DEFAULT_PRESETS)

// features/executive-dashboard/presets.ts
export const EXECUTIVE_PRESETS: PresetOption[];   // Hôm nay, Tuần này, Tháng này, Năm nay

// features/dashboard/format.ts — additions
export function formatVndCompact(value: number): string;      // 2.840.000.000 → "2,84 tỷ"; 412.000.000 → "412 tr"; 45.500.000 → "45,5 tr"; 950.000 → "950.000"
export function formatSignedCompact(value: number): string;   // "+186 tr", "-700.000", "0"
export function formatPct(value: number | null | undefined, digits = 1): string;   // 14.5 → "14,5%"; 100 → "100%"; null → "—"

// features/executive-dashboard/types.ts
export type ExecutiveDomain = 'sales' | 'cash' | 'debt' | 'inventory';
export type ExecutiveTab = 'overview' | ExecutiveDomain;
export type ExecutiveScopeMode = 'company' | 'branch';
export type SeriesGranularity = 'Day' | 'Week' | 'Month';
export type SalesTopBy = 'Customer' | 'Product' | 'Employee';
export type SalesTopSort = 'Revenue' | 'GrossProfit';
export type RecentDocumentKind = 'Receipt' | 'Payment' | 'StockIn' | 'StockOut';
export interface ExecutiveParams { from: string; to: string; branchId?: string }
export interface BranchParam { branchId?: string }
// plus one interface per DTO of Phases 01–03, same names without the "Dto" suffix, properties in camelCase:
// SalesSummary, SalesSeries, SalesPoint, SalesTopRow, CashSummary, FundBalance, CashSeries, CashPoint, CashBreakdown,
// CashReasonTotal, CashDocument, DebtSummary, DebtSideSummary, DebtBreakdown, DebtPartnerRow, CreditLimitBreach,
// DebtEmployeeRow, PayableDueRow, InventorySummary, InventoryBreakdown, StockGroup, NegativeStockRow, StockProductRow,
// BranchComparisonRow, Attention, AttentionItem, RecentDocument.
// decimal → number; nullable → `| null`; DateOnly and DateTimeOffset → string; enums → their names (the API serializes enums by name).
// Reuse, do not redeclare: `AgingBucket` from '@/features/cash-reports/types';
// `FundKind`, `CashVoucherType`, `DebtSourceType` from '@/features/cash-common/types'.

// features/executive-dashboard/keys.ts
export const executiveKeys = {
  all: ['executive'] as const,
  domain: (d: ExecutiveDomain | 'cross') => [...executiveKeys.all, d] as const,
  salesSummary: (p: ExecutiveParams) => [...executiveKeys.domain('sales'), 'summary', p] as const,
  salesSeries: (p: ExecutiveParams & { granularity: SeriesGranularity }) => [...executiveKeys.domain('sales'), 'series', p] as const,
  salesTop: (p: ExecutiveParams & { by: SalesTopBy; sort?: SalesTopSort; limit?: number }) => [...executiveKeys.domain('sales'), 'top', p] as const,
  cashSummary: (p: ExecutiveParams) => [...executiveKeys.domain('cash'), 'summary', p] as const,
  cashSeries: (p: ExecutiveParams & { granularity: SeriesGranularity }) => [...executiveKeys.domain('cash'), 'series', p] as const,
  cashBreakdown: (p: ExecutiveParams) => [...executiveKeys.domain('cash'), 'breakdown', p] as const,
  debtSummary: (p: BranchParam) => [...executiveKeys.domain('debt'), 'summary', p] as const,
  debtBreakdown: (p: BranchParam) => [...executiveKeys.domain('debt'), 'breakdown', p] as const,
  inventorySummary: (p: ExecutiveParams) => [...executiveKeys.domain('inventory'), 'summary', p] as const,
  inventoryBreakdown: (p: BranchParam) => [...executiveKeys.domain('inventory'), 'breakdown', p] as const,
  branches: (p: { from: string; to: string }) => [...executiveKeys.domain('cross'), 'branches', p] as const,
  attention: (p: BranchParam) => [...executiveKeys.domain('cross'), 'attention', p] as const,
  recent: (p: BranchParam & { limit?: number }) => [...executiveKeys.domain('cross'), 'recent', p] as const,
};

// features/executive-dashboard/api.ts — `executiveApi`, one function per key, same parameter types,
// each `apiGet<T>('/dashboard/executive/<route>', params)` (routes in SUMMARY.md). No function sends `at`.

// features/executive-dashboard/hooks.ts — one hook per function:
// useSalesSummary, useSalesSeries, useSalesTop, useCashSummary, useCashSeries, useCashBreakdown, useDebtSummary,
// useDebtBreakdown, useInventorySummary, useInventoryBreakdown, useBranchComparison, useAttention, useRecentDocuments.
// Signature: (params, options: { enabled?: boolean } = {}). Every hook spreads:
export const executiveQueryOptions = { staleTime: 30_000, refetchInterval: 60_000, refetchIntervalInBackground: false } as const;

// lib/realtime-bus.ts
export type RealtimeStatus = 'connecting' | 'connected' | 'disconnected';
export interface DashboardChangedSignal { branchId: string; domains: string[] }
export const realtimeBus: {
  getStatus(): RealtimeStatus;                                   // starts as 'connecting'
  setStatus(status: RealtimeStatus): void;                       // notifies only on a change
  onStatus(listener: () => void): () => void;                    // returns unsubscribe
  emitDashboardChanged(signal: DashboardChangedSignal): void;
  onDashboardChanged(listener: (signal: DashboardChangedSignal) => void): () => void;
};

// features/executive-dashboard/use-executive-live.ts
export function useExecutiveLive(branchId: string | undefined): { status: RealtimeStatus; updatedAt: number | null };

// features/executive-dashboard/use-executive-params.ts
export function useExecutiveParams(): {
  from: string; to: string; tab: ExecutiveTab; scopeParam: ExecutiveScopeMode;
  setRange(from: string, to: string): void;
  setPreset(preset: RangePreset): void;
  setTab(tab: ExecutiveTab): void;
  setScopeParam(scope: ExecutiveScopeMode): void;
};

// features/executive-dashboard/use-executive-scope.ts
export function useExecutiveScope(): {
  canSwitch: boolean;                       // myBranches.canSwitch (the user has branches.access_all)
  mode: ExecutiveScopeMode;                 // always 'branch' when !canSwitch
  branchId: string | undefined;             // what the API receives: the working branch id in 'branch' mode for canSwitch users, else undefined
  workingBranch: Branch | undefined;
  branches: Branch[];
  selectCompany(): void;
  selectBranch(branchId: string): void;
};

// features/executive-dashboard/use-executive-access.ts
export function useExecutiveAccess(): Record<ExecutiveDomain, boolean> & { tabs: ExecutiveTab[] };
// sales: reports.profit · cash: reports.cash · debt: reports.debt · inventory: reports.inventory; tabs = ['overview', …allowed domains in that order]

// hooks/useCanOpenLink.ts
/// Like useCanAccessRoute, for a link that may carry a query string: only the path part is checked.
export function useCanOpenLink(): (to: string) => boolean;

// features/dashboard/components
export interface TileQuery { isLoading: boolean; isError: boolean; error?: unknown; data?: unknown; refetch: () => unknown }
export function TileCard(props: {
  title: string;
  action?: { label: string; to?: string; onClick?: () => void; title?: string };   // a link (`to`) or a button (`onClick`)
  query?: TileQuery;
  isEmpty?: boolean;
  emptyText?: string;
  className?: string;
  children: React.ReactNode;
}): JSX.Element;
export function StatTile(props: {
  label: string;
  value: string | null;                     // null → "—"
  delta?: number | null;                    // undefined: no badge; null: "—"
  hint?: React.ReactNode;
  hintTone?: 'default' | 'good' | 'warn' | 'bad';
  badge?: string;                           // e.g. "tạm tính"
  to?: string;
  title?: string;                           // tooltip of the link
  loading?: boolean;
  error?: boolean;                          // the tile's query failed and has no data
  onRetry?: () => void;
}): JSX.Element;
export function LiveStatus(props: { status: RealtimeStatus; updatedAt: number | null }): JSX.Element;
```

## Rules

- **Imports.** Type-only imports use `import type` (the lint rule `consistent-type-imports` warns otherwise). `noUnusedParameters` is on: do not declare a parameter or destructure a prop that is not used yet.
- **Presets.** Weeks start on Monday. `this-week` = Monday of the current week … today; `this-year` = 1 January … today; `today` = today … today; the four ranges the module already has do not change. `matchActivePreset` returns the first key of the given list whose range equals `from`/`to` (so on a Monday "Hôm nay" wins over "Tuần này", which is the same range that day).
- **One preset module.** `lib/date-range-presets.ts` is the only implementation. `quotation-date-filter.tsx` passes its own four keys to `matchActivePreset`. `use-dashboard-params.ts` and `range-picker.tsx` drop their private copies and call it; `use-dashboard-params.ts` re-exports the `RangePreset` type so existing imports keep working. `RangePicker`'s chips, labels and "Tuỳ chỉnh" button stay as they are when `presets` is not passed.
- **Compact money.** Decide the unit after rounding: millions rounded to 1 decimal; when that is below 1,000 and `|v| ≥ 1e6`, print it with at most 1 decimal + " tr"; when `|v|` rounds to 1,000 tr or more, print billions with exactly 2 decimals + " tỷ"; below 1e6 print `formatNumber(v)`. Decimal comma (vi-VN). Every sign printed by code is the ASCII hyphen-minus `-` (what `Intl` prints) or `+`.
- **Sidebar item:** `{ to: '/dashboard/executive', label: 'Điều hành', icon: Presentation, section: 'Tổng hợp', permission: 'dashboard.executive' }`, first item of the `reports` group.
- **Route:** `dashboard/executive` inside the layout route, next to `admin/dashboard`: `<ProtectedRoute permission="dashboard.executive"><ExecutiveDashboardPage /></ProtectedRoute>`. Route rule: `{ pattern: /^\/dashboard\/executive$/, permission: 'dashboard.executive' }`.
- **Never** add a dashboard path to `lib/sw-routes.ts` (branch- and permission-scoped data).
- **Hub hook.** `useNotificationHub` keeps its `NewNotification` handler unchanged and adds:
  - `connection.on('DashboardChanged', …)`: forward the signal with `realtimeBus.emitDashboardChanged(signal)` and call `queryClient.invalidateQueries({ queryKey: executiveKeys.all, refetchType: 'none' })`, so a dashboard that is not open refetches when it is opened again;
  - status: `'connecting'` before `start()`, `'connected'` when `start()` resolves and on `onreconnected`, `'disconnected'` when `start()` rejects, on `onreconnecting`, on `onclose` and on cleanup;
  - reconnecting never gives up: `withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => Math.min(30_000, 2_000 * (ctx.previousRetryCount + 1)) })`, and a rejected `start()` is tried again every 5 seconds;
  - one `active` flag, cleared in the cleanup, guards every status write and every retry, so a late callback of a replaced connection changes nothing.
- **Live refresh (`useExecutiveLive`).**
  - A signal is ignored when `branchId` is set and differs from `signal.branchId`. Without `branchId` (company scope, or a user who cannot switch) every signal counts.
  - Domains of the signals of one second are collected; then one `invalidateQueries({ queryKey: executiveKeys.domain(d) })` per collected name that is an `ExecutiveDomain`, plus one for `executiveKeys.domain('cross')`.
  - When the status goes from `'disconnected'` to `'connected'`, invalidate `executiveKeys.all`.
  - `updatedAt` = the largest `dataUpdatedAt` of the cached queries under `executiveKeys.all`, or `null` when there is none.
  - The timer and the subscriptions are released on unmount.
- **URL state (`useExecutiveParams`).** `from`, `to` and the presets come from `useDashboardParams()`. `tab` is the search param `tab` (anything else than the five values is `'overview'`; `'overview'` is not written to the URL). `scopeParam` is the search param `scope`: `'branch'` or, when absent, `'company'`.
- **Scope (`useExecutiveScope`).**
  - `canSwitch` false: `mode = 'branch'`, `branchId = undefined` (the server forces the user's branch); `selectBranch` and `selectCompany` do nothing.
  - `canSwitch` true: `mode = scopeParam`; `branchId` = the working branch id in `'branch'` mode, `undefined` in `'company'` mode.
  - `selectBranch(id)`: first `setScopeParam('branch')`, then, when `id` is not the working branch, `void switchWorkingBranch(id)` in the same synchronous tick. Never await the switch: it remounts the page (the outlet is keyed by the working branch), and an awaited continuation would run in the unmounted instance after the new one already fetched in company scope.
  - `selectCompany()`: `setScopeParam('company')`; the working branch does not change.
  - The branch store is never written directly; update the comment in `use-branch-context.ts` ("only the switcher and logout change it") to name the executive dashboard's branch select as well.
- **`TileCard`.** `query.isLoading` → a pulse skeleton with `aria-busy="true"`; `query.isError` **and** `query.data === undefined` → the message (`getErrorMessage(query.error)`, or "Không tải được dữ liệu." without an error object) and a "Thử lại" button that calls `refetch`; a failed background refetch keeps the last data on screen; `isEmpty` → `emptyText ?? 'Chưa có dữ liệu.'`; otherwise the children. The action: with `to`, a `<Link>` rendered only when `useCanOpenLink()` accepts it; with `onClick`, a button.
- **`StatTile`.** Shows the label (and the badge next to it), the value in `tabular-nums`, the delta badge when `delta !== undefined` (`▲` or `▼` followed by `formatPct(|delta|)`, e.g. "▲ 12,5%"; `null` → "—"; emerald for a rise, rose for a fall), and the hint in the tone's colour (`default` muted, `good` emerald, `warn` amber, `bad` rose). With `to` and `useCanOpenLink()` accepting it, the whole tile is a `<Link>` carrying `title`. `loading` shows a skeleton in place of the value. `error` shows "—" and a "Thử lại" button (calling `onRetry`) in place of the hint.
- **`LiveStatus`** (`role="status"`): connected → "Trực tiếp · cập nhật HH:mm:ss"; connecting → "Đang kết nối…"; disconnected → "Mất kết nối · số liệu lúc HH:mm"; without `updatedAt` the time part is left out.
- Colours come from Tailwind classes already used in the feature (`text-emerald-700`, `text-rose-700`, `text-amber-700`, `text-muted-foreground`); no new CSS file.
- **Staging.** Each task lists its files; stage exactly those (`git status --short` must show nothing else from the task).

## Tasks

Frontend commands run from `frontend/` in the worktree.

### Task 5.1 — Install dependencies

1. `npm ci` (the worktree has no `node_modules`).
2. `npm run typecheck`. Expected: exit 0.
3. `npx vitest run src/features/dashboard src/components/layout`. Expected: PASS.
4. No commit (nothing changes). If `npm ci` modifies `package-lock.json`, restore it with `git checkout -- frontend/package-lock.json` and report.

### Task 5.2 — Permission code, route rule, sidebar item, route

Files: `lib/permissions.ts`, `lib/route-permissions.ts`, `lib/route-permissions.test.ts`, `components/layout/nav-config.ts`, `components/layout/nav-config.test.ts`, `components/layout/sidebar/sidebar.test.tsx`, `components/layout/sidebar/sidebar-rail.test.tsx`, `components/layout/sidebar/nav-search.test.ts`, `App.tsx`, `pages/executive-dashboard/executive-dashboard-page.tsx`, `pages/executive-dashboard/executive-dashboard-page.test.tsx`.

1. Write the failing tests:
   - `route-permissions.test.ts`: `expectRule('/dashboard/executive', 'dashboard.executive')` (the helper is local to a `describe`; add the case inside one that has it).
   - `nav-config.test.ts`: the reports group of a user with every permission starts with `/dashboard/executive`; `paths(['dashboard.executive'], 'reports')` equals `['/dashboard/executive']`; `paths(ACCOUNTANT_CASH_PERMS, 'reports')` does not contain it; the "report sections for admin" expectation gets "Tổng hợp" first.
   - `sidebar.test.tsx` and `sidebar-rail.test.tsx`: the section lists become `['Tổng hợp', 'Doanh thu', 'Kho', 'Quỹ', 'Công nợ']`.
   - `nav-search.test.ts`: the count that is 14 becomes 15.
   - `executive-dashboard-page.test.tsx`: the placeholder page renders a heading "Điều hành".
2. Run `npx vitest run src/lib/route-permissions.test.ts src/components/layout src/pages/executive-dashboard`. Expected: FAIL — the rule and the item do not exist; the page module is missing.
3. Implement: the permission code in `permissions.ts` (after `'reports.cash'`), the rule in `route-permissions.ts`, the nav item, the placeholder page and the route in `App.tsx`.
4. Run the same command and `npm run typecheck`. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard route and sidebar item"`.

### Task 5.3 — Range presets

Files: `lib/date-range-presets.ts`, `lib/date-range-presets.test.ts`, `pages/quotations/components/quotation-date-filter.tsx`, `features/dashboard/use-dashboard-params.ts`, `features/dashboard/components/range-picker.tsx`, `features/dashboard/components/range-picker.test.tsx`, `features/executive-dashboard/presets.ts`.

1. Write the failing tests:
   - `date-range-presets.test.ts` with `today = new Date(2026, 9, 13)` (a Tuesday): `today` → `2026-10-13 … 2026-10-13`; `this-week` → `2026-10-12 … 2026-10-13`; `this-week` on Sunday 2026-10-18 → `2026-10-12 … 2026-10-18`; `this-week` on Monday 2026-10-12 → one day; `this-month` → `2026-10-01 … 2026-10-13`; `this-year` → `2026-01-01 … 2026-10-13`; `last-month` → `2026-09-01 … 2026-09-30`; `7d` → `2026-10-07 … 2026-10-13`; `30d` → `2026-09-14 … 2026-10-13`; `matchActivePreset('2026-10-12', '2026-10-13', ['today', 'this-week', 'this-month', 'this-year'], today) === 'this-week'`; the same range with `['7d', '30d', 'this-month', 'last-month']` → `null`; on Monday 2026-10-12 the range `2026-10-12 … 2026-10-12` with the first list → `'today'`.
   - `range-picker.test.tsx`: without `presets` the chips are "Hôm nay", "7N", "30N", "Tháng này", "Tháng trước"; with `presets={EXECUTIVE_PRESETS}` they are "Hôm nay", "Tuần này", "Tháng này", "Năm nay"; clicking "Năm nay" calls `onPreset('this-year')`.
2. Run `npx vitest run src/lib/date-range-presets.test.ts src/features/dashboard`. Expected: FAIL — `computePreset` takes no date and knows no `this-week`; `EXECUTIVE_PRESETS` does not exist.
3. Implement the rules above (extend the module, the three callers, `presets.ts`).
4. Run the same command plus `npx vitest run src/pages/quotations` and `npm run typecheck`. Expected: PASS (the existing `use-dashboard-params.test.tsx` and the quotation tests included).
5. Commit: `git commit -m "feat(frontend): week and year range presets"`.

### Task 5.4 — Compact formats

Files: `features/dashboard/format.ts`, `features/dashboard/format.test.ts`.

1. Write the failing test `format.test.ts`: `formatVndCompact(2_840_000_000) === '2,84 tỷ'`; `(1_260_000_000) → '1,26 tỷ'`; `(3_100_000_000) → '3,10 tỷ'`; `(412_000_000) → '412 tr'`; `(45_500_000) → '45,5 tr'`; `(999_960_000) → '1,00 tỷ'`; `(950_000) → '950.000'`; `(0) → '0'`; `(-186_000_000) → '-186 tr'`; `formatSignedCompact(186_000_000) === '+186 tr'`; `(-700_000) → '-700.000'`; `(0) → '0'`; `formatPct(14.5) === '14,5%'`; `formatPct(100) === '100%'`; `formatPct(33.33) === '33,3%'`; `formatPct(null) === '—'`.
2. Run `npx vitest run src/features/dashboard/format.test.ts`. Expected: FAIL — `formatVndCompact` is not exported.
3. Implement the three functions in `format.ts`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): compact money and percent formats"`.

### Task 5.5 — Types, api, keys, hooks

Files: `features/executive-dashboard/types.ts`, `api.ts`, `keys.ts`, `hooks.ts`, `hooks.test.tsx`.

1. Write the failing test `hooks.test.tsx` (style of `features/cash-reports/hooks.test.tsx`, `executiveApi` mocked):
   - `useSalesSummary({ from, to })` calls `executiveApi.salesSummary` with the params and exposes the data;
   - `useDebtSummary({}, { enabled: false })` does not call the api;
   - every key factory result starts with `['executive', <its domain>]`, and `branches`, `attention`, `recent` start with `['executive', 'cross']`;
   - `executiveQueryOptions.refetchInterval === 60_000` and `refetchIntervalInBackground === false`.
2. Run `npx vitest run src/features/executive-dashboard/hooks.test.tsx`. Expected: FAIL — the modules do not exist.
3. Implement `types.ts` (from the backend DTO files), `api.ts`, `keys.ts`, `hooks.ts`.
4. Run the same command and `npm run typecheck`. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard data layer"`.

### Task 5.6 — Realtime bus and hub hook

Files: `lib/realtime-bus.ts`, `lib/realtime-bus.test.ts`, `hooks/useNotificationHub.ts`, `hooks/useNotificationHub.test.ts`.

1. Write the failing tests:
   - `realtime-bus.test.ts`: the status starts as `'connecting'`; `setStatus('connected')` notifies status listeners once and a second `setStatus('connected')` does not; an unsubscribed listener is not called; `emitDashboardChanged` reaches every signal listener with the signal. Reset the bus status in `beforeEach`.
   - `useNotificationHub.test.ts`: add `onreconnecting`, `onreconnected`, `onclose` (each `vi.fn()`) to the mocked connection object, then add the cases:
     - registers a `DashboardChanged` handler that forwards the signal to `realtimeBus` and calls `invalidateQueries({ queryKey: ['executive'], refetchType: 'none' })`;
     - the status is `'connected'` after `start()` resolves;
     - calling the `onreconnecting` callback sets `'disconnected'` and the `onreconnected` callback sets `'connected'`;
     - unmount sets `'disconnected'`, and calling the captured `onreconnected` callback afterwards does not change it;
     - the object passed to `withAutomaticReconnect` has `nextRetryDelayInMilliseconds`, which returns a number of at most 30,000 for `previousRetryCount` 0 and 100;
     - with `start` rejecting once (`vi.useFakeTimers()`): the status is `'disconnected'` and `start` is called again after 5,000 ms.
     Do not change the four existing cases (one of them is a known baseline failure).
2. Run `npx vitest run src/lib/realtime-bus.test.ts src/hooks/useNotificationHub.test.ts`. Expected: FAIL — `realtime-bus` does not exist.
3. Implement `realtime-bus.ts` and the additions to `useNotificationHub.ts` (keep the file's style: no semicolons).
4. Run the same command. Expected: all pass except the baseline case "invalidates unread-count query on NewNotification".
5. Commit: `git commit -m "feat(realtime): dashboard signal on the notification hub"`.

### Task 5.7 — `useExecutiveLive`

Files: `features/executive-dashboard/use-executive-live.ts`, `use-executive-live.test.tsx`.

1. Write the failing test (a real `QueryClient` in a wrapper, `vi.useFakeTimers()`, a spy on `queryClient.invalidateQueries`):
   - two signals within a second (`['cash']`, then `['cash', 'debt']`) → nothing before 1,000 ms; after it exactly three calls: `['executive', 'cash']`, `['executive', 'debt']`, `['executive', 'cross']`;
   - with `branchId = 'b1'`, a signal for `'b2'` causes no call and a signal for `'b1'` does;
   - without `branchId`, a signal of any branch counts;
   - an unknown domain name is skipped (only `cross` is invalidated);
   - status `'disconnected'` then `'connected'` → one call with `['executive']`; the first `'connecting'` → `'connected'` causes none;
   - `status` follows the bus; `updatedAt` is `null` with an empty cache and equals the `dataUpdatedAt` of a query seeded with `queryClient.setQueryData(executiveKeys.attention({}), …)`;
   - after unmount a pending timer does not fire.
2. Run `npx vitest run src/features/executive-dashboard/use-executive-live.test.tsx`. Expected: FAIL — the module does not exist.
3. Implement `use-executive-live.ts` (`useSyncExternalStore` for the status and for the cache time).
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): live refresh of the executive dashboard"`.

### Task 5.8 — URL state, scope and access hooks

Files: `features/executive-dashboard/use-executive-params.ts`, `use-executive-params.test.tsx`, `use-executive-scope.ts`, `use-executive-scope.test.tsx`, `use-executive-access.ts`, `use-executive-access.test.tsx`, `features/branches/use-branch-context.ts`.

1. Write the failing tests:
   - `use-executive-params.test.tsx` (`MemoryRouter`): defaults are the current month, `tab 'overview'`, `scopeParam 'company'`; `/?tab=cash&scope=branch` is read; `/?tab=nope` gives `'overview'`; `setTab('debt')` writes `tab=debt` and `setTab('overview')` removes it; `setScopeParam('branch')` writes `scope=branch` and `'company'` removes it; `setPreset('this-year')` writes `from` = 1 January.
   - `use-executive-scope.test.tsx` (`useMyBranches`, the branch store and `useSwitchWorkingBranch` mocked): `canSwitch` false → `mode 'branch'`, `branchId undefined`, and `selectBranch` neither writes the URL nor calls the switch; `canSwitch` true and no `scope` → `mode 'company'`, `branchId undefined`; `scope=branch` → `branchId` = the working branch id; `selectBranch(other)` calls the switch with `other` and writes `scope=branch`; with a switch mock that never resolves, `scope=branch` is still written; `selectBranch(working)` does not call the switch; `selectCompany()` removes `scope`.
   - `use-executive-access.test.tsx` (`useAuthStore.setState` with permissions): `reports.profit` and `reports.debt` only → `sales` and `debt` true, `cash` and `inventory` false, `tabs` = `['overview', 'sales', 'debt']`.
2. Run `npx vitest run src/features/executive-dashboard`. Expected: FAIL — the three modules do not exist.
3. Implement the three hooks and the comment change.
4. Run the same command and `npm run typecheck`. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard URL state, scope and access"`.

### Task 5.9 — Tile kit

Files: `hooks/useCanOpenLink.ts`, `hooks/useCanOpenLink.test.ts`, `features/dashboard/components/tile-card.tsx`, `tile-card.test.tsx`, `stat-tile.tsx`, `stat-tile.test.tsx`, `live-status.tsx`, `live-status.test.tsx`.

1. Write the failing tests:
   - `useCanOpenLink.test.ts`: with only `reports.cash`, `'/reports/cash-book'` is allowed and `'/reports/debt-overdue?side=Receivable&at=2026-10-13'` is not; with `reports.debt` the second one is.
   - `tile-card.test.tsx`: shows the title and children; `query.isLoading` → an `aria-busy` skeleton and no children; `isError` without data → the error's message (or "Không tải được dữ liệu.") and clicking "Thử lại" calls `refetch`; `isError` with data → the children stay and there is no "Thử lại"; `isEmpty` → "Chưa có dữ liệu." or the given text; an action with `to` renders a link when the route is allowed (`useAuthStore.setState` with the permission) and nothing when it is not, also when `to` has a query string; an action with `onClick` renders a button that calls it.
   - `stat-tile.test.tsx`: value and label; `value={null}` → "—"; `delta={12.5}` → "▲ 12,5%"; `delta={-3.2}` → "▼ 3,2%"; no `delta` prop → no badge; `badge="tạm tính"` is shown; `hintTone="bad"` gives the hint a rose class and `"warn"` an amber one; `to` renders a link with the `title` when allowed and no link when not (use a `to` with a query string); `loading` hides the value; `error` shows "—" and "Thử lại", and clicking it calls `onRetry`.
   - `live-status.test.tsx`: connected with a time → text starting "Trực tiếp · cập nhật"; disconnected → "Mất kết nối"; connecting → "Đang kết nối…"; the element has `role="status"`.
2. Run `npx vitest run src/hooks/useCanOpenLink.test.ts src/features/dashboard/components`. Expected: FAIL — the four modules do not exist.
3. Implement the hook and the three components.
4. Run the same command, `npm run typecheck` and `npm run lint`. Expected: PASS; lint reports no new problem (the baseline is 3 errors and 14 warnings).
5. Commit: `git commit -m "feat(frontend): shared dashboard tiles"`.

## Verification

From `frontend/`:

- `npm run typecheck` — exit 0.
- `npm run lint` — no more than the baseline's 3 errors and 14 warnings, and none in a file this phase created.
- `npm run test` — only the 3 baseline failures.
- `npm run build` — exit 0; then restore the tracked `tsconfig.*.tsbuildinfo` files (see `SUMMARY.md`).

## Exit Criteria

- `/dashboard/executive` opens a placeholder for a user with `dashboard.executive` and `/403` for others; the sidebar shows "Điều hành" for the former.
- The quotation dashboards and the quotation date filter look and behave as before.
- A `DashboardChanged` event on the hub invalidates the matching executive queries after one second, and marks them stale when no dashboard is open.
- Eight commits (Tasks 5.2–5.9).
