# Phase 11 — Frontend cash vouchers, fund transfers & stock voucher form

**Status:** [x] complete
**Complexity:** XL

## Objective

Build the money documents on the frontend:

- the receipt and payment list and form, following the quotation/stock voucher pattern: header, line grid, totals, **allocation grid** with an earliest-due suggestion, read-only mode for vouchers managed by a stock voucher, the generic confirmation flow, concurrency handling and activity history;
- the fund transfer list and form.

Then update the stock voucher form:

- generic confirmations instead of the negative-stock-only flow;
- fund select and due date;
- the paid-amount default that follows the payment method's fund kind (P9);
- a link to the automatic receipt/payment;
- the customer debt panel (P6).

## Files

- `frontend/src/features/cash-vouchers/{api,hooks,keys,types,schema,payload,allocation}.ts` (+ `schema.test.ts`, `payload.test.ts`, `allocation.test.ts`, `hooks.test.tsx`) (new)
- `frontend/src/features/debt/{api,hooks,types,keys}.ts` (modify — open items, partner summary)
- `frontend/src/features/fund-transfers/{api,hooks,keys,types,schema,payload}.ts` (+ `payload.test.ts`) (new)
- `frontend/src/components/partner-autocomplete/partner-autocomplete.tsx` (+ test) (modify — optional `useSearch`)
- `frontend/src/pages/cash-vouchers/{cash-voucher-list-page,cash-voucher-form-page,labels}.tsx|ts` (+ tests) (new)
- `frontend/src/pages/cash-vouchers/components/{cash-line-grid,allocation-grid,cash-voucher-header}.tsx` (+ `allocation-grid.test.tsx`, `cash-line-grid.test.tsx`) (new)
- `frontend/src/pages/fund-transfers/{fund-transfer-list-page,fund-transfer-form-page}.tsx` (+ tests) (new)
- `frontend/src/features/stock-vouchers/{types,schema,payload,hooks}.ts` (+ tests) (modify)
- `frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx` (+ test) (modify)
- `frontend/src/pages/stock-vouchers/components/{stock-totals-panel.tsx,customer-debt-panel.tsx (new)}` (+ tests)
- `frontend/src/App.tsx` (modify — routes)

## Reference files (read-only)

- `frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx` — loader wrapper, `PendingAction`, `runAction`, `handleError`, `resetTo`, `lastVersionRef`, `submitWithIntent`, Ctrl+S
- `frontend/src/pages/stock-vouchers/stock-voucher-list-page.tsx` — URL-state filters, table helpers, footer
- `frontend/src/features/stock-vouchers/{payload,schema,hooks}.ts` and their tests
- `frontend/src/pages/stock-vouchers/components/stock-voucher-activity-history.tsx` — reused as is

## Labels

| Item | Receipt | Payment |
|---|---|---|
| title / listTitle | Phiếu thu / Danh sách phiếu thu | Phiếu chi / Danh sách phiếu chi |
| personLabel | Người nộp | Người nhận |
| descriptionLabel | Lý do nộp | Lý do chi |
| basePath | `/receipts` | `/payments` |
| permissionPrefix | `receipts` | `payments` |
| managed banner | "Phiếu thu tự động theo phiếu xuất {code} — sửa trên phiếu kho." | "Phiếu chi tự động theo phiếu nhập {code} — sửa trên phiếu kho." |

## Tasks

### Task 11.1 — Cash voucher feature: API, hooks, schema, payload

- `types.ts` mirrors the Phase 05 DTOs (`CashVoucher`, `CashVoucherLine`, `CashVoucherAllocation`, `CashVoucherListItem`, `CashVoucherListResult`, `CashVoucherListParams`, `CashVoucherDefaults`, `UpsertCashVoucherRequest`, `CashDocumentActionRequest`).
- `keys.ts`: `cashVoucherKeys` under `[...cashKeys.all, 'cash-vouchers']` (lists, list(params), detail(id), activities(id), defaults(type), partners(type, keyword, reasonId)).
- `api.ts`: `cashVouchersApi` with list, defaults, partners, get, activities, create, update, cancel, restore, remove (query params like stock vouchers).
- `hooks.ts`:
  - `useCashVouchers`, `useCashVoucher`, `useCashVoucherActivities`, `useCashVoucherDefaults(type, { fresh })`, `useCashPartnerSearch(type, keyword, reasonId)`;
  - mutations `useCreateCashVoucher`, `useUpdateCashVoucher`, `useCancelCashVoucher`, `useRestoreCashVoucher`, `useDeleteCashVoucher`, which invalidate `cashKeys.all` and `inventoryKeys.all` (P28).
