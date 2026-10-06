# Phase 09 — Frontend stock voucher list & form

**Status:** [-] in progress (Tasks 9.1–9.2 started early, parallel agent)
**Complexity:** XL

## Objective

Deliver the stock-in and stock-out screens, modelled on the quotation list and form. Both directions share one list page and one form page parameterised by `type`. The form has a header card, a line grid with typeahead autofill and dimension columns per `PricingMode`, a per-line warehouse, the stock at the voucher's time, a totals panel, a negative-stock confirmation flow, a read-only view for cancelled vouchers, an unsaved-draft store and Ctrl+S. The list has filters, a persisted status filter, resizable columns and a totals footer.

## Files

- `frontend/src/lib/pricing-quantity.ts`, `pricing-quantity.test.ts` (new)
- `frontend/src/lib/vn-datetime.ts`, `vn-datetime.test.ts` (new — review M12)
- `frontend/src/lib/round.ts`, `round.test.ts` (new — `roundAwayFromZero`, review m24)
- `frontend/src/lib/stock-quantity.ts` (new — `formatStockQuantity`, up to 6 decimals, review m27)
- `frontend/src/pages/quotations/utils/compute-line.ts` (modify — delegate quantity)
- `frontend/src/pages/stock-vouchers/utils/compute-stock-line.ts`, `compute-stock-line.test.ts` (new)
- `frontend/src/features/stock-vouchers/{types,api,keys,hooks,schema,payload}.ts`, `schema.test.ts`, `payload.test.ts` (new)
- `frontend/src/features/stock-vouchers/use-stock-voucher-draft.ts`, `use-stock-voucher-draft.test.ts` (new)
- `frontend/src/components/partner-autocomplete/partner-autocomplete.tsx`, `partner-autocomplete.test.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/{stock-line-grid,stock-totals-panel,negative-stock-dialog,stock-list-footer,stock-voucher-activity-history}.tsx` + tests (new)
- `frontend/src/pages/stock-vouchers/{stock-voucher-form-page,stock-voucher-list-page}.tsx` + tests (new)
- `frontend/src/pages/stock-vouchers/labels.ts` (new — direction-specific labels)
- `frontend/src/stores/ui-store.ts`, `ui-store.test.ts` (modify — persisted status filter per type)
- `frontend/src/App.tsx` (modify)

## Reference files (read-only)

- `frontend/src/pages/quotations/quotation-form-page.tsx` — toolbar, save intents, Ctrl+S, draft banner, `toFormDefaults`/`toPayload`
- `frontend/src/pages/quotations/components/{line-items-grid,totals-panel,product-typeahead-cell,list-footer,quotation-date-filter}.tsx`
- `frontend/src/pages/quotations/quotation-list-page.tsx` — filters in search params, persisted status filter, column sizing, footer
- `frontend/src/features/quotations/use-quotation-draft.ts`
- `frontend/src/components/customer-autocomplete/customer-autocomplete.tsx`
- Phase 05 API contract

## Labels (`pages/stock-vouchers/labels.ts`)

| Key | In | Out |
|---|---|---|
| `title` | Phiếu nhập kho | Phiếu xuất kho |
| `listTitle` | Danh sách phiếu nhập kho | Danh sách phiếu xuất kho |
| `dateLabel` | Ngày nhập | Ngày xuất |
| `reasonLabel` | Lý do nhập | Lý do xuất |
| `handlerLabel` | Người giao hàng | Người nhận hàng |
| `basePath` | `/stock-in` | `/stock-out` |
| `permissionPrefix` | `stock_in` | `stock_out` |

## Tasks

### Task 9.1 — Shared pricing quantity on the frontend

```ts
// lib/pricing-quantity.ts
export interface PricingQuantityInput { pricingMode: PricingMode; sheetCount?: number; length?: number; width?: number; thickness?: number; quantity: number; }
export function computePricingQuantity(input: PricingQuantityInput): number; // same formulas as backend PricingQuantity
```

