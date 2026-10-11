# Phase 12 — Frontend matching, offset, opening debts & reports

**Status:** [x] complete
**Complexity:** L

## Objective

Finish the 2A screens:

- the manual matching screen (Đối trừ chứng từ), with an oldest-first suggestion and unmatching;
- the debt offset list and form (Bù trừ công nợ);
- the opening debt screen (Nợ đầu kỳ);
- the three reports: Số dư công nợ (rows drill into the ledger), Sổ chi tiết công nợ and Sổ quỹ.

## Files

- `frontend/src/features/debt/{api,hooks,types,keys,matching}.ts` (+ `matching.test.ts`) (modify/new)
- `frontend/src/features/debt-offsets/{api,hooks,keys,types,payload}.ts` (+ `payload.test.ts`) (new)
- `frontend/src/features/opening-debts/{api,hooks,types,schema}.ts` (new)
- `frontend/src/features/cash-reports/{api,hooks,keys,types}.ts` (new)
- `frontend/src/pages/debt/{debt-matching-page,debt-offset-list-page,debt-offset-form-page,opening-debt-page,opening-debt-form-dialog}.tsx` (+ tests) (new)
- `frontend/src/pages/debt/components/{open-item-grid,settlement-list}.tsx` (+ `open-item-grid.test.tsx`) (new)
- `frontend/src/pages/cash-reports/{debt-balance-page,debt-ledger-page,cash-book-page,document-link}.tsx|ts` (+ tests) (new)
- `frontend/src/App.tsx` (modify — routes)

## Reference files (read-only)

- `frontend/src/pages/inventory/{stock-card-page,stock-on-hand-page}.tsx` and tests — report page pattern (filters in a Card, plain `<table>`, `voucherLink`)
- `frontend/src/pages/cash-vouchers/components/allocation-grid.tsx` (Phase 11) — money inputs, totals footer
- `frontend/src/lib/{vn-datetime,round}.ts`

## Tasks

### Task 12.1 — Matching helpers and debt API hooks

```ts
// features/debt/matching.ts
export interface MatchRow { entryId: string; amount: number }
/** Pairs decrease rows with increase rows in the given order (greedy), splitting amounts; Σ must be equal. */
export function pairAllocations(decreases: MatchRow[], increases: MatchRow[]): { increaseEntryId: string; decreaseEntryId: string; amount: number }[];
/** Oldest first on both sides: (dueDate ?? docDate), docDate, docCode; fills both sides up to min(Σ open left, Σ open right). */
export function suggestMatching(decreases: AllocatableItem[], increases: AllocatableItem[]): { decreases: MatchRow[]; increases: MatchRow[] };
```

Hooks in `features/debt/hooks.ts`:
- `useDebtSettlements({ partnerId, side }, { enabled })`;
- `useCreateSettlementBatch`, `useDeleteSettlement`, `useDeleteSettlementBatch`, which invalidate `cashKeys.all`;
- `useDebtPartnerSearch(side?, keyword, { bothRoles? })` (created in Phase 10; extend it with `bothRoles`).

1. **Write the failing tests** `features/debt/matching.test.ts`:
   - `pairs one decrease across two increases` (D 3,000 vs I1 1,000 + I2 2,000 → pairs `[I1←D 1,000]`, `[I2←D 2,000]`)
   - `pairs two decreases into one increase`
   - `throws when sums differ`
   - `suggestion fills the smaller side fully, oldest first`
2. **Run the tests to verify they fail:** `npx vitest run src/features/debt`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): debt matching helpers and settlement hooks"`

### Task 12.2 — Matching screen

`DebtMatchingPage` (route `debt/matching`, `debt.matching`):

- **Filters:** "Phía" toggle (Phải thu / Phải trả) and a partner picker (`useDebtPartnerSearch(side)`). The URL keeps `side` and `partnerId`.
- **Two `OpenItemGrid`s side by side** (stacked on mobile), from `useOpenItems({ partnerId, side, direction })`:
  - left "Chứng từ giảm nợ chưa đối trừ" (Decrease);
  - right "Chứng từ ghi nợ còn nợ" (Increase);
  - columns Số CT, Ngày, Hạn TT, Còn lại, Đối trừ (money input), with a link on Số CT (`documentLink`, Task 12.5).
