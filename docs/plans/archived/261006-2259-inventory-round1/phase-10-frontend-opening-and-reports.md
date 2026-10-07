# Phase 10 — Frontend opening stock & inventory reports

**Status:** [x] complete
**Complexity:** M

## Objective

Add the remaining "Kho" screens: the opening stock grid (Tồn đầu kỳ), the stock-on-hand report (Tồn kho) and the stock card (Thẻ kho). Cost and value columns appear only for `inventory.view_cost`, and values from a period that has not ended are labelled provisional ("tạm tính").

## Files

- `frontend/src/features/opening-stock/{types,api,keys,hooks}.ts` (new)
- `frontend/src/features/inventory-reports/{types,api,keys,hooks}.ts` (new)
- `frontend/src/pages/inventory/opening-stock-page.tsx`, `opening-stock-page.test.tsx` (new)
- `frontend/src/pages/inventory/stock-on-hand-page.tsx`, `stock-on-hand-page.test.tsx` (new)
- `frontend/src/pages/inventory/stock-card-page.tsx`, `stock-card-page.test.tsx` (new)
- `frontend/src/App.tsx` (modify)

## Reference files (read-only)

- Phase 06 API: `GET/PUT /api/inventory/opening-stock`, `GET /api/reports/stock-on-hand`, `GET /api/reports/stock-card`
- `frontend/src/pages/stock-vouchers/components/negative-stock-dialog.tsx`, `frontend/src/lib/api-client.ts` (`getApiError`)
- `frontend/src/pages/quotations/components/product-typeahead-cell.tsx`
- `frontend/src/pages/reports/sales-revenue-page.tsx` — report page layout

## Tasks

### Task 10.1 — Opening stock screen

- `features/opening-stock/types.ts`: `OpeningStockGrid { warehouseId, openingDate?: string, lines: OpeningStockLine[] }`, `OpeningStockLine { productId, productCode, productName, unitName, quantity, amount }`, `SaveOpeningStockRequest { warehouseId, openingDate, acknowledgeNegativeStock, lines: { productId, quantity, amount }[] }`.
- `api.ts`: `get(warehouseId)` and `save(body)`. `hooks.ts`: `useOpeningStock(warehouseId?)` and `useSaveOpeningStock()`, which invalidates `inventoryKeys.all` (vouchers, stock-at, reports and opening stock share that root since Phase 09 Task 9.3). Create `features/inventory-reports/keys.ts` here (`inventoryReportKeys = { all: [...inventoryKeys.all, 'reports'] as const, stockOnHand: (p) => [...all, 'stock-on-hand', p], stockCard: (p) => [...all, 'stock-card', p] }`, and `openingStockKeys` under `[...inventoryKeys.all, 'opening-stock']`); Task 10.2 adds the rest of that module.
- `opening-stock-page.tsx`, route `/inventory/opening-stock` (`inventory.opening_stock`; values are visible to this permission — D36):
  - Kho `Select` (`useWarehouses()`); Ngày tồn đầu (`type="date"`, default = the loaded `openingDate` or `firstDayOfMonthYmd()` from `lib/vn-datetime.ts` — never `toISOString().slice(0, 10)`, which gives the previous day before 07:00 VN).
  - Grid: STT · Mã hàng (`ProductTypeaheadCell`; a suggestion — from the typeahead **or** the catalog dialog, both via `toProductSuggestion` — with `trackInventory === false` shows the toast `Hàng không theo dõi tồn kho` and is ignored; a duplicate shows `Hàng đã có trong danh sách`) · Tên hàng · ĐVT (stock unit) · Số lượng (stock unit, `formatStockQuantity`) · Giá trị · Đơn giá (read-only `value / quantity`) · delete.
  - `Thêm dòng`; `Lưu`. On 422 `NEGATIVE_STOCK_WARNING` open `NegativeStockDialog`, then resend with `acknowledgeNegativeStock: true`. Blocked → dialog. `PERIOD_LOCKED` / validation / other errors → toast with `formatApiErrorDetails` (line errors under the row when `details` has `lines[i].…`).