- `features/debt`: `useOpenItems(params, { enabled })`, with params `{ partnerId, side, direction?, forSourceType?, forSourceId? }` under `[...cashKeys.all, 'debt', 'open-items', params]`.
- `schema.ts` — `cashVoucherSchema`:

  | Field | Rule |
  |---|---|
  | `type`, `voucherAt` | `voucherAt` is a datetime-local string |
  | `fundId`, `reasonId` | required uuid |
  | `partnerId` | optional uuid |
  | `partnerName`, `partnerAddress`, `partnerTaxCode`, `personName`, `description` (max 500), `attachedDocs` (max 255), `note` | optional strings |
  | `version` | optional number, never taken from a later refetch |
  | `lines` | min 1; each `{ id?, description` required max 500, `amount > 0`, `note? }` |
  | `allocations` | each `{ entryId, docCode, amount ≥ 0 }` |
  | `allocationsTouched` | boolean |

- `payload.ts`:
  - `toUpsertPayload(parsed, debtEffect, flags: ConfirmFlags = {})`:
    - ISO `voucherAt` via `fromDateTimeLocalValue`;
    - `allocations` = rows with `amount > 0` when `debtEffect !== 'None'`, else `[]`. It is always an array, so changing to a reason without effect clears the `AtVoucher` rows (Phase 05 rule 4 accepts an empty list);
    - spreads `flags`.
  - `toFormDefaults(voucher?, defaults?)`: edit keeps only `Origin = 'AtVoucher'` allocations; new uses defaults (fund, reason, `voucherAt`, one empty line).
- `allocation.ts`:
  ```ts
  export interface AllocatableItem { entryId: string; docCode: string; docDate: string; dueDate: string | null; open: number }
  /** Earliest (dueDate ?? docDate), then docDate, then docCode; fills until amount is used. Rounded with roundAwayFromZero. */
  export function suggestAllocations(items: AllocatableItem[], amount: number): { entryId: string; docCode: string; amount: number }[];
  export function allocationTotal(rows: { amount: number }[]): number;
  ```

1. **Write the failing tests:**
   - `features/cash-vouchers/allocation.test.ts`:
     - `suggests earliest due first` (due 10-10 before due 10-20, and a null due with doc date 10-15 in between);
     - `stops when the amount is used` (partial last row);
     - `zero amount gives no rows`.
   - `schema.test.ts`: `requires one line with amount > 0`, `fund and reason required`, `valid voucher passes`.
   - `payload.test.ts`:
     - `maps a new receipt with allocations`;
     - `sends empty allocations for reasons without debt effect`;
     - `edit keeps only at-voucher allocations in defaults`;
     - `flags are spread into the payload`;
     - `version comes from form values`.
   - `hooks.test.tsx`: `mutations invalidate cash and inventory roots`.
2. **Run the tests to verify they fail:** `npx vitest run src/features/cash-vouchers`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): cash voucher feature module with allocation suggestion"`

### Task 11.2 — Allocation grid and line grid components

`AllocationGrid({ items, value, total, onChange, onSuggest, readOnly, otherSettled })`:
- `items` are the open items (`OpenItemDto` from `useOpenItems` with `forSource` on edit). Rows show Số CT, Ngày CT, Hạn TT, Còn nợ, Phân bổ (money input).
- Footer: "Tổng phân bổ", "Chưa phân bổ" = `total − otherSettled − Σ value`. Shown in red when negative, together with the message "Tổng phân bổ vượt số tiền phiếu".
- Buttons:
  - "Gợi ý" calls `onSuggest`, which runs `suggestAllocations(items, total − otherSettled)`;
  - "Bỏ phân bổ" clears all rows.
- `otherSettled` is the voucher's own `Manual`/`Offset` settlements on edit, read from `voucher.allocations` with another origin. It shows as "Đã đối trừ ở màn khác: x".
- An amount above `open` shows an inline error.
- Numbers are right-aligned with `tabular-nums`.