1. **Write the failing test** `lib/pricing-quantity.test.ts` — the same five cases as backend `PricingQuantityTests`.
2. **Run the test to verify it fails:** `npx vitest run src/lib/pricing-quantity.test.ts`. Expected: FAIL (module missing).
3. **Write the minimal implementation:** the function. `compute-line.ts` `computeLineQuantity` now returns `computePricingQuantity(line)`.
4. **Run the tests to verify they pass:** `npx vitest run src/lib/pricing-quantity.test.ts src/pages/quotations/utils`. Expected: PASS.
5. **Commit:** `git commit -m "refactor(frontend): share pricing quantity between quotations and inventory"`

### Task 9.2 — `computeStockVoucher` preview calculator

```ts
// pages/stock-vouchers/utils/compute-stock-line.ts
export interface StockLineLike {
  trackInventory: boolean; pricingMode: PricingMode; priceIncludesVat: boolean;
  sheetCount?: number; length?: number; width?: number; thickness?: number; quantity: number;
  unitPrice: number; discountRate: number; discountAmount?: number; discountManual: boolean; vatRate: number;
}
export interface StockHeaderLike { direction: 'In' | 'Out'; freight: number; orderDiscount: number; netExcludesVat: boolean; }
export interface StockLineComputed { quantity: number; amount: number; discountAmount: number; orderDiscountAllocated: number; vatAmount: number; netAmount: number; freightAllocated: number; }
export interface StockTotals { goodsAmount: number; lineDiscountTotal: number; discountTotal: number; vatTotal: number; total: number; }
export function computeStockVoucher(header: StockHeaderLike, lines: StockLineLike[]): { lines: StockLineComputed[]; totals: StockTotals };
```

Implement the exact algorithm of backend `StockVoucherCalculator` (Phase 03 Task 3.3) with `roundAwayFromZero(x, 0)` for money and `roundAwayFromZero(x, 6)` for quantity (new `lib/round.ts`: sign × `Math.round(|x| × 10^d)` with a relative epsilon, matching .NET `MidpointRounding.AwayFromZero`; the quotation `round0` — `Math.round(x + EPSILON)` — rounds −2.5 to −2 and is left untouched), the same remainder rules. The backend stays authoritative; this is only a live preview.

1. **Write the failing tests:**
   - `lib/round.test.ts`: `-2.5 → -3`, `2.5 → 3`, `1.005 at 2 digits → 1.01`, an exact .5 VAT case (`Net′ 105 × 10% = 10.5 → 11`).
   - `compute-stock-line.test.ts` — port every Phase 03 Task 3.3 case with identical numbers (Out example, In example, manual discount, NetExcludesVat, VAT-inclusive flag ignored on In, remainder to the last positive line, six-decimal quantity, freight without tracked lines).
2. **Run the test to verify it fails:** `npx vitest run src/lib/round.test.ts src/pages/stock-vouchers/utils`. Expected: FAIL.
3. **Write the minimal implementation:** `roundAwayFromZero` and `computeStockVoucher`.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher preview calculator matching backend rules"`

### Task 9.3 — `stock-vouchers` feature module, schema, payload and date-time helpers

- `lib/vn-datetime.ts` (review M12) — the API returns instants (`timestamptz`, usually `+00:00`); the browser runs in VN time (D14):
  - `toDateTimeLocalValue(iso: string): string` → `format(new Date(iso), "yyyy-MM-dd'T'HH:mm")` (date-fns, local time);
  - `fromDateTimeLocalValue(local: string): string` → `new Date(local).toISOString()`;
  - `todayYmd()` / `firstDayOfMonthYmd()` built on local date parts (like `features/dashboard/format.ts` `formatDateYmd`).
  Never copy `toISOString().slice(0, 10)` from `quotation-form-page.tsx` / `sales-revenue-page.tsx`: it gives the previous day before 07:00 VN.
