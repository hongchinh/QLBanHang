# Phase 06 — Frontend page shell and the Tổng quan tab

**Status:** [ ] pending
**Complexity:** L

## Objective

Replace the placeholder page with the real one: header (title, live status, comparison period, branch scope, period), tab bar, and the Tổng quan tab of the approved mockup ([mockup-02-overview-tab.html](../../brainstorms/261013-2247-executive-dashboard/mockup-02-overview-tab.html)): seven KPI tiles, the revenue–cost–gross profit chart, cash flow with fund balances, the receivable term structure, the branch comparison, top customers, "Cần chú ý" and "Vừa phát sinh".

## Files

- `frontend/src/features/executive-dashboard/links.ts`, `links.test.ts` (new)
- `frontend/src/features/executive-dashboard/period.ts`, `period.test.ts` (new)
- `frontend/src/pages/executive-dashboard/executive-dashboard-page.tsx`, `executive-dashboard-page.test.tsx` (modify — real page)
- `frontend/src/pages/executive-dashboard/components/scope-select.tsx`, `kpi-row.tsx`, `data-table.tsx`, `branch-document-link.tsx`, `granularity-toggle.tsx`, `profit-chart.tsx`, `cash-flow-chart.tsx`, `fund-balance-table.tsx`, `debt-term-bar.tsx`, `branch-comparison-table.tsx`, `attention-list.tsx`, `recent-feed.tsx` and one `.test.tsx` per component (new)
- `frontend/src/pages/executive-dashboard/tabs/overview-tab.tsx`, `overview-tab.test.tsx` (new)

## Reference files (read-only)

- The mockup named above and `docs/brainstorms/261013-2247-executive-dashboard/SUMMARY.md` (section "Page layout")
- Phase 05 of this plan — hooks, keys, `useExecutiveParams`, `useExecutiveScope`, `useExecutiveAccess`, `useExecutiveLive`, `useCanOpenLink`, `TileCard`, `StatTile`, `LiveStatus`, formats, `EXECUTIVE_PRESETS`
- `frontend/src/features/dashboard/components/revenue-area-chart.tsx`, `status-funnel.tsx`, `top-list-card.tsx` — chart literals, CSS bars, list rows
- `frontend/src/pages/reports/sales-performance-page.tsx` — a Recharts `BarChart`
- `frontend/src/components/ui/tabs.tsx`, `select.tsx`, `table.tsx`, `card.tsx`
- `frontend/src/components/layout/header/header-branch-switcher.tsx` — a branch `Select`
- `frontend/src/pages/cash-reports/document-code-link.tsx` (`DocumentCodeLink`), `document-link.ts`
- `frontend/src/features/cash-reports/aging.ts` — `bucketLabel` ("1–30", "> 90")
- `frontend/src/pages/cash-reports/cash-book-page.test.tsx` — page test style
- `frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.test.tsx` — a tab test (Radix tabs react to `mouseDown`, not `click`)
- `frontend/src/lib/vn-datetime.ts` (`todayYmd`), `frontend/src/lib/date-only.ts` (`formatDateOnly`)

## Contracts