`CashLineGrid({ form, readOnly })`: `useFieldArray` over `lines`, with columns #, Diễn giải, Số tiền, Ghi chú, and add/remove row. The money input uses `parseMoneyInput` / `formatMoneyForDisplay`. The footer shows Tổng.

1. **Write the failing tests:**
   - `allocation-grid.test.tsx`:
     - `shows open items and remaining`;
     - `suggest fills earliest due first`;
     - `over-allocation shows error`;
     - `other settled reduces the capacity`;
     - `read-only hides inputs`.
   - `cash-line-grid.test.tsx`: `adds and removes lines and totals amounts`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-vouchers/components`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): allocation grid and cash line grid"`

### Task 11.3 — Cash voucher list page and routes

`CashVoucherListPage({ type })` mirrors `StockVoucherListPage`:
- URL-state filters: q, page, size, from, to, fundId, reasonId, partnerId/partnerName, status, owners.
- Columns: Số phiếu, Ngày, Quỹ, Lý do, Đối tượng, Người nộp/nhận, Diễn giải, Số tiền, Trạng thái, Người lập.
- A managed row shows a "Tự động" pill with a link to its stock voucher (`/stock-out/{id}` or `/stock-in/{id}`).
- The footer shows the total amount.
- The "Thêm" button follows `Can permission="{prefix}.create"`.

Routes in `App.tsx`: `receipts` and `payments` with index/new/:id children, gated like stock vouchers (`receipts.view` / `receipts.create` / `receipts.view`). The form page is added in Task 11.4; until then, `new` and `:id` point to the list page.

1. **Write the failing test** `pages/cash-vouchers/cash-voucher-list-page.test.tsx`:
   - `reads filters from the url and sends them`;
   - `managed rows link to the stock voucher`;
   - `payment labels`;
   - `create button follows permission`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-vouchers/cash-voucher-list-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation:** the page, `labels.ts` (`CASH_VOUCHER_LABELS: Record<CashVoucherType, …>` per the Labels table), the routes.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): receipt and payment list pages"`

### Task 11.4 — Cash voucher form page

`PartnerAutocomplete` gains an optional prop `useSearch?: (keyword: string, reasonId?: string) => { data?: PartnerSearchItem[]; isFetching: boolean }`. The component calls `(useSearch ?? defaultUsePartnerSearch)(…)` exactly once per render, which keeps the hook order stable. Existing callers are unchanged.

`CashVoucherFormPage({ type })` is a loader wrapper like the stock voucher one. The inner `CashVoucherForm`:
- **Sticky action bar:** Lưu và thoát / Lưu tạm / Cập nhật / Hủy phiếu / Khôi phục / Xóa; Ctrl+S; "Số phiếu dự kiến" for a new voucher.
- **Header card "Thông tin chung"** (`cash-voucher-header.tsx`):
  - Số phiếu (read-only), Ngày giờ;
  - Quỹ select: `selectableFunds`, label `Code – Name`, kind badge;
  - Lý do select: `useCashReasons({ type })`, without auto reasons;
  - Đối tượng: `PartnerAutocomplete` with `useSearch = useCashPartnerSearch(type, …)` and `requireReason`, disabled when the reason's `partnerType` is None;
  - Tên / MST / Địa chỉ;
  - Người nộp/nhận, Lý do nộp/chi, Kèm theo, Ghi chú.
- **Lines card:** `CashLineGrid`.
- **Allocation card:** shown when the reason's `debtEffect !== 'None'` and a partner is selected.
  - `side`/`direction` are derived from the effect: the voucher's own direction is ReceivableDecrease → Decrease, …; open items use the opposite direction.
  - `useOpenItems({ partnerId, side, direction: opposite, forSourceType: type, forSourceId: id })`.
  - While `allocationsTouched` is false, the allocations follow `suggestAllocations(items, total − otherSettled)`. Any manual edit sets `allocationsTouched`.
- **Confirmation flow:**
  - `PendingAction` as in stock vouchers, but carrying `flags: ConfirmFlags`.
  - On error, `toConfirmationRequest(err)` → `ConfirmRequiredDialog`. On confirm, call `runAction(action, withConfirmed(flags, request))`.
  - `CONCURRENCY` → refetch, `resetTo(fresh)` and a toast. `VALIDATION` → map `lines[i].*` and `allocations[i].*` onto the fields. Anything else → toast with `formatApiErrorDetails`.
