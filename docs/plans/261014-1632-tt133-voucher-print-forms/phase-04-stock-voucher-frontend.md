# Phase 04 — Price mode dialog and the stock voucher pages

**Status:** [x] complete
**Complexity:** M

## Objective

- Ask for the price mode before printing a stock voucher (`StockPriceModeDialog`, V13), remembering the last choice per user.
- Put the print button on the stock-in and stock-out pages, and the checkbox column, row print button and selection bar on their list pages.

Needs Phase 02 (`VoucherPrintButton`, `useVoucherPrint`, `useRowSelection`, `SelectionCheckbox`). The API is mocked, so Phase 03 need not be finished. Runs in the same worktree as Phase 02 (V1).

## Files

Frontend (`frontend/src/`):

- `features/voucher-print/price-mode-storage.ts`, `price-mode-storage.test.ts` (new)
- `components/voucher-print/stock-price-mode-dialog.tsx`, `stock-price-mode-dialog.test.tsx` (new)
- `components/voucher-print/voucher-print-button.tsx`, `voucher-print-button.test.tsx` (modify)
- `pages/stock-vouchers/stock-voucher-form-page.tsx`, `stock-voucher-form-page.test.tsx` (modify)
- `pages/stock-vouchers/stock-voucher-list-page.tsx`, `stock-voucher-list-page.test.tsx` (modify)

## Reference files (read-only)

- `pages/stock-vouchers/utils/use-stock-grid-prefs.ts` L35-73 (a per-user `localStorage` key with try/catch).
- `components/tt133-export/export-targets-dialog.tsx` (dialog with radio options built on `@/components/ui/dialog`).
- `pages/stock-vouchers/stock-voucher-form-page.test.tsx` L71-80 (`useAuthStore` mock with `permissionState.granted`), `stock-voucher-list-page.test.tsx` (list hook mock, `LocationProbe`).
- Phase 02 Tasks 2.6 and 2.8 (the same changes on the cash pages).

## Tasks

### Task 4.1 — Price mode storage and dialog

1. **Write the failing tests:**
   - `features/voucher-print/price-mode-storage.test.ts` (`localStorage.clear()` in `beforeEach`):
     - `readPriceMode('u1', true)` with nothing stored → `'Voucher'`;
     - `writePriceMode('u1', 'None')` then `readPriceMode('u1', true)` → `'None'`; the key is `qldh:stock-print-price-mode:u1`;
     - stored `'Cost'`: `readPriceMode('u1', true)` → `'Cost'`, `readPriceMode('u1', false)` → `'Voucher'`;
     - a stored value that is not a mode (`'Foo'`) → `'Voucher'`;
     - `getItem` / `setItem` throwing (`vi.spyOn(Storage.prototype, …)`) → `readPriceMode` returns `'Voucher'`, `writePriceMode` does not throw.
   - `components/voucher-print/stock-price-mode-dialog.test.tsx` (real `useAuthStore.setState` with `user.id = 'u1'` and the permissions of the case; `onConfirm`, `onOpenChange` mocks):
     - with `inventory.view_cost`: title `"Chọn giá in"`; options `"Giá trên phiếu"` (checked), `"Giá vốn"`, `"Không in tiền"`; choose `"Giá vốn"`, click `"In"` → `onConfirm('Cost')`, `onOpenChange(false)`, the key holds `'Cost'`; reopening shows `"Giá vốn"` checked;
     - without the permission: no `"Giá vốn"` option; with `'Cost'` stored, `"Giá trên phiếu"` is checked;
     - `"Hủy"` → `onOpenChange(false)`, `onConfirm` not called.