```ts
// features/executive-dashboard/links.ts — pure
export const executiveLinks = {
  debtOverdue: (side: 'Receivable' | 'Payable', at: string, partnerId?: string) => string,   // /reports/debt-overdue?side=&at=[&partnerId=]
  debtAging: (side: 'Receivable' | 'Payable', at: string) => string,                         // /reports/debt-aging?side=&at=
  debtBalance: (side: 'Receivable' | 'Payable', from: string, to: string) => string,         // /reports/debt-balance?side=&from=&to=
  debtBySalesperson: (from: string, to: string) => string,                                   // /reports/debt-by-salesperson?side=Receivable&from=&to=
  cashBook: () => '/reports/cash-book',
  stockOnHand: () => '/inventory/stock-on-hand',
  creditLimits: () => '/credit-limits',
};
/// The hint of a report link when the dashboard shows more than the report will:
/// mode 'company' with more than one branch → "Báo cáo mở theo chi nhánh đang làm việc (<code>)"; otherwise undefined.
export function reportLinkTitle(mode: ExecutiveScopeMode, branchCount: number, workingBranchCode?: string): string | undefined;

// features/executive-dashboard/period.ts — pure; parse 'yyyy-MM-dd' by hand into a local Date, print with formatDateYmd
export function rangeDays(from: string, to: string): number;                               // inclusive
export function previousPeriod(from: string, to: string): { from: string; to: string };    // same length, ending the day before `from`
export function defaultGranularity(from: string, to: string): SeriesGranularity;           // up to 31 days → 'Day'; up to 92 → 'Week'; more → 'Month'
export function isDayGranularityAllowed(from: string, to: string): boolean;                // at most 366 days (the server rejects more)

// pages/executive-dashboard/components
export interface DataColumn<T> {
  key: string;
  header: string;
  align?: 'left' | 'right';                       // 'right' adds text-right and tabular-nums to header and cells
  cell: (row: T) => React.ReactNode;
}
DataTable<T>           { columns: DataColumn<T>[]; rows: T[]; rowKey: (row: T, index: number) => string; footer?: React.ReactNode }
BranchDocumentLink     { sourceType: DebtSourceType | CashBookSourceType; sourceId: string; code: string; branchCode: string }
ScopeSelect            { }                                            // reads useExecutiveScope(); renders nothing when !canSwitch
KpiRow                 { params: ExecutiveParams }
GranularityToggle      { value: SeriesGranularity; onChange(g: SeriesGranularity): void; dayDisabled?: boolean }
ProfitChart            { points: SalesPoint[]; canViewCost: boolean; granularity: SeriesGranularity; onGranularityChange?(g): void; dayDisabled?: boolean; height?: number }
CashFlowChart          { points: CashPoint[]; granularity: SeriesGranularity; onGranularityChange?(g): void; dayDisabled?: boolean; height?: number }
FundBalanceTable       { funds: FundBalance[]; showBranch: boolean; limit?: number; total?: number }
DebtTermBar            { side: DebtSideSummary; buckets: AgingBucket[] }
BranchComparisonTable  { rows: BranchComparisonRow[]; onSelectBranch(branchId: string): void }
AttentionList          { attention: Attention | undefined; today: string; from: string; to: string }
RecentFeed             { items: RecentDocument[] }
OverviewTab            { params: ExecutiveParams; openTabs: ExecutiveTab[]; onOpenTab(tab: ExecutiveTab): void }
```

## Rules

### General

- Money: KPI tiles and the branch comparison use `formatVndCompact`; tables print full numbers with `formatNumber` (as the report pages do); chart tooltips and the "Cần chú ý" sub-texts use `formatVnd`. Tests that read a `formatVnd` text match the digits with a regular expression (the string has a non-breaking space before "₫").
- Numeric columns are right-aligned with `tabular-nums` (`DataColumn.align: 'right'`). A `null` number shows "—".
- `{code} – {name}` prints the name alone when the code is null. A list of parts joined with " · " leaves out null parts.
- A link is rendered only when `useCanOpenLink()` accepts it; otherwise the text is plain.
- `today` = `todayYmd()`.
- Type-only imports use `import type`; no unused parameters or props.

### Page shell (`executive-dashboard-page.tsx`)

- State: `useExecutiveParams()`, `useExecutiveScope()`, `useExecutiveAccess()`, `useExecutiveLive(scope.branchId)`.
- `params: ExecutiveParams = { from, to, branchId: scope.branchId }` is passed to the active tab.
- Header, left: `<h1>` "Điều hành", `LiveStatus`, and the line "So với kỳ trước: dd/MM/yyyy – dd/MM/yyyy" (`previousPeriod(from, to)` printed with `formatDateOnly`). Right: `ScopeSelect`, then `RangePicker` with `presets={EXECUTIVE_PRESETS}` (it already prints the chosen period).
- Tab bar: shadcn `Tabs`, controlled by `tab` / `setTab`. Labels: Tổng quan, Kinh doanh, Tiền, Công nợ, Kho. A `PANELS` map from `ExecutiveTab` to a component holds the tabs that exist; this phase registers `overview` only (Phase 07 adds the other four). The bar lists the tabs of `access.tabs` that are in `PANELS`. A `tab` value that is not listed shows `overview`.
- `OverviewTab` receives `openTabs` = the listed tabs except `overview`, and `onOpenTab = setTab`.
- Inactive panels are not mounted (default `TabsContent` behaviour), so their queries do not run.

### Scope select

