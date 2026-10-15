# Phase 04 — Wire screens, help and docs

**Status:** [x] done (2026-10-15)
**Complexity:** S

## Objective

Turn quick add on for the stock-in/out line grid, the quotation line grid and the opening stock page. The stock card and cost recalculation pages stay off. Then update the help files and docs, and check everything in a real browser.

## Files

- `frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx` (line ~565)
- `frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx`
- `frontend/src/pages/quotations/components/line-items-grid.tsx` (line ~290)
- `frontend/src/pages/quotations/components/line-items-grid.test.tsx`
- `frontend/src/pages/inventory/opening-stock-page.tsx` (line ~248)
- `frontend/src/pages/inventory/opening-stock-page.test.tsx`
- `frontend/src/features/help/content/stock-in.form.md`, `stock-out.form.md`, `quotations.form.md`, `inventory-opening-stock.md`
- `docs/architecture/system-architecture.md`, `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`, `docs/SUMMARY.md`
- `docs/plans/261015-2038-product-quick-add/GO-LIVE-CHECKLIST.md` (new)

Commands run from `frontend/` unless stated.

## Tasks

### Task 4.1 — Stock line grid

1. **Write the failing test** in `stock-line-grid.test.tsx`.
   - The file already mocks `@/features/products/hooks` at line 20; make its `useProductSearch` return `settledQuery` equal to the typed text.
   - Mock the quick add dialog with the same fake as in Phase 03 Task 3.4 (async factory importing `@/components/ui/dialog`, and a "fake lưu" button that calls `onCreated(created)` then `onOpenChange(false)`).
   - The file has **no** auth mock today. The grid reads `useAuthStore((s) => s.user?.id ?? '')` (`stock-line-grid.tsx:352`) to key its Enter preferences, and the tests store those preferences under user `''` (`stockGridPrefsKey('', type)`). So add a module mock in the style of `partner-code-name-field.test.tsx:13-17`: the selector state has `hasPermission: (p) => permissions.includes(p)` and **no** `user`, so the preferences key stays `''`. `permissions` is a mutable array, `['products.create']` by default.

   Cases:
   - Type `TL-50` in row 1's product code cell (search settled on `[]`) and press Enter. The dialog opens. Click "fake lưu" with `created = { …sheet, id: 'new-id', code: 'TL-50', pricingMode: 'PerUnit' }`. Row 1 shows `created.name`, and `stock-line-quantity-0` has the focus (the default `PerUnit` stops are `['quantity','unit-price']`, `line-navigation.ts:21`).
   - With `permissions = []`, there is no add row.
2. **Run it and check it fails:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx`. Expected: FAIL with no option named `Thêm mới hàng hóa "TL-50"`.
3. **Write the minimal implementation:** add `allowQuickAdd` to the `<ProductTypeaheadCell>` in `stock-line-grid.tsx`. `onSelect` stays as it is.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers`. Expected: PASS, and `stock-voucher-form-page.test.tsx` still passes.
5. **Commit:** `git add src/pages/stock-vouchers && git commit -m "feat(stock-vouchers): product quick add in the line grid"`

### Task 4.2 — Quotation line grid and opening stock

1. **Write the failing tests:**
   - **Quotation.** `line-items-grid.test.tsx:12-20` replaces the cell with a plain `<input aria-label="product-code">`, so no add row can be rendered there. Change that mock to also record the props it receives (`lastCellProps = props`). Add a case asserting `lastCellProps.allowQuickAdd === true` and `lastCellProps.nextFocusId === getLineCellId('name', 0)`. The commit and focus behaviour itself is covered by cell tests 3.4(f) and (f2).
   - **Opening stock.** In `opening-stock-page.test.tsx`, which does not mock the cell and has no auth mock, grant `products.create` with `useAuthStore.setState` (the way `line-items-grid.test.tsx:46-58` sets the store), and use the fake dialog from Task 4.1. After typing an unknown code, pressing Enter and clicking "fake lưu", the row shows `created.name` (via `selectProduct`) and the cell input has the focus.
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx src/pages/inventory/opening-stock-page.test.tsx`. Expected: FAIL. In quotations, `allowQuickAdd` is undefined. In opening stock, the add row is missing.
3. **Write the minimal implementation:** add `allowQuickAdd` to the cell in `line-items-grid.tsx` and in `opening-stock-page.tsx`. Do **not** add it in `stock-card-page.tsx` or `recalc-cost-page.tsx`.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations src/pages/inventory src/pages/settings`. Expected: PASS.
5. **Commit:** `git add src/pages/quotations src/pages/inventory && git commit -m "feat(products): product quick add on quotations and opening stock"`

### Task 4.3 — Help files and docs

