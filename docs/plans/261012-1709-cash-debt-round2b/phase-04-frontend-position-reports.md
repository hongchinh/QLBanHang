# Phase 04 — Frontend: documents, aging, overdue pages & panel link

**Status:** [ ] pending
**Complexity:** L

## Objective

Ship the three "as at a date" reports in the UI, under the **Tiền & Công nợ** group:

- **Công nợ theo chứng từ** (`/reports/debt-documents`);
- **Tuổi nợ** (`/reports/debt-aging`), with bucket boundaries typed on the screen (not saved);
- **Nợ quá hạn** (`/reports/debt-overdue`), the overdue warning list (B2);
- the "Xem nợ quá hạn" link on the stock-out customer debt panel.

Every page keeps its filters in the URL (like the ledger page), so other screens can open it for one partner.

## Files

- `frontend/src/features/cash-reports/{types,api,keys,hooks}.ts` (modify)
- `frontend/src/features/cash-reports/aging.ts` (+ `aging.test.ts`) (new)
- `frontend/src/pages/cash-reports/report-partner-picker.tsx` (new — extracted from `debt-ledger-page.tsx`)
- `frontend/src/pages/cash-reports/debt-ledger-page.tsx` (modify — use the extracted picker)
- `frontend/src/pages/cash-reports/debt-documents-page.tsx` (+ `.test.tsx`) (new)
- `frontend/src/pages/cash-reports/debt-aging-page.tsx` (+ `.test.tsx`) (new)
- `frontend/src/pages/cash-reports/debt-overdue-page.tsx` (+ `.test.tsx`) (new)
- `frontend/src/pages/cash-reports/bucket-boundaries-input.tsx` (new, Task 4.5)
- `frontend/src/pages/cash-reports/report-partner-picker.test.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/customer-debt-panel.tsx` (+ `.test.tsx`) (modify)
- `frontend/src/App.tsx`, `frontend/src/lib/route-permissions.ts` (+ `route-permissions.test.ts`), `frontend/src/components/layout/nav-config.ts` (+ `nav-config.test.ts`) (modify)

## Reference files (read-only)

- `frontend/src/pages/cash-reports/debt-ledger-page.tsx` + test — mocks `@/features/cash-reports/hooks` and `@/features/debt/hooks` (`useDebtPartnerSearch`), sets permissions with `useAuthStore.setState`; URL filters (`useSearchParams`, `updateParams`), `firstDayOfMonthYmd` / `todayYmd`, `rangeError`, table classes, `DocumentCodeLink`
- `frontend/src/pages/cash-reports/debt-balance-page.tsx` + test — `renderAt(url)` with `MemoryRouter` + `LocationDisplay`, row click → navigate (it mocks neither the partner search nor permissions; take those from `debt-ledger-page.test.tsx`)
- `frontend/src/pages/cash-reports/document-link.ts` — `DEBT_SOURCE_TYPE_LABELS`, `formatAmountCell`, `formatSignedBalance`, `NUMERIC_CELL`, `parseDebtSide`
- Phases 01–02 of this plan — the API contracts (field names below are their camelCase JSON)

## Contracts used