- Renders nothing when `!canSwitch`.
- Options: "Toàn công ty" (value `company`), then each branch as `{code} — {name}` (value = branch id). The selected value is `company` in company mode and the working branch id in branch mode.
- Choosing an option calls `selectCompany()` or `selectBranch(id)`.

### KPI row

Two groups with small headings: "Trong kỳ" and "Hiện tại". A tile is rendered only when the user has its domain (`useExecutiveAccess`); its hook runs with `enabled` equal to that. Each tile gets `loading={query.isLoading}`, `error={query.isError && query.data === undefined}` and `onRetry={query.refetch}`. Report links carry `title={reportLinkTitle(scope.mode, scope.branches.length, scope.workingBranch?.code)}`.

| Group | Label | Value | Delta | Hint | Link |
|---|---|---|---|---|---|
| Trong kỳ | Doanh thu xuất bán | `formatVndCompact(revenue)` | `revenueDeltaPct` | "so với kỳ trước" | — |
| Trong kỳ | Lãi gộp | `formatVndCompact(grossProfit)`; `null` when `!canViewCost`; badge "tạm tính" when `isProvisional && canViewCost` | — | "Tỷ suất {formatPct(marginPct)}", plus " (▲ x,x điểm)" or " (▼ x,x điểm)" against `prevMarginPct` when both exist; no hint when `!canViewCost` | — |
| Trong kỳ | Thu − chi ròng | `formatSignedCompact(net)` | `netDeltaPct` | "Thu {…receipts} · Chi {…payments}" | — |
| Hiện tại | Tồn quỹ | `formatVndCompact(balance)` | — | "TM {…cashBalance} · NH {…bankBalance}"; tone `bad` when `balance < 0` | `cashBook()` |
| Hiện tại | Phải thu | `formatVndCompact(receivable.balance)` | — | "Quá hạn {…receivable.overdue}"; tone `bad` when it is above 0 | `debtOverdue('Receivable', today)` |
| Hiện tại | Phải trả | `formatVndCompact(payable.balance)` | — | "Đến hạn 7 ngày: {…payable.dueSoon}"; tone `warn` when it is above 0 | `debtBalance('Payable', from, to)` |
| Hiện tại | Giá trị tồn kho | `formatVndCompact(stockValue)`; `null` when `!canViewCost` | — | "{productCount} mã · {warehouseCount} kho" | `stockOnHand()` |

The label "Thu − chi ròng" is a fixed string (copy it from this table). Layout: `grid gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7`, each group label above its tiles on wide screens.

### Tiles of the Tổng quan tab

`OverviewTab` renders `<KpiRow params={params} />` first, then the body: `grid gap-4 xl:grid-cols-[minmax(0,1fr)_280px]`; the left column is a `grid gap-4 lg:grid-cols-2` with the profit chart spanning both columns; the right column stacks "Cần chú ý" and "Vừa phát sinh". On narrow screens everything is one column.

One granularity state lives in `OverviewTab`: it starts at `defaultGranularity(from, to)` and is set back to it whenever `from` or `to` changes. Both charts use it; only the profit chart shows the toggle (when the user has no sales domain, the cash chart shows it). `dayDisabled = !isDayGranularityAllowed(from, to)`.

| Tile title | Needs | `query` | Content | Action |
|---|---|---|---|---|
| Doanh thu – giá vốn – lãi gộp | sales | `useSalesSeries({ ...params, granularity })` | `ProfitChart`; `canViewCost` = some point has `cost !== null` | button "Chi tiết →" → `onOpenTab('sales')` when `openTabs` has `sales` |
| Dòng tiền thu – chi | cash | `useCashSeries({ ...params, granularity })` | `CashFlowChart`, then, when `useCashSummary(params)` has data, `FundBalanceTable` with `limit={5}` and `showBranch={scope.canSwitch && scope.mode === 'company'}` | link "Sổ quỹ →" `cashBook()` |
| Cơ cấu nợ phải thu | debt | `useDebtSummary({ branchId })` | `DebtTermBar` of `receivable` | link "Nợ quá hạn →" `debtOverdue('Receivable', today)` |
| So sánh chi nhánh | `scope.canSwitch && scope.mode === 'company'` | `useBranchComparison({ from, to })` | `BranchComparisonTable` with `onSelectBranch={scope.selectBranch}` | button "Kinh doanh →" → `onOpenTab('sales')` when `openTabs` has `sales` |
| Top khách hàng theo doanh thu | sales | `useSalesTop({ ...params, by: 'Customer', limit: 5 })` | `TopListCard`: `id = row.id`, `primary` = revenue, `secondary` = "LG {formatVndCompact(grossProfit)}" when it is not null | button "Xem thêm →" → `onOpenTab('sales')` when `openTabs` has `sales` |
| Cần chú ý | always | `useAttention({ branchId })` | `AttentionList` | — |
| Vừa phát sinh | always | `useRecentDocuments({ branchId, limit: 10 })` | `RecentFeed` | — |