2. **Run the tests to verify they fail:** `npx vitest run src/features/voucher-print/price-mode-storage.test.ts src/components/voucher-print/stock-price-mode-dialog.test.tsx`. Expected: FAIL — cannot resolve the modules.
3. **Write the minimal implementation:**
   - `price-mode-storage.ts`: `readPriceMode(userId: string | undefined, canViewCost: boolean): StockPrintPriceMode` and `writePriceMode(userId: string | undefined, mode: StockPrintPriceMode): void`, every storage access in try/catch (no user id → default / no write).
   - `StockPriceModeDialog` (contract in `SUMMARY.md`): `Dialog` from `@/components/ui/dialog`, three native radio inputs (`name="stock-print-price-mode"`, labels as above, the `Cost` one only with `inventory.view_cost`), the selection initialised from `readPriceMode` whenever the dialog opens, buttons `"Hủy"` and `"In"`; `"In"` writes the mode, calls `onConfirm(mode)` and closes. Stop click propagation on the dialog content (it can sit inside a clickable table row). The overlay is rendered next to the content (`dialog.tsx` L48-50), not inside it, so a click on the overlay still bubbles to the row; the list pages ignore it with the row guard of Task 2.8 / Task 4.4.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/features/voucher-print/price-mode-storage.ts frontend/src/features/voucher-print/price-mode-storage.test.ts frontend/src/components/voucher-print/stock-price-mode-dialog.tsx frontend/src/components/voucher-print/stock-price-mode-dialog.test.tsx && git commit -m "feat(frontend): stock voucher price mode dialog"`

### Task 4.2 — Stock vouchers ask for the price mode

1. **Write the failing tests** in `voucher-print-button.test.tsx` (the dialog is real; permissions through `useAuthStore.setState`):
   - `stock: the format opens the price mode dialog`: `kind="stock" type="In"` → `"Excel"` → the dialog `"Chọn giá in"` opens and `printMock` is not called yet; choose `"Không in tiền"`, `"In"` → `printMock` with `{ kind: 'stock', type: 'In', vouchers, format: 'xlsx', priceMode: 'None' }`.
   - `stock: cancelling the dialog prints nothing`.
   - `cash prints without the dialog` (the Phase 02 tests still pass unchanged).
   - `icon variant: the dialog stays inside the row`: inside `<div onClick={parentClick}>` (with the eslint-disable line of the Executor conventions, "Lint in tests"), `"In (PDF)"` → dialog → `"In"` → printed, `parentClick` never called.
2. **Run the tests to verify they fail:** `npx vitest run src/components/voucher-print/voucher-print-button.test.tsx`. Expected: FAIL — `printMock` is called at once without `priceMode`.
3. **Write the minimal implementation:** for `kind === 'stock'`, a menu item stores the chosen format and opens `StockPriceModeDialog`; its `onConfirm(mode)` calls `print({ kind, type, vouchers, format, priceMode: mode })`.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/components/voucher-print/voucher-print-button.tsx frontend/src/components/voucher-print/voucher-print-button.test.tsx && git commit -m "feat(frontend): price mode before printing a stock voucher"`

### Task 4.3 — Print on the stock-in and stock-out page

1. **Write the failing tests** in `pages/stock-vouchers/stock-voucher-form-page.test.tsx`. Stub the button (the page test renders without a `QueryClientProvider`): `vi.mock('@/components/voucher-print/voucher-print-button', () => ({ VoucherPrintButton: (props) => { printProps = props; return <button data-testid="voucher-print" disabled={props.disabled} title={props.disabledReason} />; } }))`, with `printProps` reset in `beforeEach`.
   - no print button on a new voucher (route id `new`);
   - an existing stock-in → the stub with `kind: 'stock'`, `type: 'In'`, `vouchers: [{ id, code }]`, `disabled: false`;
   - after `fireEvent.change(byId('stock-voucher-note'), …)` (the "Ghi chú" field, always mounted, as the existing tests do; the stock form has no "Diễn giải") → `disabled: true`, `disabledReason: 'Lưu thay đổi trước khi in'`;
   - a cancelled voucher (read-only) → enabled.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/stock-vouchers/stock-voucher-form-page.test.tsx`. Expected: FAIL — no `voucher-print` element.
3. **Write the minimal implementation:** in the header button group (`<div className="flex flex-wrap justify-end gap-2">`, before the first button), when `isEdit && voucher`: `<VoucherPrintButton kind="stock" type={type} vouchers={[{ id: voucher.id, code: voucher.code }]} disabled={isDirty || busy} disabledReason={isDirty ? 'Lưu thay đổi trước khi in' : undefined} />`.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx frontend/src/pages/stock-vouchers/stock-voucher-form-page.test.tsx && git commit -m "feat(frontend): print button on stock vouchers"`

### Task 4.4 — Selection and print on the stock voucher lists