```ts
// features/cash-reports/types.ts (add; mirror DebtPositionReportDtos.cs)
export interface AgingBucket { from: number; to?: number | null }

// GET /api/reports/debt-documents (reports.debt)
export interface DebtDocumentsParams { side: DebtSide; at: string; partnerId?: string }
export interface DebtDocumentRow { entryId: string; sourceType: DebtSourceType; sourceId: string; docCode: string; docDate: string;
  dueDate: string; description?: string | null; amount: number; settled: number; remaining: number;
  daysOutstanding: number; daysOverdue: number }
export interface DebtUnallocatedRow { entryId: string; sourceType: DebtSourceType; sourceId: string; docCode: string; docDate: string;
  description?: string | null; amount: number; settled: number; remaining: number }
export interface DebtDocumentsGroup { partnerId: string; partnerCode?: string | null; partnerName?: string | null;
  items: DebtDocumentRow[]; unallocated: DebtUnallocatedRow[]; totalRemaining: number; totalUnallocated: number; balance: number }
export interface DebtDocumentsReport { side: DebtSide; at: string; groups: DebtDocumentsGroup[];
  totalRemaining: number; totalUnallocated: number; totalBalance: number }

// GET /api/reports/debt-aging (reports.debt) — buckets: "30,60,90"
export interface DebtAgingParams { side: DebtSide; at: string; partnerId?: string; buckets?: string }
export interface DebtAgingRow { partnerId?: string | null; partnerCode?: string | null; partnerName?: string | null;
  amounts: number[]; totalOpen: number; unallocated: number; balance: number }
export interface DebtAgingReport { side: DebtSide; at: string; buckets: AgingBucket[]; rows: DebtAgingRow[]; totals: DebtAgingRow }

// GET /api/reports/debt-overdue (reports.debt)
export interface DebtOverdueParams { side: DebtSide; at: string; partnerId?: string; buckets?: string }
export interface DebtOverdueRow { entryId: string; sourceType: DebtSourceType; sourceId: string; docCode: string; docDate: string;
  dueDate: string; description?: string | null; amount: number; remaining: number; daysOverdue: number; bucketIndex: number }
export interface DebtOverdueGroup { partnerId: string; partnerCode?: string | null; partnerName?: string | null;
  items: DebtOverdueRow[]; amounts: number[]; totalOverdue: number; unallocated: number }
export interface DebtOverdueReport { side: DebtSide; at: string; buckets: AgingBucket[]; groups: DebtOverdueGroup[];
  totalAmounts: number[]; totalOverdue: number; totalUnallocated: number }
```

```ts
// features/cash-reports/aging.ts
export const DEFAULT_BUCKETS = '30,60,90';
/** Mirrors DebtAging.ParseBoundaries: "15, 45" → "15,45"; null when not 1–6 strictly ascending positive integers. Blank → DEFAULT_BUCKETS.
 *  Each trimmed part must match /^[0-9]+$/ and be 1…2147483647 (what the server's int.TryParse with NumberStyles.None accepts);
 *  the output joins the parsed numbers. */
export function normalizeBuckets(input: string): string | null;
/** "0–30", "31–60", "> 90" (en dash; open-ended bucket "> {from − 1}"). */
export function bucketLabel(bucket: AgingBucket): string;
```

## Tasks

### Task 4.1 — Types, API, keys, hooks and the bucket helper

1. **Write the failing test** `features/cash-reports/aging.test.ts`:
   - `normalizeBuckets`: `''` → `'30,60,90'`; `' 15 , 45 '` → `'15,45'`; `'7'` → `'7'`; `'60,30'`, `'30,30'`, `'0,30'`, `'abc'`, `'30,,60'`, `'1,2,3,4,5,6,7'`, `'+5'`, `'1e2'`, `'30abc'`, `'99999999999'`, `'36501'`, `'30,50000'` → `null` (each value is capped at 36500 days, mirroring `DebtAging.MaxBoundaryDays`; the cap was added in Phase 02 deviation to prevent int overflow in bucket ranges, so `normalizeBuckets` must reject values above 36500).
   - `bucketLabel`: `{from:0,to:30}` → `'0–30'`; `{from:31,to:60}` → `'31–60'`; `{from:91,to:null}` → `'> 90'`; `{from:1,to:30}` → `'1–30'`.
2. **Run the test to verify it fails:** `npx vitest run src/features/cash-reports/aging.test.ts`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** `aging.ts`; add the types above to `types.ts`; `cashReportsApi.debtDocuments / debtAging / debtOverdue` (`apiGet` to `/reports/debt-documents` etc.); keys `debtDocuments(params)`, `debtAging(params)`, `debtOverdue(params)` under `cashReportKeys.all`; hooks `useDebtDocuments`, `useDebtAging`, `useDebtOverdue` with the same `{ enabled }` option as `useDebtBalance`.
4. **Run the test to verify it passes:** same command, then `npm run typecheck`. Expected: PASS, no type errors.
5. **Commit:** `git commit -m "feat(frontend): debt position report api, hooks and bucket helper"`

### Task 4.2 — Extract the report partner picker