Every tile is a `TileCard` with the `query` of its row. `isEmpty`: a chart tile is empty when it has no point or every value of every point is 0; a list tile when its list is empty. Report links get `title={reportLinkTitle(...)}` as in the KPI row.

### Components

- **`DataTable`** — built on `components/ui/table.tsx`; one header cell per column, one row per item, the optional `footer` as a last row.
- **`BranchDocumentLink`** — a document belongs to one branch and its screen rejects another working branch. When `branchCode` equals `useExecutiveScope().workingBranch?.code`, render `<DocumentCodeLink sourceType sourceId code />` (it already falls back to text without access); otherwise the code as plain text with `title="Chứng từ của chi nhánh {branchCode}"`.
- **`GranularityToggle`** — three buttons Ngày / Tuần / Tháng with the look of the buttons in `revenue-area-chart.tsx`; "Ngày" is disabled when `dayDisabled`.
- **`ProfitChart`** — Recharts `ComposedChart` with `<Legend />`: bars "Doanh thu" (`hsl(var(--primary))`) and "Giá vốn" (`#94a3b8`), line "Lãi gộp" (`#16a34a`). Without `canViewCost` only the revenue bars are drawn. X axis: the bucket date as `dd/MM` (Day, Week) or `MM/yyyy` (Month), cut from the `yyyy-MM-dd` string. Y ticks `formatVndCompact`. Tooltip values `formatVnd`. The `GranularityToggle` is shown above the chart when `onGranularityChange` is given. Grid, axis and tooltip literals as in `revenue-area-chart.tsx`; `isAnimationActive={false}`; default `height` 240.
- **`CashFlowChart`** — Recharts `BarChart` with `<Legend />`: "Thu" (`#16a34a`) and "Chi" (`#dc2626`) per bucket; same X-axis rule, literals and toggle rule; default `height` 160.
- **`FundBalanceTable`** — a `DataTable` with the columns `Quỹ` (`{code} – {name}`, with `{branchCode} · ` in front when `showBranch`) and `Số dư` (right-aligned, rose text when negative); `limit` keeps the largest by `|balance|`; `total` adds the footer row "Tổng cộng".
- **`DebtTermBar`** — one horizontal CSS bar (as `status-funnel.tsx` draws bars) with a segment for "Trong hạn" (`notDue`) and one per overdue bucket; segment widths are shares of `totalOpen`. Below it a `DataTable`: label, amount, share (whole percent). Labels: "Trong hạn", then `Quá hạn {bucketLabel(bucket)} ngày` for each bucket ("Quá hạn 1–30 ngày" … "Quá hạn > 90 ngày"). Colours: `bg-primary`, `bg-amber-400`, `bg-orange-500`, `bg-rose-500`, `bg-rose-700`. `totalOpen == 0` → "Không có công nợ". When `unallocated > 0` a note line: "Chưa đối trừ: {amount}".
- **`BranchComparisonTable`** — a `DataTable`: Chi nhánh (`{code} — {name}`, a `<button type="button">` that calls `onSelectBranch`), Doanh thu, Lãi gộp % (`formatPct`), Phải thu, Tồn kho (the three money columns `formatVndCompact`).
- **`AttentionList`** — one row per non-null item, in this order, each with a dot, the count and a text. A row is a `<Link>` when its count is above 0 and the route is allowed:

  | Item | Text | Sub-text | Link | Dot when count > 0 |
  |---|---|---|---|---|
  | `overdueReceivables` | "Khách nợ quá hạn" | `formatVnd(amount)` | `debtOverdue('Receivable', today)` | rose |
  | `overCreditLimit` | "Khách vượt hạn mức nợ" | "vượt {formatVnd(amount)}" | `creditLimits()` | rose |
  | `negativeStock` | "Mã hàng tồn âm" | — | `stockOnHand()` | amber |
  | `payablesDueSoon` | "Khoản phải trả đến hạn 7 ngày" | `formatVnd(amount)` | `debtBalance('Payable', from, to)` | amber |
  | `negativeFunds` | "Quỹ âm" | `formatVnd(amount)` | `cashBook()` | rose |

  A row with count 0 has an emerald dot and the sub-text "Không có". When every item is null: "Không có mục nào trong quyền xem của bạn."