- **Managed voucher** (`isManaged`): the whole form is read-only, with the managed banner (Labels) linking to the stock voucher, and no action buttons except "Đóng".
- **Cancelled:** read-only, with only Khôi phục (`{prefix}.cancel`) and Xóa.
- **Activity history:** the `StockVoucherActivityHistory` component fed by `useCashVoucherActivities`.

Routes: `receipts/new`, `receipts/:id`, `payments/new` and `payments/:id` now point to the form.

1. **Write the failing test** `pages/cash-vouchers/cash-voucher-form-page.test.tsx`. Mock the hooks as in `stock-voucher-form-page.test.tsx`.
   - `new receipt shows expected code and default fund and reason`
   - `create sends mapped payload with suggested allocations` (open items 1,000 due 10-10 and 2,000 due 10-20; line 2,500 → allocations `[1,000, 1,500]`)
   - `changing to a reason without debt effect hides allocations and sends an empty list`
   - `credit/fund warning resends with accumulated flags`:
     - first call → 422 `DEBT_SETTLEMENTS_WILL_BE_REMOVED`; confirm;
     - second → 422 `NEGATIVE_FUND_WARNING`; confirm;
     - third call has both `confirmRemoveSettlements` and `acknowledgeNegativeFund`.
   - `blocked negative fund shows close only and does not resend`
   - `concurrency resets the form`
   - `validation maps allocation errors to the grid`
   - `managed voucher is read-only with a link to the stock voucher`
   - `cancelled voucher offers restore and delete only`
   - `payment labels`
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-vouchers/cash-voucher-form-page.test.tsx src/components/partner-autocomplete`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command, plus `npx vitest run src/pages/stock-vouchers`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): receipt and payment form with allocation and confirmations"`

### Task 11.5 — Fund transfer list and form

- Feature `fund-transfers`: types/api/hooks/keys under `[...cashKeys.all, 'fund-transfers']`.
  - `schema.ts`: `voucherAt`, `fromFundId`, `toFundId` (refine: different), `amount > 0`, `description`, `note`, `version`.
  - `payload.ts`: `toUpsertPayload(parsed, flags)`, `toFormDefaults(transfer?, defaults?)`.
- `FundTransferListPage`:
  - filters from/to, fundId, status, search;
  - columns Số phiếu, Ngày, Quỹ đi, Quỹ đến, Số tiền, Diễn giải, Trạng thái, Người lập;
  - total footer.
- `FundTransferFormPage`:
  - header: Số phiếu, Ngày giờ, Quỹ đi, Quỹ đến (both `selectableFunds`, showing kind), Số tiền, Diễn giải, Ghi chú;
  - the same action bar, confirmation flow (negative fund), concurrency handling and activity history as Task 11.4.
- Routes `fund-transfers` index/new/:id (`fund_transfers.*`).

1. **Write the failing tests:**
   - `features/fund-transfers/payload.test.ts`: `maps payload with flags` and `rejects same funds in schema`.
   - `pages/fund-transfers/fund-transfer-list-page.test.tsx` (smoke): `lists transfers with filters`.
   - `pages/fund-transfers/fund-transfer-form-page.test.tsx`:
     - `creates a transfer`;
     - `negative fund warning resends with acknowledgement`;
     - `cancelled transfer is read-only`.
2. **Run the tests to verify they fail:** `npx vitest run src/features/fund-transfers src/pages/fund-transfers`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): fund transfer list and form"`

### Task 11.6 — Stock voucher form: confirmations, fund, due date, paid amount, automatic voucher link

Changes:
- **Types:** `StockVoucher` gains `dueDate`, `fundId`, `fundCode`, `fundName`, `reasonDebtEffect`, `autoCashVoucherId`, `autoCashVoucherCode`, `autoCashVoucherType`. `UpsertStockVoucherRequest` gains `dueDate`, `fundId`, `confirmRemoveSettlements`, `acknowledgeCreditLimit`, `acknowledgeNegativeFund`. `StockVoucherActionRequest` gains the three flags.
- **Schema:** `dueDate` (optional `yyyy-MM-dd`), `fundId` (optional uuid).
- **`payload.ts`:** `toUpsertPayload(type, parsed, flags: ConfirmFlags = {})` replaces the `ack` boolean. Update the callers and `payload.test.ts`.
- **Paid amount (P9):**
  - while `paidAmountTouched` is false, `paidAmount` follows `total` only when the selected payment method has `fundKind !== 'None'`, and is 0 otherwise;
  - `toUpsertPayload` still omits an untouched `paidAmount` on create (the server applies the same default).