1. **Write the failing test:** in `debt-ledger-page.test.tsx` nothing changes; add `report-partner-picker` coverage by changing the ledger page import only after the new file exists. RED step: run `npx vitest run src/pages/cash-reports/debt-ledger-page.test.tsx` **after** replacing the page's local `PartnerPicker` with an import from `./report-partner-picker` but **before** creating that file. Expected: FAIL — cannot resolve `./report-partner-picker`.
2. **Run the test to verify it fails** (as above).
3. **Write the minimal implementation:** move `PartnerPicker` to `report-partner-picker.tsx` as `export function ReportPartnerPicker({ id, side, selectedLabel, onSelect, onClear, clearLabel }: { id: string; side?: DebtSide; selectedLabel: string; onSelect: (p: DebtPartnerSearchItem) => void; onClear?: () => void; clearLabel?: string })` — when `onClear` is given, the combobox shows a clear entry labelled `clearLabel` (default "Tất cả đối tượng") that calls it; `side` undefined searches every partner (`useDebtPartnerSearch(undefined, keyword, { includeInactive: true })`), used by the reconciliation in Phase 05. The ledger page passes `id="debt-ledger-partner"`.
4. **Write `report-partner-picker.test.tsx`** (mock `useDebtPartnerSearch`): selecting a result calls `onSelect`; with `onClear` the clear entry is shown and calls it; without `onClear` it is absent.
5. **Run tests to verify they pass:** `npx vitest run src/pages/cash-reports`. Expected: PASS (ledger tests unchanged).
6. **Commit:** `git add frontend/src/pages/cash-reports/report-partner-picker.tsx frontend/src/pages/cash-reports/report-partner-picker.test.tsx frontend/src/pages/cash-reports/debt-ledger-page.tsx && git commit -m "refactor(frontend): share the report partner picker"`

### Task 4.3 — Công nợ theo chứng từ page

Page `DebtDocumentsPage`, route `/reports/debt-documents`, filters in the URL: `side` (`parseDebtSide`), `at` (default `todayYmd()`), `partnerId` (optional, `ReportPartnerPicker` with `onClear` that removes `partnerId` from the URL). **Partner rules (documents, aging and overdue pages):** the picker label is the picked item's label, else the first group/row whose `partnerId` matches the URL (`partnerCode - partnerName`), else the fallback "Đối tượng đã chọn" (a deep link before the data loads); switching Phía removes `partnerId` from the URL and keys the picker by side. Layout: filter `Card` (Phía, Tại ngày, Đối tượng); then per group a heading "{code} - {name}", a table **Chứng từ còn nợ** (Ngày CT, Số CT with `DocumentCodeLink`, Loại CT, Diễn giải, Hạn TT, Số tiền, Đã đối trừ, Còn lại, Số ngày nợ, Quá hạn (ngày; empty when 0, red when > 0)), a table **Chưa đối trừ** when `unallocated` is not empty (Ngày CT, Số CT, Loại CT, Diễn giải, Số tiền, Đã đối trừ, Còn lại), and a line "Số dư: {formatSignedBalance(side, balance)}". Footer card: totals Còn lại / Chưa đối trừ / Số dư. Empty: "Không có chứng từ còn nợ tại ngày này".

1. **Write the failing test** `debt-documents-page.test.tsx` (mock `useDebtDocuments` and `useDebtPartnerSearch`, set permissions with `useAuthStore.setState` so `DocumentCodeLink` renders links, as `debt-ledger-page.test.tsx` does):
   - renders with defaults → the hook gets `{ side: 'Receivable', at: <today>, partnerId: undefined }`;
   - with `?side=Payable&at=2026-10-31&partnerId=p1` → the hook gets those params;
   - a group with one item (overdue 41) and one unallocated row → both tables, the item's "41" cell has the destructive class, the code links to `/stock-out/{id}` for a StockOut row, and the "Số dư" line shows exactly `formatSignedBalance('Receivable', 1500)` (import it in the test rather than hard-coding the number format);
   - empty groups → the empty message;
   - changing "Tại ngày" updates the URL (`LocationDisplay`);
   - with `?partnerId=p1` the clear entry removes `partnerId` from the URL; changing Phía removes it too;
   - route and nav: in `lib/route-permissions.test.ts` add `expectRule` for the five new paths (`/reports/debt-documents`, `debt-aging`, `debt-overdue`, `debt-by-salesperson`, `debt-reconciliation`) with `reports.debt`; in `components/layout/nav-config.test.ts` update the exact item list of the Tiền & Công nợ group (final order in `SUMMARY.md`; items added later are appended to it by their own tasks). These two edits are part of the RED step.