- **`RecentFeed`** — per row: a tag (Thu / Chi / Nhập / Xuất; emerald for Thu, rose for Chi, primary for the stock kinds), the code as `<BranchDocumentLink sourceType={kind} sourceId={id} code branchCode />`, the line "{partnerName} · {ownerName} · HH:mm" (null parts left out; the branch code in front when the scope is the company), and the amount (`formatNumber`, with "+" in front for Thu and "-" for Chi). Empty → "Hôm nay chưa có chứng từ."

## Tasks

Frontend commands run from `frontend/` in the worktree. Component tests mock `@/features/executive-dashboard/hooks` and, where a component reads them, `use-executive-scope`, `use-executive-access` and `use-executive-live`; they set permissions with `useAuthStore.setState`, as `cash-book-page.test.tsx` does. Stage exactly the files each task names.

### Task 6.1 — Links and period helpers

Files: `features/executive-dashboard/links.ts`, `links.test.ts`, `period.ts`, `period.test.ts`.

1. Write the failing tests:
   - `links.test.ts`: `debtOverdue('Receivable', '2026-10-13') === '/reports/debt-overdue?side=Receivable&at=2026-10-13'`; with a partner id `&partnerId=…` is appended; `debtBalance('Payable', '2026-10-01', '2026-10-13') === '/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-13'`; `debtAging('Payable', '2026-10-13') === '/reports/debt-aging?side=Payable&at=2026-10-13'`; `debtBySalesperson('2026-10-01', '2026-10-13')` has `side=Receivable`; `reportLinkTitle('company', 3, 'CN01')` contains "CN01"; `reportLinkTitle('company', 1, 'CN01')` and `reportLinkTitle('branch', 3, 'CN01')` are `undefined`.
   - `period.test.ts`: `rangeDays('2026-10-01', '2026-10-13') === 13`; `previousPeriod('2026-10-01', '2026-10-13')` → `2026-09-18 … 2026-09-30`; `previousPeriod('2026-10-13', '2026-10-13')` → `2026-10-12 … 2026-10-12`; `defaultGranularity('2026-10-01', '2026-10-31') === 'Day'`; `('2026-08-01', '2026-10-13') → 'Week'`; `('2026-01-01', '2026-10-13') → 'Month'`; `isDayGranularityAllowed('2026-01-01', '2026-10-13') === true`; `('2025-01-01', '2026-10-13') → false`.
2. Run `npx vitest run src/features/executive-dashboard/links.test.ts src/features/executive-dashboard/period.test.ts`. Expected: FAIL — the modules do not exist.
3. Implement both modules.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard links and period helpers"`.

### Task 6.2 — Page shell

Files: `pages/executive-dashboard/components/scope-select.tsx`, `scope-select.test.tsx`, `pages/executive-dashboard/tabs/overview-tab.tsx`, `pages/executive-dashboard/executive-dashboard-page.tsx`, `executive-dashboard-page.test.tsx`.

1. Write the failing tests:
   - `scope-select.test.tsx`: nothing is rendered when `canSwitch` is false; with two branches the options are "Toàn công ty" and the two `{code} — {name}`; choosing a branch calls `selectBranch(id)`; choosing "Toàn công ty" calls `selectCompany()`.
   - `executive-dashboard-page.test.tsx` (replace the placeholder test; `OverviewTab`, `use-executive-live`, `use-executive-scope` and `use-executive-access` mocked): heading "Điều hành"; the `LiveStatus` text is present; the chips are "Hôm nay", "Tuần này", "Tháng này", "Năm nay"; the tab "Tổng quan" is selected; `OverviewTab` receives `params` with `from`, `to` and the scope's `branchId`, and `openTabs` empty; for `?from=2026-10-01&to=2026-10-13` the line "So với kỳ trước: 18/09/2026 – 30/09/2026" is shown.