- `types.ts`: mirrors the Phase 05 DTOs — `StockVoucher`, `StockVoucherLine`, `StockVoucherListItem`, `StockVoucherListParams` (`type`, `page`, `pageSize`, `search`, `from`, `to`, `warehouseId`, `partnerId`, `reasonId`, `status`, `ownerUserIds: string[]`, `sortBy`, `sortDirection`), `StockVoucherListResult` (extends the shared paged result: `items`, `page`, `pageSize`, `totalItems`, `totalPages`, `hasNextPage`, `hasPreviousPage`, plus `aggregates`), `StockVoucherDefaults`, `StockAtRequest` / `StockAtResult`, `UpsertStockVoucherRequest`, `StockVoucherActionRequest`, `StockVoucherActivity`, `StockVoucherStatus = 'Active' | 'Cancelled'`, `StockVoucherStatusFilter = StockVoucherStatus | 'all'`. Import `StockDirection` and `PartnerType` from `@/features/stock-reasons/types` (Phase 08 Task 8.2); do not redeclare them.
- `api.ts`: `list` (joins `ownerUserIds` with commas; status `'all'` → no `status` param), `owners(type)`, `defaults(type, voucherAt?)` (sends `fromDateTimeLocalValue(voucherAt)`), `stockAt(body)` (`at` as an ISO instant), `partners(type, keyword, reasonId?)`, `get`, `activities`, `create`, `update`, `cancel(id, body)`, `restore(id, body)`, `remove(id, body)` (DELETE with `version` and `acknowledgeNegativeStock` as query params).
- `keys.ts`: a shared inventory root `inventoryKeys.all = ['inventory'] as const` (exported from `features/stock-vouchers/keys.ts`; Phase 10 nests opening stock and reports under it), and `stockVoucherKeys.{all = [...inventoryKeys.all, 'stock-vouchers'], lists, list(params), details, detail(id), activities(id), owners(type), defaults(type, voucherAt), stockAt(body), partners(...)}`.
- `hooks.ts`: queries (`useStockVouchers` with `keepPreviousData`, `useStockVoucher`, `useStockVoucherActivities(id, enabled)`, `useStockVoucherOwners`, `useStockVoucherDefaults` (`placeholderData: keepPreviousData`), `useStockAt` (enabled when there are items), `usePartnerSearch(type, keyword, reasonId?)` (enabled when the trimmed keyword has ≥ 1 character)) and mutations (`useCreateStockVoucher`, `useUpdateStockVoucher`, `useCancelStockVoucher`, `useRestoreStockVoucher`, `useDeleteStockVoucher`). Every mutation invalidates `inventoryKeys.all` (lists, detail, activities, stock-at, defaults, and the Phase 10 reports and opening stock).
- `schema.ts` (zod):
  - line: `_uiKey`, `id?`, `sortOrder`, `productId` (uuid, "Chọn hàng hóa"), `productCode`, `productName`, `pricingMode`, `unitName`, `trackInventory`, `priceIncludesVat`, `warehouseId` (uuid), `sheetCount` / `length` / `width` / `thickness` (`optionalNumber`), `quantity`, `unitPrice` (≥ 0), `discountRate` (0..100), `discountAmount?`, `discountManual`, `vatRate` (0..100), `note`. A `superRefine` requires dimensions per pricing mode (same paths as the backend) and `computePricingQuantity > 0` ("Số lượng phải lớn hơn 0").
  - line `discountAmount` (manual) between 0 and the computed line amount ("Tiền CK không vượt quá số tiền").
  - header: `voucherAt` (non-empty, `datetime-local` string), `warehouseId`, `reasonId` (uuid, "Chọn lý do"), `partnerId?`, `partnerName?`, `partnerAddress?`, `partnerTaxCode?`, `handlerName?`, `paymentMethodId?`, `note?`, `freight ≥ 0`, `orderDiscount ≥ 0`, `paidAmount?`, `paidAmountTouched: boolean`, `version?: number` (D29), `lines` (min 1, "Phiếu phải có ít nhất 1 dòng").
- `payload.ts`: `toUpsertPayload(type, values, acknowledgeNegativeStock = false): UpsertStockVoucherRequest`. It converts `voucherAt` with `fromDateTimeLocalValue`, drops `_uiKey`, takes `version` from `values.version` (D29 — never from a later refetch), sends `paidAmount` when `paidAmountTouched` **or** when editing (`values.version !== undefined`), and sends `quantity` only for `PerUnit` (0 otherwise). Also `toFormDefaults(voucher?: StockVoucher, defaults?: StockVoucherDefaults): StockVoucherFormValues`: `voucherAt = toDateTimeLocalValue(voucher?.voucherAt ?? defaults.voucherAt)`; for an existing voucher `version = voucher.version`, `paidAmount = voucher.paidAmount` and `paidAmountTouched = voucher.paidAmount !== voucher.total` (a partial payment is kept — review M13); new vouchers start untouched.