2. **Run the test to verify it fails:** `npx vitest run src/pages/cash-reports/debt-documents-page.test.tsx src/lib src/components/layout`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** the page; route in `App.tsx` next to `debt-ledger` (`<ProtectedRoute permission="reports.debt">`); extend the `route-permissions.ts` pattern to `/^\/reports\/(debt-balance|debt-ledger|debt-documents|debt-aging|debt-overdue|debt-by-salesperson|debt-reconciliation)$/` once here (the other pages reuse it); nav item `{ to: '/reports/debt-documents', label: 'Công nợ theo chứng từ', icon: FileText, permission: 'reports.debt' }` after "Sổ chi tiết công nợ".
4. **Run tests to verify they pass:** `npx vitest run src/pages/cash-reports src/lib src/components/layout`. Expected: PASS.
5. **Commit:** `git add` the page, its test, `App.tsx`, `route-permissions.ts` and its test, `nav-config.ts` and its test, then `git commit -m "feat(frontend): debt documents report page"`

### Task 4.4 — Tuổi nợ page

Page `DebtAgingPage`, route `/reports/debt-aging`. Filters: Phía, Tại ngày, Đối tượng (optional, partner rules of Task 4.3), **Mốc tuổi nợ** text input (`buckets` in the URL, default `30,60,90`). The input keeps local text; on blur or Enter, `normalizeBuckets(text)`: valid → write the normalized value to the URL; invalid → show "Mốc không hợp lệ: các số nguyên dương tăng dần, cách nhau bởi dấu phẩy, tối đa 6 mốc" and keep the last valid value in the URL. A `buckets` value that arrives through the URL is validated the same way: `const buckets = normalizeBuckets(searchParams.get('buckets') ?? '')`; when it is `null` the page shows the same error and calls the hook with `enabled: false`, so no request carries invalid buckets. Empty state when `rows` is empty: "Không có công nợ tại ngày này" (the totals arrays are zero-filled by the API, B12, and are not rendered then). Table: Mã, Tên, one column per `buckets[i]` labelled `bucketLabel`, Tổng còn nợ, Chưa đối trừ, Số dư (`formatSignedBalance`); footer row from `totals`. Clicking a row navigates to `/reports/debt-documents?side=&at=&partnerId={id}`.

1. **Write the failing test** `debt-aging-page.test.tsx`:
   - default params → hook receives `buckets: '30,60,90'`; headers "0–30", "31–60", "61–90", "> 90" come from the mocked response's `buckets`;
   - typing `15, 45` then blur → URL has `buckets=15%2C45` and the hook receives `'15,45'`;
   - typing `45,15` then blur → the error text shows, the URL keeps the previous buckets;
   - row click → location `/reports/debt-documents?side=Receivable&at=…&partnerId=p1`;
   - totals row renders `totals.amounts`;
   - `?buckets=45,15` in the URL → the error text, the hook is called with `enabled: false`;
   - empty `rows` → the empty message;
   - nav: add the item to the `nav-config.test.ts` list (RED step) after "Công nợ theo chứng từ".
2. **Run the test to verify it fails:** `npx vitest run src/pages/cash-reports/debt-aging-page.test.tsx`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** the page, the route, the nav item `{ to: '/reports/debt-aging', label: 'Tuổi nợ', icon: Hourglass, permission: 'reports.debt' }`.
4. **Run tests to verify they pass:** `npx vitest run src/pages/cash-reports src/features/cash-reports src/components/layout`. Expected: PASS.
5. **Commit:** `git add` the page, its test, `App.tsx`, `nav-config.ts` and its test, then `git commit -m "feat(frontend): debt aging report page with editable buckets"`

### Task 4.5 — Nợ quá hạn page