1. **Write the failing test** `pages/inventory/opening-stock-page.test.tsx` (mock the hooks):
   - `loads the grid, adds a tracked product, rejects an untracked one and saves the payload`
   - `rejects an untracked product picked from the catalog dialog`
   - `default opening date is the first day of the current VN month` (fake timers at 2026-10-01 06:00 VN)
   - `negative stock warning resends with acknowledgement`
2. **Run the test to verify it fails:** `npx vitest run src/pages/inventory/opening-stock-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** feature module, page, route.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): opening stock screen"`

### Task 10.2 — Stock-on-hand report screen

- `features/inventory-reports/types.ts`: `StockOnHandReport` and `StockOnHandRow` (Phase 06 Task 6.4 shapes), `StockCard` and `StockCardRow` (Task 6.5). `api.ts`: `stockOnHand(params)` and `stockCard(params)`. `keys.ts` already exists (Task 10.1). `hooks.ts`: `useStockOnHand(params)` and `useStockCard(params)` (enabled when `productId` is set).
- `stock-on-hand-page.tsx`, route `/inventory/stock-on-hand` (`reports.inventory`):
  - Filters: Thời điểm (`datetime-local`, empty = `Hiện tại`; sent as `at=fromDateTimeLocalValue(value)`, an ISO instant), Kho, Nhóm hàng (the lookup hook from `@/features/products/hooks` — not the paged `useProductGroups` of `features/product-groups`), Tìm kiếm.
  - Table: Mã hàng · Tên hàng · Nhóm · ĐVT · Kho · Số lượng (`formatStockQuantity`) · Giá trị (only when `canViewCost`; under Branch scope the backend splits the branch value by quantity, D32).
  - Footer `Tổng giá trị` (only when `canViewCost`). A badge `Giá trị tạm tính` when `isProvisional`.
  - Clicking a row navigates to `/inventory/stock-card?productId=…&warehouseId=…`.

1. **Write the failing test** `pages/inventory/stock-on-hand-page.test.tsx`: `renders rows, provisional badge and opens the stock card on row click`; `hides value column and total without view cost`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/inventory/stock-on-hand-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** feature module, page, route.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock-on-hand report screen"`

### Task 10.3 — Stock card screen

`stock-card-page.tsx`, route `/inventory/stock-card` (`reports.inventory`):
- Filters: Hàng hóa (`ProductTypeaheadCell`; initial value from `?productId=`), Kho (optional, `?warehouseId=`), Từ ngày / Đến ngày (default = `firstDayOfMonthYmd()` .. `todayYmd()`, local VN dates).
- Summary: Tồn đầu (SL / giá trị) · Nhập · Xuất · Tồn cuối, plus the `Giá vốn tạm tính` badge when `isProvisional`.
- Rows: Ngày giờ (instant shown in local VN time) · Số CT (a `Link` to `/stock-in/{sourceId}` or `/stock-out/{sourceId}`; opening rows show `reasonName` (`Tồn đầu kỳ`) and no link) · Lý do · Đối tượng · Kho · Nhập · Xuất · Tồn (`formatStockQuantity`) · Đơn giá · Giá trị nhập · Giá vốn xuất · Giá trị tồn. Value columns (Đơn giá onwards) only when `canViewCost`. When `valuesAtScopeOnly` (a warehouse filter under Branch scope, D32), `Giá trị tồn` and the opening/closing values are hidden and the note `Giá trị tồn tính theo chi nhánh — bỏ lọc kho để xem` is shown.

1. **Write the failing test** `pages/inventory/stock-card-page.test.tsx`: `reads product from the URL and renders opening, rows (voucher codes linked by type, opening rows unlinked) and closing`; `value columns hidden without view cost`; `values-at-scope-only hides running values and shows the note`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/inventory/stock-card-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** page and route.
4. **Run the tests to verify they pass:** `npx vitest run src/pages/inventory`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock card screen"`

## Verification

- `cd frontend && npm run typecheck`
- `npm run lint`
- `npx vitest run src/pages/inventory src/features`
- `npm run test` (no new failures compared with the baseline)
- `npm run build`

## Exit Criteria

- Opening stock can be entered per warehouse, with the negative-stock flow.
- The stock-on-hand and stock-card screens show correct data, hide values without `inventory.view_cost`, and label provisional costs.
- Every "Kho" sidebar entry opens a working screen.