1. **Write the failing tests:**
   - `lib/vn-datetime.test.ts` (with `TZ` pinned to `Asia/Ho_Chi_Minh` in the Vitest config or via `process.env.TZ` in the test setup): `'2026-10-31T22:00:00+00:00' → '2026-11-01T05:00'`; round-trip keeps the instant; `firstDayOfMonthYmd` on 2026-10-01 06:00 VN → `2026-10-01`.
   - `features/stock-vouchers/schema.test.ts`: `requires width for square-meter lines`; `requires quantity > 0 for per-unit lines`; `requires at least one line`; `rejects a manual discount above the amount`; `accepts a valid voucher`.
   - `features/stock-vouchers/payload.test.ts`: `maps form values to request with ISO voucherAt`; `omits paidAmount for an untouched new voucher`; `edit of a partially paid voucher keeps paidAmount`; `edit round-trip keeps voucherAt unchanged` (server `2026-10-06T02:00:00+00:00` → form `2026-10-06T09:00` → payload `2026-10-06T02:00:00.000Z`); `version comes from the form values`; `sends zero quantity for dimension-based lines`; `toFormDefaults uses defaults for new vouchers and voucher values for edits`.
2. **Run the tests to verify they fail:** `npx vitest run src/lib/vn-datetime.test.ts src/features/stock-vouchers`. Expected: FAIL.
3. **Write the minimal implementation:** `lib/vn-datetime.ts` and the module files (`getApiError` already exists since Phase 07 Task 7.2).
4. **Run the tests to verify they pass:** same command plus `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher feature module, schema and payload mapping"`

### Task 9.4 — Partner autocomplete

`components/partner-autocomplete/partner-autocomplete.tsx`, props `{ type: StockDirection; reasonId?: string; partnerType?: PartnerType; requireReason?: boolean /* default true */; value: { id: string; code: string; name: string } | null; onSelect: (p: CustomerSearchItem | null) => void; disabled?: boolean; inputId?: string; placeholder?: string }`. It follows the `customer-autocomplete.tsx` interaction (debounced input, listbox, keyboard navigation, clear button) but searches with `usePartnerSearch(type, keyword, reasonId)`. When `requireReason` is true (form mode) it is disabled with placeholder `Chọn lý do trước` until `reasonId` is set, and disabled with `Không cần đối tượng` when `partnerType === 'None'`. With `requireReason={false}` (list filter mode) it is always enabled and searches every role.

1. **Write the failing test** `partner-autocomplete.test.tsx` (mock `@/features/stock-vouchers/hooks`): `is disabled until a reason is chosen`; `is disabled for partner type None`; `filter mode (requireReason=false) is enabled without a reason`; `lists results and selects with Enter`; `clear button calls onSelect(null)`.
2. **Run the test to verify it fails:** `npx vitest run src/components/partner-autocomplete`. Expected: FAIL.
3. **Write the minimal implementation:** the component.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): partner autocomplete filtered by stock reason"`

### Task 9.5 — Stock line grid

`stock-line-grid.tsx`, props `{ form: UseFormReturn<StockVoucherFormValues>; type: StockDirection; warehouses: Warehouse[]; computed: StockLineComputed[]; stockAt: Record<string, number>; readOnly: boolean }`. `stockAt` is keyed `${productId}:${warehouseId}`.

Columns: STT · Mã hàng (`ProductTypeaheadCell`) · Tên hàng · ĐVT · Kho (Select) · Số tấm · Dài · Rộng · Dày · SL · SL tồn · Đơn giá · Số tiền · %CK · Tiền CK · %VAT · Tiền VAT · Số còn lại · Ghi chú · delete. Numeric columns are right-aligned with `tabular-nums` (conventions).