Page `DebtOverduePage`, route `/reports/debt-overdue`. Filters as the aging page, with the same URL-`buckets` validation and partner rules (the boundaries input reuses the same component — extract `BucketBoundariesInput` into `pages/cash-reports/bucket-boundaries-input.tsx` in this task and use it in both pages). Per group: heading, table (Số CT link, Ngày CT, Hạn TT, Diễn giải, Còn lại, Số ngày quá hạn, Nhóm = `bucketLabel(buckets[row.bucketIndex])`), a subtotal line per bucket, "Tổng quá hạn", and "Chưa đối trừ (tham khảo): {unallocated}" when > 0. Footer: `totalAmounts` per bucket, `totalOverdue`. Empty: "Không có khoản nợ quá hạn tại ngày này".

1. **Write the failing test** `debt-overdue-page.test.tsx`:
   - defaults → hook receives `{ side: 'Receivable', at: <today>, buckets: '30,60,90' }`;
   - a group with two items renders the "Nhóm" labels "31–60" and "1–30", the totals, and the unallocated note;
   - `?partnerId=p1` from the URL is passed to the hook;
   - the empty message;
   - `?buckets=60,30` → the error text and `enabled: false`;
   - nav: add the item to the `nav-config.test.ts` list (RED step) after "Tuổi nợ".
2. **Run the test to verify it fails:** `npx vitest run src/pages/cash-reports/debt-overdue-page.test.tsx src/components/layout`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** the page, `BucketBoundariesInput` (move the input logic out of the aging page; its tests keep passing), route, nav item `{ to: '/reports/debt-overdue', label: 'Nợ quá hạn', icon: AlarmClock, permission: 'reports.debt' }`.
4. **Run tests to verify they pass:** `npx vitest run src/pages/cash-reports src/components/layout`. Expected: PASS.
5. **Commit:** `git add` the page, its test, `bucket-boundaries-input.tsx`, the aging page, `App.tsx`, `nav-config.ts` and its test, then `git commit -m "feat(frontend): overdue debt report page"`

### Task 4.6 — "Xem nợ quá hạn" link on the customer debt panel (B2)

1. **Write the failing test** in `customer-debt-panel.test.tsx` (wrap every render in `MemoryRouter`, including the existing ones, which currently render without a router; set permissions through `useAuthStore.setState` the way `debt-ledger-page.test.tsx` does):
   - `overdueAmount > 0` and the user has `reports.debt` → a link "Xem nợ quá hạn" with `href` `/reports/debt-overdue?side=Receivable&partnerId={P1}` and `target="_blank"` (`rel="noopener noreferrer"`), so the unsaved voucher stays open;
   - `overdueAmount = 0` → no link;
   - user without `reports.debt` (only `debt.partner_summary`) → no link.
2. **Run the test to verify it fails:** `npx vitest run src/pages/stock-vouchers/components/customer-debt-panel.test.tsx`. Expected: FAIL — link not found.
3. **Write the minimal implementation:** in `CustomerDebtPanel`, read `useAuthStore((s) => s.hasPermission)`; under the "Nợ quá hạn" row render a `Link` (react-router, `target="_blank"`, `rel="noopener noreferrer"`) when both conditions hold (small text, `text-destructive underline`). The stock-voucher form already renders the panel inside the router.
4. **Run tests to verify they pass:** `npx vitest run src/pages/stock-vouchers`. Expected: PASS.
5. **Commit:** `git add frontend/src/pages/stock-vouchers/components/customer-debt-panel.tsx frontend/src/pages/stock-vouchers/components/customer-debt-panel.test.tsx && git commit -m "feat(frontend): link the customer debt panel to the overdue report"`

## Verification

```bash
cd frontend
npx vitest run src/features/cash-reports src/pages/cash-reports src/pages/stock-vouchers
npm run typecheck
npm run lint
npm run build
git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo   # the build rewrites tracked files
```

## Exit Criteria

- The three pages render the Phase 01–02 contracts, keep their filters in the URL and are reachable only with `reports.debt` (route, nav, route-permissions).
- Invalid bucket boundaries never reach the API, whether typed or taken from the URL.
- `nav-config.test.ts` and `route-permissions.test.ts` list the new items and routes.
- The panel link shows only with an overdue amount and `reports.debt`.
- Typecheck clean; lint no worse than `BASELINE.md`; `npm run build` passes; no new frontend test failures.