1. **Write the failing tests** in `pages/stock-vouchers/stock-voucher-list-page.test.tsx` (stub `VoucherPrintButton` as in Task 2.8, the `icon` stub with its `createPortal` button; the list hook mock today returns one row with `hasNextPage: false`: make it three stock-ins `PN00001`…`PN00003`, with `hasNextPage: true` for the next-page case):
   - each row has a checkbox `"Chọn PN00001"` … and an icon print stub (`kind: 'stock'`, `type: 'In'`, the row's voucher);
   - ticking two rows shows `"Đã chọn 2 phiếu"` and a `button` stub with both vouchers in page order;
   - the header checkbox `"Chọn tất cả phiếu trong trang"` selects all; unticking one leaves it indeterminate;
   - ticking does not navigate; clicking `portal-PN00001` does not navigate; clicking the row's own cell still navigates;
   - `"Bỏ chọn"` hides the bar;
   - moving to the next page (the `StockListFooter` next button) or changing the status filter clears the selection.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/stock-vouchers/stock-voucher-list-page.test.tsx`. Expected: FAIL — no checkboxes.
3. **Write the minimal implementation** in `stock-voucher-list-page.tsx`:
   - as on the cash list (Task 2.8): extract `const listParams = { … }` from the inline object passed to `useStockVouchers` (L126-138; it already holds the resolved status), same keys, and add `const items = data?.items ?? []`;
   - `const selection = useRowSelection(items.map((v) => v.id), JSON.stringify(listParams))`;
   - a first column `select`: `SelectionCheckbox` in the header (`checked={selection.allSelected}`, `indeterminate={selection.someSelected}`, `onCheckedChange={selection.toggleAll}`, `aria-label="Chọn tất cả phiếu trong trang"`) and in each cell (`aria-label={\`Chọn ${row.original.code}\`}`);
   - a last column `actions` with `<VoucherPrintButton variant="icon" kind="stock" type={type} vouchers={[{ id: row.original.id, code: row.original.code }]} />`;
   - both columns with `size` and `minSize` equal to their default and `enableResizing: false`; `select: 36` and `actions: 48` in `COLUMN_SIZING_DEFAULTS` (L44-59, the object given to `useColumnSizingPersist`);
   - the columns' `useMemo` (L154-203) has only `[labels.basePath]` as deps: add `selection` and `type` (or build the two new columns outside it);
   - the row's `onClick` (L385-389) navigates only when `e.currentTarget.contains(e.target as Node)` (clicks from the menu, the price-mode dialog and its overlay bubble through portals);
   - above the table, when `selection.selectedIds.length > 0`: `<div role="region" aria-label="Phiếu đã chọn" className="flex flex-wrap items-center gap-3 rounded-md border bg-muted/40 px-3 py-2 text-sm">` with `Đã chọn {n} phiếu`, `<VoucherPrintButton kind="stock" type={type} vouchers={selected} />` (the selected rows' `{ id, code }` in page order) and `<Button variant="ghost" size="sm" onClick={selection.clear}>Bỏ chọn</Button>`.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/pages/stock-vouchers/stock-voucher-list-page.tsx frontend/src/pages/stock-vouchers/stock-voucher-list-page.test.tsx && git commit -m "feat(frontend): select and print stock vouchers from the list"`

### Task 4.5 — Phase checkpoint

1. From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Compare with `BASELINE.md`; fix a new failure or lint error (failing test first).
3. Mark Phase 04 `[x]` in this file and in `SUMMARY.md`; commit `git add docs/plans/261014-1632-tt133-voucher-print-forms && git commit -m "chore(plan): phase 04 complete"`.

## Verification

```bash
cd frontend
npx vitest run src/features/voucher-print src/components/voucher-print src/pages/stock-vouchers
npm run typecheck
npm run lint
npm run test -- --testTimeout=30000
npm run build
git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
```

## Exit Criteria

- Printing a stock voucher always asks for the price mode first; Giá vốn is offered only with `inventory.view_cost`; the last choice is remembered per user.
- A saved stock-in or stock-out page shows "In", disabled while the form has unsaved changes.
- The stock voucher lists have row checkboxes, a row print button and the "Đã chọn N phiếu" bar; the selection is cleared on page or filter change.
- No test, typecheck, lint or build result beyond `BASELINE.md`.