- **Footer:**
  - Σ left and Σ right;
  - "Gợi ý" runs `suggestMatching`, "Xóa" clears;
  - "Đối trừ" is enabled only when both sums are equal and > 0. It sends `pairAllocations(...)` as one batch.
  - On 400, the `items[i].*` errors show in a toast with `formatApiErrorDetails`.
- **Tab "Đã đối trừ":** `SettlementList` lists `useDebtSettlements`, grouped by `batchId`, with origin labels (Lúc lập phiếu / Thủ công / Bù trừ / Tự động).
  - `Manual` and `AtVoucher` rows have "Bỏ" (one settlement) and "Bỏ cả lô" (batch), each behind `ConfirmDialog`;
  - `Auto` and `Offset` rows show "Do chứng từ quản lý" and no button.

1. **Write the failing tests:**
   - `pages/debt/components/open-item-grid.test.tsx`: `enters amounts and caps at open` (an amount above open shows an error).
   - `pages/debt/debt-matching-page.test.tsx`:
     - `loads open items for the selected partner and side`;
     - `suggest then save sends paired items`;
     - `save disabled when sums differ`;
     - `unmatch single and batch`;
     - `managed settlements have no unmatch button`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/debt/components src/pages/debt/debt-matching-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation**, including the route.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): document matching screen with suggestion and unmatching"`

### Task 12.3 — Debt offset list and form

- Feature `debt-offsets`: types per Phase 08 DTOs; keys under `[...cashKeys.all, 'debt-offsets']`; hooks list/get/activities/defaults/create/cancel/delete.
- `payload.ts`: `toCreatePayload({ voucherAt, partnerId, note, receivables, payables })` (only rows with amount > 0).
- `DebtOffsetListPage`: filters from/to, partner, status; columns Số phiếu, Ngày, Đối tượng, Số tiền, Ghi chú, Trạng thái, Người lập; total footer.
- `DebtOffsetFormPage` (`new` and `:id`):
  - new:
    - Ngày giờ and a partner picker (`bothRoles: true`);
    - two `OpenItemGrid`s: receivable Increase items (`side Receivable, direction Increase`) and payable Increase items;
    - "Gợi ý" fills both sides up to `min(Σ open)` oldest first (reuse `suggestMatching`);
    - "Lưu" is enabled when the sums are equal and > 0;
  - view (`:id`): read-only header, items table, activity history; "Hủy phiếu" (`debt_offset.cancel`) and "Xóa" (`debt_offset.delete`) with version; there is no edit.
- Routes `debt/offsets` index/new/:id.

1. **Write the failing tests:**
   - `features/debt-offsets/payload.test.ts`: `maps rows with amounts`.
   - `pages/debt/debt-offset-form-page.test.tsx`:
     - `requires equal sides`;
     - `creates an offset`;
     - `view mode cancels with version`.
   - `pages/debt/debt-offset-list-page.test.tsx` (smoke): `lists offsets`.
2. **Run the tests to verify they fail:** `npx vitest run src/features/debt-offsets src/pages/debt/debt-offset`. Expected: FAIL.
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): debt offset list and form"`

### Task 12.4 — Opening debt screen

- Feature `opening-debts`: hooks list/create/update/delete under `[...cashKeys.all, 'opening-debts']`.
- `schema.ts`: `partnerId`, `side`, `kind`, `docNo` (1..50), `docDate`, `dueDate` (optional; must be empty when kind = Prepayment), `amount > 0`, `note`.
- `OpeningDebtPage` (route `debt/opening`, `debt.opening_balance`):
  - a header line "Ngày đầu kỳ: dd/MM/yyyy" from `OpeningDebtListResult.openingDate`; when it is null, a warning with a link to Settings → Chi nhánh, and "Thêm" disabled;
  - filters partner (any role), side and search;
  - table columns Đối tượng, Phía, Loại, Số CT, Ngày CT, Hạn TT, Số tiền, Đã đối trừ, Ghi chú;
  - create/edit in `OpeningDebtFormDialog`: the partner picker follows the chosen side; "Hạn TT" is hidden for Trả trước;
  - delete with `ConfirmDialog`;
  - edit and delete handle `DEBT_SETTLEMENTS_WILL_BE_REMOVED` through `ConfirmRequiredDialog` and resend with `confirmRemoveSettlements`.
- The owner field is **not** shown in 2A. The backend accepts it; 2B decides on the employee field (P2).