2. Run `npx vitest run src/pages/executive-dashboard`. Expected: FAIL — `scope-select` does not exist and the page has no chips.
3. Implement `scope-select.tsx` and the page shell, with `overview-tab.tsx` as a component that takes its props object without reading it and renders nothing yet (the real tab is Task 6.7).
4. Run the same command and `npm run typecheck`. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard page shell"`.

### Task 6.3 — KPI row

Files: `pages/executive-dashboard/components/kpi-row.tsx`, `kpi-row.test.tsx`.

1. Write the failing test (hooks mocked with one data set):
   - with all four domains: the seven labels and the values "2,84 tỷ" (revenue 2,840,000,000), "412 tr" (gross profit 412,000,000), "+186 tr" (net 186,000,000), "1,26 tỷ" (balance 1,260,000,000), "3,10 tỷ" (receivable balance 3,100,000,000), "1,75 tỷ" (payable balance 1,750,000,000), "4,62 tỷ" (stock value 4,620,000,000);
   - `canViewCost: false` → the Lãi gộp and Giá trị tồn kho values are "—" and there is no "tạm tính";
   - `isProvisional: true` → the badge "tạm tính";
   - `marginPct 14.5`, `prevMarginPct 13.7` → the hint contains "Tỷ suất 14,5%" and "▲ 0,8 điểm";
   - `receivable.overdue > 0` → the hint "Quá hạn …" has the rose class;
   - without the cash domain → no "Tồn quỹ" and no "Thu − chi ròng" tile, and `useCashSummary` was called with `enabled: false`;
   - the Phải thu tile links to `/reports/debt-overdue?side=Receivable&at=<today>` for a user with `reports.debt`;
   - a summary in error without data → its tiles show "Thử lại".
2. Run `npx vitest run src/pages/executive-dashboard/components/kpi-row.test.tsx`. Expected: FAIL — the module does not exist.
3. Implement `kpi-row.tsx`.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive KPI row"`.

### Task 6.4 — Data table and branch document link

Files: `pages/executive-dashboard/components/data-table.tsx`, `data-table.test.tsx`, `branch-document-link.tsx`, `branch-document-link.test.tsx`.

1. Write the failing tests:
   - `data-table.test.tsx`: one header cell per column and one row per item; a column with `align: 'right'` has `text-right` and `tabular-nums` on its header and its cells; the `footer` is rendered as the last row.
   - `branch-document-link.test.tsx`: with the working branch code equal to `branchCode` and `receipts.view` held → a link to `/receipts/<id>`; same branch without the permission → plain text; another `branchCode` → plain text with the title "Chứng từ của chi nhánh <code>" and no link.
2. Run `npx vitest run src/pages/executive-dashboard/components/data-table.test.tsx src/pages/executive-dashboard/components/branch-document-link.test.tsx`. Expected: FAIL — the modules do not exist.
3. Implement both components.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive dashboard table and document link"`.

### Task 6.5 — Charts, fund table, debt term bar, branch table

Files: `pages/executive-dashboard/components/granularity-toggle.tsx`, `profit-chart.tsx`, `cash-flow-chart.tsx`, `fund-balance-table.tsx`, `debt-term-bar.tsx`, `branch-comparison-table.tsx` and their `.test.tsx` files.

1. Write the failing tests:
   - `granularity-toggle.test.tsx`: three buttons; clicking "Tuần" calls `onChange('Week')`; `dayDisabled` disables "Ngày".
   - `profit-chart.test.tsx`: with points an `svg` is rendered; with `canViewCost` the legend has "Giá vốn" and "Lãi gộp", without it neither; the toggle is present only when `onGranularityChange` is given.
   - `cash-flow-chart.test.tsx`: with points an `svg` and the legend entries "Thu" and "Chi"; no toggle without the handler.
   - `fund-balance-table.test.tsx`: one row per fund with `{code} – {name}` and the balance with thousand separators; `showBranch` puts the branch code in front; a negative balance has the rose class; `limit={1}` keeps the fund with the largest `|balance|`; `total={29300}` adds a "Tổng cộng" row.
   - `debt-term-bar.test.tsx`: `totalOpen 3000`, `notDue 0`, `overdueAmounts [2000, 1000, 0, 0]`, buckets (1,30), (31,60), (61,90), (91,null) → rows "Trong hạn", "Quá hạn 1–30 ngày", "Quá hạn 31–60 ngày", "Quá hạn 61–90 ngày", "Quá hạn > 90 ngày" with the amounts and the shares 0%, 67%, 33%, 0%, 0%; `unallocated 1500` → the note "Chưa đối trừ"; `totalOpen 0` → "Không có công nợ".
   - `branch-comparison-table.test.tsx`: one row per branch; a `null` cell shows "—"; clicking a branch name calls `onSelectBranch(id)`.