Behaviour:
- **Product select** (one rule for every selection path, section-02 #9): set `productId`, `productCode`, `productName`, `pricingMode`, `trackInventory`, `priceIncludesVat`, and `unitName` (`m` / `m²` / `m³` for metre modes, else `unitName`). Set `unitPrice` (In → `costPrice ?? 0`, Out → `defaultPrice ?? 0`), `discountRate` (In → `purchaseDiscountRate`, Out → `salesDiscountRate`) and `vatRate` (`defaultTaxRate ?? 0`). Copy `length` / `width` / `thickness`, clear `sheetCount`, set `quantity = 0`, `discountManual = false`, and `warehouseId` = the header warehouse when empty.
- **Enabled inputs:** `Số tấm` for non-PerUnit; `Dài` for Linear / Square / Cubic; `Rộng` for Square / Cubic; `Dày` for Cubic. `SL` is editable only for PerUnit; otherwise it shows `computed[i].quantity`.
- **Discounts:** editing `%CK` sets `discountManual = false`; editing `Tiền CK` sets `discountManual = true` and `discountAmount`.
- **Read-only:** `SL tồn` shows `stockAt` for tracked lines, `—` for untracked ones; `Số tiền` / `Tiền VAT` / `Số còn lại` come from `computed`.
- **Quantity display:** `SL` and `SL tồn` use `formatStockQuantity` (`lib/stock-quantity.ts`, `maximumFractionDigits: 6`); do not copy `line-items-grid.tsx`'s two-decimal quantity formatter, which would show 0.003659 m³ as `0,00`.
- **Selection paths:** the typeahead and its "Xem danh mục đầy đủ" catalog dialog both deliver a `ProductSuggestion` built by `toProductSuggestion` (Phase 08 Task 8.5), so one autofill rule covers both.
- `readOnly` disables every input and hides add/delete.
- Row navigation reuses the Enter/Tab approach of `line-items-grid.tsx` (ids `stock-line-{field}-{idx}`).

1. **Write the failing test** `stock-line-grid.test.tsx` (render inside a small harness that creates `useForm` with `toFormDefaults()`; mock `useProductSearch` via `@/features/products/hooks`):
   - `selecting a product on a stock-in fills cost price, purchase discount, VAT and dimensions`
   - `selecting a product on a stock-out fills sales price and sales discount`
   - `dimension inputs follow the pricing mode` (PerUnit / PerLinearMeter / PerSquareMeter / PerCubicMeter)
   - `editing discount amount switches the line to manual discount`
   - `shows stock at voucher time for tracked lines and a dash for untracked ones`
   - `small cubic quantities keep six decimals` (0.003659)
   - `selecting through the catalog dialog applies the same autofill` (mock the catalog list hook; double-click a row)
   - `read-only grid disables inputs`
2. **Run the test to verify it fails:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** the grid.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher line grid"`

### Task 9.6 — Totals panel

`stock-totals-panel.tsx`, props `{ type; totals: StockTotals; freight; orderDiscount; paidAmount; onFreightChange; onOrderDiscountChange; onPaidAmountChange; onApplyVatToAll?: (rate: number) => void; readOnly }`. Rows: Tiền hàng · Tổng chiết khấu · Tổng VAT · Phí vận chuyển (editable) · CK cả đơn (editable) · **Tổng thanh toán** · Số tiền thanh toán (editable). For stock-in only, a `% VAT cho mọi dòng` input with an `Áp dụng` button (section-02 #2). Reuse `pages/quotations/utils/money-input.ts`.

1. **Write the failing test** `stock-totals-panel.test.tsx`: `renders totals and calls handlers for freight, order discount and paid amount`; `VAT-to-all input exists only for stock-in and calls onApplyVatToAll`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/stock-vouchers/components/stock-totals-panel.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** the panel.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher totals panel"`

### Task 9.7 — Negative-stock dialog

`negative-stock-dialog.tsx`, props `{ open; blocked: boolean; shortages: Array<{ key: string; messages: string[] }>; confirmLabel?: string; onConfirm: () => void; onClose: () => void }`. Title `Cảnh báo xuất âm kho` (warning) or `Không đủ tồn kho` (blocked). The list renders `key` as `Mã hàng @ Kho` plus the messages. Buttons: warning → `confirmLabel` (default `Vẫn lưu`; the form passes `Vẫn hủy phiếu`, `Vẫn xóa` or `Vẫn khôi phục` for those actions) and `Quay lại`; blocked → `Đóng` only. A helper `toShortages(details?: Record<string, string[]>)` lives in the same file.

1. **Write the failing test** `negative-stock-dialog.test.tsx`: `warning shows shortages (via toShortages) and a confirm button`; `custom confirm label`; `blocked shows only close`.
2. **Run the test to verify it fails:** `npx vitest run src/pages/stock-vouchers/components/negative-stock-dialog.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** the component.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): negative stock confirmation dialog"`

### Task 9.8 — Unsaved draft for new vouchers

`use-stock-voucher-draft.ts` mirrors `use-quotation-draft.ts` (1500 ms debounce, only when `!isEdit`). The key is `stock_voucher_draft_${type}_${userId}_${branchId}` (a draft holds warehouses of one branch, so it must not be restored under another branch); it stores `{ savedAt, values, selectedPartner }`. Every `localStorage` access, including `removeItem`, is wrapped in try/catch. It exports `readStockVoucherDraft`, `writeStockVoucherDraft`, `deleteStockVoucherDraft` and `useStockVoucherDraft({ form, type, userId, branchId, isEdit, getSelectedPartner, initialHasDraft, initialSavedAt })`.

1. **Write the failing test** `use-stock-voucher-draft.test.ts`: `writes a per-type, per-branch draft after debounce for new vouchers and clearDraft removes it`; `a draft of another branch is not read`; `does not write in edit mode`.
2. **Run the test to verify it fails:** `npx vitest run src/features/stock-vouchers/use-stock-voucher-draft.test.ts`. Expected: FAIL.
3. **Write the minimal implementation:** the hook and helpers.
4. **Run the test to verify it passes:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): unsaved draft for new stock vouchers"`

### Task 9.9 — Stock voucher form page

`stock-voucher-form-page.tsx`: `export function StockVoucherFormPage({ type }: { type: StockDirection })`. Route params: `id` (`'new'` or uuid).

Behaviour:
- **New:** the first `useStockVoucherDefaults(type)` response (no `voucherAt`) seeds `voucherAt`, `warehouseId`, `reasonId` and `paymentMethodId`; only that first load gates the form mount, and a restored draft wins over defaults (as on the quotation form). The header shows `Số phiếu dự kiến: {nextCode}`. When `voucherAt` changes, a separate defaults query with that date (debounced 400 ms, `placeholderData: keepPreviousData`) updates **only** the `Số phiếu dự kiến` text — it never resets or unmounts the form (review m5). The draft banner is shown as on the quotation form.
- **Edit:** `useStockVoucher(id)`; read-only when `status === 'Cancelled'` or `!canEdit`. A Cancelled voucher shows the badge `Đã hủy`. `form.reset(toFormDefaults(data))` runs when the id changes and, after that, only when `data.version` changes **and** the form is not dirty (D29).
- **Header card "Thông tin chung":**
  - Số phiếu (read-only);
  - `{dateLabel}` (`datetime-local`);
  - Kho (`useWarehouses()`, active only);
  - `{reasonLabel}` (`useStockReasons({ direction: type })`) — changing it to a reason with `partnerType === 'None'` clears the partner;
  - Đối tượng (`PartnerAutocomplete`; selecting fills `partnerName` / `partnerAddress` / `partnerTaxCode`, all editable);
  - `{handlerLabel}` (text);
  - Hình thức thanh toán (`usePaymentMethods()`);
  - Ghi chú.
- **Computation:** `useWatch` on the lines and header → `computeStockVoucher` (`netExcludesVat` from `useInventorySettings()`). `paidAmount` follows `totals.total` until the user edits it (`paidAmountTouched`).
- **Stock at time:** `useStockAt({ type, at: fromDateTimeLocalValue(voucherAt), excludeVoucherId: isEdit ? id : undefined, items })` for the distinct tracked `(productId, warehouseId)` pairs (`'new'` is not a Guid and would fail binding — review m5).
- **Toolbar (sticky, quotation pattern):**
  - new: `Lưu tạm` (save-stay → navigate replace to `{basePath}/{id}`) and `Lưu và thoát` (→ `{basePath}`);
  - edit: `Cập nhật`;
  - `Hủy phiếu` (`canCancel && status === 'Active'`), `Khôi phục` (`canCancel && status === 'Cancelled'`), `Xóa` (`canDelete && status === 'Active'`) — each behind a `ConfirmDialog`, sending `{ version: values.version }`.
  - `Ctrl+S` triggers save-stay (new) or Cập nhật (edit).
  - Icon colours per the conventions (save blue, cancel/delete red, restore emerald).
- **Errors:** read with `getApiError` (Phase 07 Task 7.2). Keep the pending action in state (`{ kind: 'save' | 'cancel' | 'restore' | 'delete', version }`) so a confirmation resends exactly that action.
  - `NEGATIVE_STOCK_WARNING` → open `NegativeStockDialog` with the action's label (`Vẫn lưu` / `Vẫn hủy phiếu` / `Vẫn khôi phục` / `Vẫn xóa`); on confirm, resend the pending action with `acknowledgeNegativeStock: true` (in the query string for DELETE).
  - `NEGATIVE_STOCK_BLOCKED` → blocked dialog.
  - `CONCURRENCY` → refetch, then `form.reset(toFormDefaults(fresh))` and show the destructive toast `Phiếu đã được người khác cập nhật. Đã tải lại dữ liệu — các thay đổi chưa lưu đã bị bỏ.` (D29 — never resend the stale values with the new version).
  - `VALIDATION` with `details` → map `lines[i].field` keys (camelCase, normalized by `getApiError`) onto the form errors (`lines.${i}.${field}`); other keys go to a toast via `formatApiErrorDetails`.
  - Anything else → `formatApiErrorDetails` toast.
- **History (edit mode):** a `Lịch sử` card below the grid renders `useStockVoucherActivities(id)` with `stock-voucher-activity-history.tsx` (same pattern as `QuotationActivityHistory`; labels for Created, Updated, Cancelled, Restored, Deleted) — review m8.
- **Stock-in:** `onApplyVatToAll(rate)` sets `vatRate` on every line.
- **Product picker permission:** the line grid's typeahead calls `/products/search` (`products.view`); WAREHOUSE has it by default — note it for custom roles (review m25).

1. **Write the failing test** `stock-voucher-form-page.test.tsx` (mock `react-router-dom` `useParams`/`useNavigate`, `@/features/stock-vouchers/hooks`, `@/features/warehouses/hooks`, `@/features/stock-reasons/hooks`, `@/features/payment-methods/hooks`, `@/features/inventory-settings/hooks`, `@/features/products/hooks` and `@/stores/auth-store`):
   - `new stock-in shows title, expected code and seeded defaults`
   - `reason list contains only reasons of the voucher direction`
   - `saving sends the mapped payload through create` (fill one PerUnit line → `mutateAsync` receives `type: 'In'`, ISO `voucherAt`, the line fields)
   - `negative stock warning asks for confirmation and resends with acknowledgement` (first `mutateAsync` rejects with an axios-shaped 422 `NEGATIVE_STOCK_WARNING`; click `Vẫn lưu` → second call has `acknowledgeNegativeStock: true`)
   - `blocked negative stock shows the blocked dialog without resend`
   - `cancel warning resends cancel with acknowledgement` (label `Vẫn hủy phiếu`; same `version`)
   - `delete warning resends DELETE with acknowledgeNegativeStock=true`
   - `concurrency conflict resets the form to the server data and does not resend stale values`
   - `validation details map lines[0].width onto the grid cell`
   - `Ctrl+S saves`
   - `new voucher sends no excludeVoucherId to stock-at`
   - `edit shows the activity history`
   - `cancelled voucher is read-only and offers only restore`
   - `stock-out uses Ngày xuất, Lý do xuất and Người nhận hàng labels`
2. **Run the test to verify it fails:** `npx vitest run src/pages/stock-vouchers/stock-voucher-form-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** `labels.ts` and the page. Add routes `/stock-in/new` (`stock_in.create`) and `/stock-in/:id` (`stock_in.view`; the page hides save actions without `edit`), mirroring how `/quotations/new` is guarded, and the same for `/stock-out` with `stock_out.*`.
4. **Run the tests to verify they pass:** `npx vitest run src/pages/stock-vouchers src/features/stock-vouchers src/components/partner-autocomplete`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher form for stock-in and stock-out"`

### Task 9.10 — Stock voucher list page

`stock-voucher-list-page.tsx`: `export function StockVoucherListPage({ type }: { type: StockDirection })`.

- **Filters** (search params, quotation pattern): `q` (search), `from` / `to` (date presets as in `quotation-date-filter.tsx`), `warehouseId`, `partnerId` (`PartnerAutocomplete` with `requireReason={false}`; the selected partner's name is kept in `partnerName` so the chip survives reloads), `reasonId`, `status` (`Active` / `Cancelled` / all), `owners` (multi-select from `useStockVoucherOwners(type)`), `page`, `size`.
- **Persisted status filter:** `ui-store` gains `stockVoucherStatusFilter: { In: StockVoucherStatusFilter | null; Out: StockVoucherStatusFilter | null }` and `setStockVoucherStatusFilter(type, value)` (add it to `partialize`). `'all'` is a real value (stored and written to the URL as `status=all`), mapped to "no status param" by `api.ts` — `null` only means "not set". The URL wins, then the store, then the default `Active`. When `'all'` is selected, the footer notes that totals exclude cancelled vouchers (backend aggregates rule).
- **Columns** (resizable, `useColumnSizingPersist('stock-voucher-list-' + type)`): Số phiếu · Ngày giờ · Kho · Đối tượng · Lý do · HTTT · Tiền hàng · Chiết khấu · VAT · Phí VC · Tổng TT · Đã TT · Trạng thái · Người tạo. Money columns are right-aligned with `tabular-nums`. Clicking a row navigates to `{basePath}/{id}`.
- **Footer:** `stock-list-footer.tsx` — a copy of `list-footer.tsx` typed to the voucher aggregates (the quotation footer is typed to `QuotationListAggregates` and labelled for quotations, so it cannot be reused as is), with the pager fed by `totalPages` / `hasNextPage`.
- The header button `Tạo {title}` is visible with `{permissionPrefix}.create`.

1. **Write the failing tests:**
   - `stores/ui-store.test.ts`: `persists stock voucher status filter per type`; `a stored all is restored`.
   - `stock-voucher-list-page.test.tsx` (mock hooks):
     - `renders rows and footer aggregates`
     - `passes filters from the URL (or the persisted status when the URL has none) to useStockVouchers` (render with `MemoryRouter initialEntries={['/stock-in?from=2026-10-01&to=2026-10-31&status=Cancelled']}`)
     - `selecting a partner filter writes partnerId to the URL and passes it to useStockVouchers`
     - `stock-out list uses stock-out labels and path, and hides create without stock_out.create`
     - `status all sends no status filter`
2. **Run the tests to verify they fail:** `npx vitest run src/stores/ui-store.test.ts src/pages/stock-vouchers/stock-voucher-list-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** the store change, the page, and routes `/stock-in` and `/stock-out` (index, guarded by `stock_in.view` / `stock_out.view`).
4. **Run the tests to verify they pass:** same command plus `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher list with filters and totals"`

## Verification

- `cd frontend && npm run typecheck`
- `npm run lint`
- `npx vitest run src/lib src/stores src/features/stock-vouchers src/components/partner-autocomplete src/pages/stock-vouchers src/pages/quotations`
- `npm run test` (no new failures compared with the baseline)
- `npm run build`
- Manual (backend + frontend dev servers): create, edit, cancel and restore a stock-in and a stock-out voucher; trigger the negative-stock warning; confirm the preview totals equal the saved totals.

## Exit Criteria

- Both directions have a working list and form that match the backend contract.
- Preview totals equal backend totals for every Phase 03 example.
- The negative-stock warning/blocked flows, concurrency conflicts, cancelled read-only mode, the draft and Ctrl+S behave as specified.
- Quotation screens are unaffected (quotation tests green).