- **Fund select "Quỹ":**
  - shown when `paidAmount > 0` and the method has a fund kind; options are `selectableFunds(funds, method.fundKind, current)`;
  - it defaults to `defaultFund(funds, kind)` when the method changes and the user has not picked a fund;
  - it sends `fundId` only when shown.
- **"Hạn thanh toán" date input:**
  - shown when `reason.debtEffect` is `ReceivableIncrease` or `PayableIncrease`;
  - when the partner is selected and the field is untouched, it fills `voucherDate + partner.creditDays` (P27; `PartnerSearchItem.creditDays`).
- **Confirmations:**
  - `PendingAction` carries `flags`;
  - `handleError` uses `toConfirmationRequest` + `ConfirmRequiredDialog` for all seven codes; `NegativeStockDialog` is no longer used by this page (the opening stock page keeps it);
  - cancel, restore and delete resend with the accumulated flags and the same `version`.
- **Automatic voucher link:** when `autoCashVoucherCode` is set, a line under the totals panel reads "Phiếu thu tự động: PT00001" and links to `/receipts/{id}` (or `/payments/{id}`).
- **Invalidation:** stock voucher mutations also invalidate `cashKeys.all` (P28).

1. **Write the failing tests:**
   - `features/stock-vouchers/payload.test.ts`: `flags are spread into the payload`; `fund and due date are sent`; update the `ack` cases.
   - `features/stock-vouchers/hooks.test.tsx`: `mutations invalidate the cash root too`.
   - `pages/stock-vouchers/stock-voucher-form-page.test.tsx`:
     - `paid amount follows total only for fund-backed methods`;
     - `fund select defaults to the default fund of the method kind`;
     - `due date pre-fills from partner credit days`;
     - `credit limit warning then negative fund warning accumulate flags`;
     - `settlement confirmation on cancel resends with confirmRemoveSettlements`;
     - `shows the automatic voucher link`;
     - update the existing negative-stock cases to the new dialog (`Vẫn tiếp tục`).
2. **Run the tests to verify they fail:** `npx vitest run src/features/stock-vouchers src/pages/stock-vouchers`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher form with fund, due date, generic confirmations and auto voucher link"`

### Task 11.7 — Customer debt panel on the stock-out form

- `features/debt`: `usePartnerDebtSummary(partnerId, { enabled })` under `[...cashKeys.all, 'debt', 'partner-summary', partnerId]`.
- `CustomerDebtPanel({ partnerId })` is a small card under the totals panel:
  - rows: "Dư nợ phải thu", "Hạn mức" ("Không giới hạn" when null), "Còn được nợ" (red when negative), "Nợ quá hạn" (red when > 0), "Số ngày được nợ";
  - it is display only;
  - while loading it shows a skeleton; on error it shows nothing.
- It is shown when:
  - `type === 'Out'`;
  - the reason's `debtEffect === 'ReceivableIncrease'`;
  - a partner is selected;
  - the user has `debt.partner_summary` or `reports.debt` (use the permission helper).

1. **Write the failing tests** `pages/stock-vouchers/components/customer-debt-panel.test.tsx`:
   - `shows balance limit available and overdue`;
   - `unlimited when no limit`.

   Add to `stock-voucher-form-page.test.tsx`:
   - `debt panel only for stock-out sale with permission`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/stock-vouchers`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): customer debt panel on stock-out vouchers"`

## Verification

```bash
cd frontend
npm run typecheck
npm run lint
npx vitest run src/features src/pages/cash-vouchers src/pages/fund-transfers src/pages/stock-vouchers src/components
npm run test    # compare with BASELINE.md
npm run build
```

Manual check (dev stack): smoke-test steps 4, 5, 9 and 10 of `SUMMARY.md`.

## Exit Criteria

- Receipts and payments can be listed, created, edited, cancelled, restored and deleted.
- The allocation grid suggests the earliest-due documents first.
- Managed vouchers are read-only with a link to their stock voucher.
- Fund transfers work end to end.
- The stock voucher form handles the fund, the due date, the paid-amount rule, every confirmation code and the automatic voucher link. Stock-out shows the customer debt panel to permitted users.
- `npm run build` passes; there are no new test failures against `BASELINE.md`.