1. Invoke the `help-content` skill for each of `stock-in.form.md`, `stock-out.form.md`, `quotations.form.md` and `inventory-opening-stock.md`. In each file:
   - Add these three paths to `sources`, so later changes flag the file as stale and the skill's `git grep` label check finds the new labels: `frontend/src/pages/quotations/components/product-typeahead-cell.tsx`, `frontend/src/features/products/components/product-quick-add-dialog.tsx` and `frontend/src/features/products/components/product-form-fields.tsx`. `quotations.form.md` already lists the cell.
   - Add this bullet. In the three form files it goes next to the partner quick add bullet. In `inventory-opening-stock.md`, which has no such bullet, it goes under "Lưu ý".
     > `F4` trong ô **Mã hàng** (cũng là dòng **Thêm mới hàng hóa** trong danh sách khi gõ mà không thấy) mở **Thêm nhanh hàng hóa**: chữ đã gõ điền sẵn vào Mã (nếu không có dấu cách, không dấu) hoặc Tên, Enter đi qua từng ô, `Ctrl Enter` lưu và điền hàng vào dòng (không lưu phiếu), `Esc` hủy và về ô mã. Cần quyền tạo hàng hóa.

     In the opening stock file, drop "(không lưu phiếu)".
   - In the "Phím tắt" section (stock-in ~line 37, stock-out ~38, quotations ~34; add one to opening stock if it has the section), add: `` `F4` (ở ô **Mã hàng**) — Thêm nhanh hàng hóa ``.

   Then run `npm run help:stale`. Expected: clean. None of these keys has a video in `videos.json`, so there are no narration hashes to update.
2. Update the docs, one sentence or one table cell each:
   - `docs/architecture/system-architecture.md`: in the RBAC / quick add grants paragraph, the Warehouse, Accountant and Sales seed roles also get `products.create` (brainstorm 261015-2033). This also opens `/products/new` and product group creation.
   - `docs/code-standard/conventions.md`, in the frontend section:
     - `ProductFormFields` is the single product form;
     - `useSaveProduct` owns the unit get-or-create step;
     - `ProductTypeaheadCell` has an `allowQuickAdd` opt-in that requires `products.create`;
     - a product created in the dialog is committed in `onCloseAutoFocus`;
     - `useDialogOpening` lives in `src/lib`.
   - `docs/codebase/directory-structure.md`: list the new files under `features/products`.
   - `docs/SUMMARY.md`: add a row for this plan, and update the descriptions of the three doc files above to mention product quick add.
3. Write `GO-LIVE-CHECKLIST.md`:
   - On existing databases, an admin ticks `products.create` for Kho / giao hàng, Kế toán and Nhân viên kinh doanh under Phân quyền.
   - Users are told that F4 in the Mã hàng cell adds a product.
4. **Commit:** `git add src/features/help ../docs && git commit -m "docs: product quick add help, docs and go-live checklist"`

### Task 4.4 — Real-browser check

Use Playwright with service workers blocked (`serviceWorkers: 'block'`), a scratch database (never `qldonhang_test`) and a running backend, as in `docs/plans/archived/261015-0926-cash-voucher-form-redesign/BROWSER-CHECK.md`.

Check these as an Admin user, and again as a Warehouse user whose role has `products.create` ticked:

- Stock out: type `TL-50` in Mã hàng and press Enter. The dialog opens with Mã `TL-50` and the focus in Tên. Fill Tên, Nhóm and ĐVT (a new unit typed by hand), then press Ctrl+Enter. The line is filled and the focus is on the next stop. The voucher still shows "Nháp chưa lưu" and has not been saved. Type `TL-50` in row 2: it is now found.
- Stock in: F4 with an empty cell opens the dialog with the focus in Mã. Esc returns to the cell.
- Quotation: type `tôn mới` and choose the add row with the arrow keys. Tên is prefilled. After saving, the focus goes to the name cell. Also check the free-text line: type an unknown code and press Enter, which now opens the dialog. Esc, then Enter again, should still reach Tên so the line can be typed without a catalog product (`productId` is optional).
- Opening stock: the add row works, and the row gets the product.
- As a Warehouse user without the tick, there is no add row and F4 does nothing.
- The stock card page shows no add row.

Write the findings to `docs/plans/261015-2038-product-quick-add/BROWSER-CHECK.md`, with screenshots under `browser-check/`. Commit them.

## Verification

- `npm run typecheck && npm run lint && npm run test -- --testTimeout=30000 --maxWorkers=3`. Only the 3 known baseline failures are allowed.
- `npm run help:stale` is clean.
- `BROWSER-CHECK.md` records every check above as passed.

## Exit Criteria

- Quick add works on stock in, stock out, quotation and opening stock. It is absent on the stock card and on cost recalculation.
- The help files, docs and go-live checklist are updated and committed.