2. Run `npx vitest run src/pages/executive-dashboard/components`. Expected: FAIL — the six modules do not exist.
3. Implement the six components.
4. Run the same command and `npm run typecheck`. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): executive overview charts and tables"`.

### Task 6.6 — Attention list and recent feed

Files: `pages/executive-dashboard/components/attention-list.tsx`, `attention-list.test.tsx`, `recent-feed.tsx`, `recent-feed.test.tsx`.

1. Write the failing tests:
   - `attention-list.test.tsx`: five rows for a full object; a null item has no row; count 0 → "Không có" and no link; "Khách nợ quá hạn" with count 12 links to the overdue report when `reports.debt` is held and is plain text otherwise; every item null → "Không có mục nào trong quyền xem của bạn."
   - `recent-feed.test.tsx`: tags Thu / Chi / Nhập / Xuất by kind; a receipt of the working branch links to `/receipts/:id` for a user with `receipts.view`; a document of another branch is plain text; a receipt amount starts with "+", a payment with "-"; a row without `partnerName` shows only the owner and the time; empty → "Hôm nay chưa có chứng từ."
2. Run `npx vitest run src/pages/executive-dashboard/components/attention-list.test.tsx src/pages/executive-dashboard/components/recent-feed.test.tsx`. Expected: FAIL — the modules do not exist.
3. Implement both components.
4. Run the same command. Expected: PASS.
5. Commit: `git commit -m "feat(frontend): attention list and recent documents feed"`.

### Task 6.7 — Tổng quan tab

Files: `pages/executive-dashboard/tabs/overview-tab.tsx`, `overview-tab.test.tsx`.

1. Write the failing test (hooks mocked):
   - with every domain and company scope: the KPI labels "Doanh thu xuất bán" and "Tồn quỹ", and the tile titles "Doanh thu – giá vốn – lãi gộp", "Dòng tiền thu – chi", "Cơ cấu nợ phải thu", "So sánh chi nhánh", "Top khách hàng theo doanh thu", "Cần chú ý", "Vừa phát sinh";
   - branch scope, or a user who cannot switch → no "So sánh chi nhánh" and `useBranchComparison` called with `enabled: false`;
   - without the debt domain → no "Cơ cấu nợ phải thu";
   - one hook in error without data → that tile shows "Thử lại" and the other tiles still render;
   - in company scope with three branches the "Sổ quỹ →" link has the `title` hint; in branch scope it has none;
   - `useCashSeries` and `useSalesSeries` receive the same `granularity`; after the period changes from one month to a year both receive `'Month'`;
   - `openTabs={['sales']}` → the buttons "Chi tiết →", "Kinh doanh →" and "Xem thêm →" exist and clicking one calls `onOpenTab('sales')`; `openTabs={[]}` → none of them exists.
2. Run `npx vitest run src/pages/executive-dashboard/tabs/overview-tab.test.tsx`. Expected: FAIL — the tab renders nothing.
3. Implement `overview-tab.tsx`.
4. Run `npx vitest run src/pages/executive-dashboard src/features/executive-dashboard`, `npm run typecheck`, `npm run lint`. Expected: PASS; no new lint problem.
5. Commit: `git commit -m "feat(frontend): executive dashboard overview tab"`.

## Verification

From `frontend/`:

- `npm run typecheck` — exit 0.
- `npm run lint` — no more than the baseline's 3 errors and 14 warnings, and none in a file this phase created.
- `npm run test` — only the 3 baseline failures.
- `npm run build` — exit 0; restore the tracked `tsconfig.*.tsbuildinfo` files.

## Exit Criteria

- `/dashboard/executive` shows the header, the KPI row and the tiles of the Tổng quan tab, each gated by the domain permission.
- Tiles a user may not see are absent and their requests are not sent.
- One failing request leaves the rest of the page usable; a failed background refresh keeps the last figures.
- A document of another branch is never a link.
- Seven commits, one per task.