1. **Write the failing test** `pages/debt/opening-debt-page.test.tsx`:
   - `lists opening debts`;
   - `creates a receivable debt with due date`;
   - `prepayment hides due date`;
   - `shrinking a settled row asks to remove settlements`;
   - `no opening date disables adding`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/debt/opening-debt-page.test.tsx`. Expected: FAIL.
3. **Write the minimal implementation**, including the route.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): opening debts screen"`

### Task 12.5 — Reports: debt balance, debt ledger, cash book

- Feature `cash-reports`: types per Phase 09 DTOs; keys `[...cashKeys.all, 'reports', name, params]`; hooks `useDebtBalance(params, { enabled })`, `useDebtLedger(params, { enabled })`, `useCashBook(params, { enabled })`.
- `document-link.ts`: `documentLink(sourceType, sourceId)` maps:
  - StockIn → `/stock-in/{id}`, StockOut → `/stock-out/{id}`;
  - Receipt → `/receipts/{id}`, Payment → `/payments/{id}`;
  - Offset → `/debt/offsets/{id}`, Opening → `/debt/opening`;
  - TransferIn/TransferOut (cash book) → `/fund-transfers/{id}`.
- `DebtBalancePage` (`reports/debt-balance`, `reports.debt`):
  - filters Phía, Từ ngày, Đến ngày (defaults `firstDayOfMonthYmd()` / `todayYmd()`), search; synced to search params;
  - columns Mã, Tên, Đầu kỳ Nợ, Đầu kỳ Có, Phát sinh tăng, Phát sinh giảm, Cuối kỳ Nợ, Cuối kỳ Có, with a totals row;
  - clicking a row navigates to `/reports/debt-ledger?side=&partnerId=&from=&to=`.
- `DebtLedgerPage` (`reports/debt-ledger`, `reports.debt`):
  - filters Phía, Đối tượng (`useDebtPartnerSearch(side)`), Từ–Đến, read from search params;
  - an opening line, then rows Ngày, Số CT (link), Loại CT, Diễn giải, Tăng, Giảm, Số dư; then the totals and the closing line (Nợ/Có per P24).
- `CashBookPage` (`reports/cash-book`, `reports.cash`):
  - filters Loại quỹ (Tất cả / Tiền mặt / Ngân hàng), Quỹ (`useFunds({ kind })` + "Tất cả"), Từ–Đến;
  - one table section per group: a fund heading (code, name, bank/account), "Tồn đầu kỳ", then rows Ngày, Số phiếu thu, Số phiếu chi, Đối tượng/Người nộp-nhận, Diễn giải, Thu, Chi, Tồn; then the total and "Tồn cuối kỳ";
  - transfer rows show the transfer code in the Thu or Chi column, according to direction;
  - when there are 2+ groups, a grand total block with a note "Không tính chuyển quỹ nội bộ giữa các quỹ đang xem".
- All numeric columns are right-aligned with `tabular-nums` and formatted with `formatCurrencyVnd`.

1. **Write the failing tests:**
   - `pages/cash-reports/debt-balance-page.test.tsx`:
     - `renders rows and totals with debit/credit columns`;
     - `row click opens the ledger with params`.
   - `pages/cash-reports/debt-ledger-page.test.tsx`:
     - `reads params from the url and renders running balance`;
     - `document codes link to their screens`.
   - `pages/cash-reports/cash-book-page.test.tsx`:
     - `renders one group per fund with opening and closing`;
     - `grand total shown for several funds`;
     - `fund options follow the kind filter`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-reports`. Expected: FAIL.
3. **Write the minimal implementation**, including the routes.
4. **Run tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): debt balance, debt ledger and cash book reports"`

## Verification

```bash
cd frontend
npm run typecheck
npm run lint
npx vitest run src/features/debt src/features/debt-offsets src/pages/debt src/pages/cash-reports
npm run test    # compare with BASELINE.md
npm run build
```

Manual check (dev stack): smoke-test steps 3, 6, 7, 9, 11 and 12 of `SUMMARY.md`.

## Exit Criteria

- Matching, unmatching, debt offset and opening debt screens work against the API, with confirmations.
- The three reports render the backend data, the balance report drills into the ledger, and document codes link to their screens.
- Every sidebar item of "Tiền & Công nợ" resolves to a working route.
- `npm run build` passes; there are no new test failures against `BASELINE.md`.
