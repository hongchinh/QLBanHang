# Phase 02 — Print button and row selection on the cash voucher pages

**Status:** [x] complete
**Complexity:** M

## Objective

- Share the file request and save step between the TT133 exports and the voucher prints (`lib/blob-download.ts`) and add the PDF tab helper (`lib/pdf-tab.ts`).
- Build the voucher print feature (`features/voucher-print`) and `VoucherPrintButton`.
- Put the button on the receipt and payment pages, and a checkbox column, a row print button and a selection bar on their list pages (V13).

The backend routes are those of V5; every test here mocks the API, so this phase can run before or in parallel with Phase 01. In parallel it runs in the frontend worktree `.claude/worktrees/tt133-voucher-print-fe` (V1, Phase 00 Task 0.3).

## Files

Frontend (`frontend/src/`):

- `lib/blob-download.ts`, `lib/blob-download.test.ts` (new)
- `features/tt133-export/api.ts` (modify — use `requestBlob` / `saveBlob`)
- `lib/pdf-tab.ts`, `lib/pdf-tab.test.ts` (new)
- `components/tt133-export/company-profile-hint.tsx`, `company-profile-hint.test.tsx` (new)
- `components/tt133-export/tt133-export-button.tsx` (modify — use `CompanyProfileHint`)
- `features/voucher-print/types.ts`, `types.test.ts`, `api.ts`, `api.test.ts`, `use-voucher-print.ts`, `use-voucher-print.test.tsx` (new)
- `components/voucher-print/voucher-print-button.tsx`, `voucher-print-button.test.tsx` (new)
- `lib/use-row-selection.ts`, `lib/use-row-selection.test.tsx` (new)
- `components/ui/selection-checkbox.tsx`, `selection-checkbox.test.tsx` (new)
- `pages/cash-vouchers/cash-voucher-form-page.tsx`, `cash-voucher-form-page.test.tsx` (modify)
- `pages/cash-vouchers/cash-voucher-list-page.tsx`, `cash-voucher-list-page.test.tsx` (modify)

## Reference files (read-only)

- `features/tt133-export/api.ts`, `api.test.ts` (axios mock, anchor-click spy, blob error case at L68-86), `use-tt133-download.ts`, `use-tt133-download.test.tsx`.
- `components/tt133-export/tt133-export-button.tsx` (menu with `@/components/ui/dropdown-menu`, company hint at L30-33 / L67-79) and its test (`useCompanyProfile` mock, `useAuthStore.setState`, `<Toaster />`).
- `pages/quotations/quotation-form-page.tsx` L231-263 (`openPdfInNewTab`: the tab opened before any await).
- `pages/admin/components/role-matrix-table.tsx` L199-255 (native checkbox and indeterminate through a ref).
- `pages/inventory/stock-card-page.test.tsx` L11-28 (stubbing a print/export button in a page test).
- `lib/blob-error.ts` (`unwrapBlobError`), `lib/api-client.ts` (`formatApiErrorDetails`), `lib/use-toast`.

## Tasks

### Task 2.1 — Shared file request and save

1. **Write the failing tests** in `lib/blob-download.test.ts` (mock `@/lib/api-client` default `get` / `post` exactly as `features/tt133-export/api.test.ts` L4-8; the same `URL.createObjectURL` / anchor `click` spies):
   - `GETs with the params, a blob response and the long timeout`: `get` resolves `{ data: blob }`; `await requestBlob({ method: 'get', url: '/x', params: { format: 'pdf' } })` returns `blob`; `get` was called with `('/x', { params: { format: 'pdf' }, responseType: 'blob', timeout: 120000 })`.
   - `POSTs the body`: `requestBlob({ method: 'post', url: '/x', params: { format: 'xlsx' }, body: { ids: ['a'] } })` → `post('/x', { ids: ['a'] }, { params: { format: 'xlsx' }, responseType: 'blob', timeout: 120000 })`.
   - `turns a JSON error blob back into JSON`: `get` rejects with the `AxiosError` of `api.test.ts` L68-86 (status 503, a JSON blob body) → the rejection's `response.data` equals the parsed body.
   - `saveBlob clicks a link with the file name`: `saveBlob(new Blob(['x']), 'PT00001.xlsx')` → one click with `download === 'PT00001.xlsx'`, `URL.revokeObjectURL('blob:x')` called.
2. **Run the tests to verify they fail:** `npx vitest run src/lib/blob-download.test.ts`. Expected: FAIL — cannot resolve `./blob-download`.
3. **Write the minimal implementation:** `lib/blob-download.ts` with `FILE_REQUEST_TIMEOUT_MS`, `BlobRequest`, `requestBlob` (axios call shape as in the tests; `catch (error) { throw await unwrapBlobError(error); }`) and `saveBlob` (the anchor code moved from `downloadTt133`). Rewrite `downloadTt133` on top of them (POST reports: `params: { format }`, `body: params`; others: `params: { ...params, format }`) and drop its local timeout constant.
4. **Run the tests to verify they pass:** `npx vitest run src/lib/blob-download.test.ts src/features/tt133-export src/components/tt133-export`. Expected: PASS (the existing TT133 tests are unchanged).
5. **Commit:** `git add frontend/src/lib/blob-download.ts frontend/src/lib/blob-download.test.ts frontend/src/features/tt133-export/api.ts && git commit -m "refactor(frontend): shared file request and save"`

### Task 2.2 — PDF tab

1. **Write the failing tests** in `lib/pdf-tab.test.ts`. A fake tab `{ document: { open: vi.fn(), write: vi.fn(), close: vi.fn() }, close: vi.fn(), closed: false }`; `vi.spyOn(window, 'open')`; `URL.createObjectURL = vi.fn(() => 'blob:pdf')`; fake timers for the revoke:
   - `opens a blank tab at once with a loading page`: `openPdfTab('PT00001')` → `window.open` called with `('about:blank', '_blank')`; the written HTML contains `<title>PT00001</title>` and `Đang tải`.
   - `show puts the PDF in the tab`: `tab.show(new Blob(['%PDF']))` → the last written HTML contains `src="blob:pdf"`; after `vi.advanceTimersByTime(60_000)`, `URL.revokeObjectURL('blob:pdf')` was called.
   - `show opens a new window when the tab was blocked`: `window.open` returns `null` for the first call → `show(...)` calls `window.open('blob:pdf', '_blank', 'noopener,noreferrer')`.
   - `close closes the tab`: `close()` → the fake tab's `close` was called; with a blocked tab `close()` does not throw.
   - `escapes the title`: `openPdfTab('<b>')` → the written HTML contains `&lt;b&gt;`.
2. **Run the tests to verify they fail:** `npx vitest run src/lib/pdf-tab.test.ts`. Expected: FAIL — cannot resolve `./pdf-tab`.
3. **Write the minimal implementation:** `openPdfTab(title)` following `openPdfInNewTab` of the quotation page (the same loading page and iframe page), with the title HTML-escaped, returning `{ show, close }`. The quotation page is not changed.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/lib/pdf-tab.ts frontend/src/lib/pdf-tab.test.ts && git commit -m "feat(frontend): PDF tab opened before the request"`

### Task 2.3 — `CompanyProfileHint`

1. **Write the failing tests** in `components/tt133-export/company-profile-hint.test.tsx` (mock `@/features/company-profile/hooks` with a mutable `profile` object as `tt133-export-button.test.tsx` L12-13; `MemoryRouter`; permissions through `useAuthStore.setState`):
   - nothing is rendered while `profile.data` is `undefined` and when `name` is `"Công ty A"`;
   - `name: ''` with `settings.company` → text `"Chưa có thông tin đơn vị"` and a link `"Cài đặt"` to `/settings/company`;
   - `name: '  '` without the permission → the text, no link.
2. **Run the tests to verify they fail:** `npx vitest run src/components/tt133-export/company-profile-hint.test.tsx`. Expected: FAIL — cannot resolve `./company-profile-hint`.
3. **Write the minimal implementation:** move the hint of `Tt133ExportButton` (its L30-33 and L67-79) into `export function CompanyProfileHint()`, and render `<CompanyProfileHint />` in its place in the button.
4. **Run the tests to verify they pass:** `npx vitest run src/components/tt133-export`. Expected: PASS.
5. **Commit:** `git add frontend/src/components/tt133-export/company-profile-hint.tsx frontend/src/components/tt133-export/company-profile-hint.test.tsx frontend/src/components/tt133-export/tt133-export-button.tsx && git commit -m "refactor(frontend): company profile hint component"`

### Task 2.4 — Voucher print feature

1. **Write the failing tests:**
   - `features/voucher-print/types.test.ts` — `voucherPrintFileBase`: one voucher `{ id: 'a', code: 'PT00001' }` → `'PT00001'`; two vouchers with `now = new Date(2026, 9, 14, 16, 5)` → `'PhieuThu_20261014-1605'` (cash `Receipt`), `'PhieuChi_20261014-1605'` (cash `Payment`), `'PhieuNhapKho_20261014-1605'` (stock `In`), `'PhieuXuatKho_20261014-1605'` (stock `Out`).
   - `features/voucher-print/api.test.ts` — mock `@/lib/blob-download` (`requestBlob`):
     - one cash voucher, pdf → `requestBlob({ method: 'get', url: '/cash-vouchers/a/print', params: { format: 'pdf' } })`;
     - one stock voucher, xlsx, `priceMode: 'Cost'` → `{ method: 'get', url: '/stock-vouchers/a/print', params: { format: 'xlsx', priceMode: 'Cost' } }`;
     - two cash vouchers → `{ method: 'post', url: '/cash-vouchers/print', params: { format: 'xlsx' }, body: { ids: ['a', 'b'] } }`;
     - two stock vouchers, `priceMode: 'None'` → `{ method: 'post', url: '/stock-vouchers/print', params: { format: 'pdf' }, body: { ids: ['a', 'b'], priceMode: 'None' } }`.
   - `features/voucher-print/use-voucher-print.test.tsx` — mock `./api` (`fetchVoucherPrint`), `@/lib/pdf-tab` (`openPdfTab` returning `{ show, close }` mocks), `@/lib/blob-download` (`saveBlob`) and `@/lib/use-toast` (`toast`), with the `(...a) => fn(...a)` forwarding of `use-tt133-download.test.tsx`:
     - `pdf: the tab opens before the request ends`: `fetchVoucherPrint` returns a promise resolved by hand; call `print({ kind: 'cash', type: 'Receipt', vouchers: [{ id: 'a', code: 'PT00001' }], format: 'pdf' })` without awaiting → `openPdfTab` was already called with `'PT00001'`; resolve with a blob → `show(blob)` called; `saveBlob` not called.
     - `pdf of several vouchers uses a count as the tab title` → `openPdfTab('In 2 phiếu')`.
     - `xlsx saves the file` → `saveBlob(blob, 'PT00001.xlsx')`; `openPdfTab` not called.
     - `failure closes the tab and shows a toast` → `close()` called; `toast` called with `{ variant: 'destructive', title: 'Không thể in phiếu', description: … }`; `print` resolves (does not throw).
     - `isPending is true while printing`.
2. **Run the tests to verify they fail:** `npx vitest run src/features/voucher-print`. Expected: FAIL — cannot resolve the modules.
3. **Write the minimal implementation:** `types.ts` (types of the shared contract; `ExportFormat` from `@/features/tt133-export/types`, `CashVoucherType` from `@/features/cash-common/types`, `StockDirection` from `@/features/stock-reasons/types`; `voucherPrintFileBase` with the prefixes of V8 and local time `yyyyMMdd-HHmm`), `api.ts` (`fetchVoucherPrint` → `requestBlob` as in the tests; `priceMode` only for stock and only when given), `use-voucher-print.ts` (`useState` pending flag; for pdf call `openPdfTab(title)` before the first `await`; `try { … } catch (error) { tab?.close(); toast({ variant: 'destructive', title: 'Không thể in phiếu', description: formatApiErrorDetails(error) }); } finally { setIsPending(false); }`).
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/features/voucher-print && git commit -m "feat(frontend): voucher print requests"`

### Task 2.5 — `VoucherPrintButton`

1. **Write the failing tests** in `components/voucher-print/voucher-print-button.test.tsx`: mock `@/features/voucher-print/use-voucher-print` (`useVoucherPrint: () => ({ print: printMock, isPending: false })`) and `@/features/company-profile/hooks` (a profile with a name); render inside `MemoryRouter`; `userEvent.setup()`.
   - `prints a PDF`: button `"In"` → menu item `"In (PDF)"` → `printMock` called with `{ kind: 'cash', type: 'Receipt', vouchers: [{ id: 'v1', code: 'PT00001' }], format: 'pdf' }`.
   - `prints Excel`: menu item `"Excel"` → `format: 'xlsx'`.
   - `disabled with a reason`: `disabled` + `disabledReason="Lưu thay đổi trước khi in"` → the button is disabled and has that `title`.
   - `disabled without vouchers`: `vouchers={[]}` → disabled.
   - `shows the company hint`: profile `name: ''` → `"Chưa có thông tin đơn vị"` next to the button; with `variant="icon"` it is not rendered.
   - `icon variant stays inside the row`: rendered inside `<div onClick={parentClick}>` (with the eslint-disable line of the Executor conventions, "Lint in tests"), the icon button has the accessible name `"In PT00001"`; opening the menu and choosing `"Excel"` prints, and `parentClick` is never called.
2. **Run the tests to verify they fail:** `npx vitest run src/components/voucher-print`. Expected: FAIL — cannot resolve the module.
3. **Write the minimal implementation:** `VoucherPrintButton` with the props of the shared contract: a `DropdownMenu` (`@/components/ui/dropdown-menu`) whose trigger is `Button variant="outline" size="sm"` with the `Printer` icon (or `Loader2` while pending), the label and `ChevronDown` (`button`), or `Button variant="ghost" size="icon"` with `aria-label={\`In ${vouchers[0]?.code}\`}` (`icon`); items `"In (PDF)"` and `"Excel"` call `print({ kind, type, vouchers, format })`. Stop click propagation on the trigger and on `DropdownMenuContent` (React events bubble out of the portal to the table row). The `button` variant renders `<CompanyProfileHint />` after the menu.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/components/voucher-print && git commit -m "feat(frontend): voucher print button"`

### Task 2.6 — Print on the receipt and payment page

1. **Write the failing tests** in `pages/cash-vouchers/cash-voucher-form-page.test.tsx`: stub the button (`vi.mock('@/components/voucher-print/voucher-print-button', () => ({ VoucherPrintButton: (props) => { printProps = props; return <button data-testid="voucher-print" disabled={props.disabled} title={props.disabledReason} />; } }))`, reset `printProps` in `beforeEach`).
   - `no print button on a new voucher` (route id `new`).
   - `prints a saved voucher`: an existing receipt → the stub is rendered with `kind: 'cash'`, `type: 'Receipt'`, `vouchers: [{ id, code }]` of the voucher, `disabled: false`.
   - `print waits for unsaved changes`: `fireEvent.change(byId('cash-voucher-note'), …)` (the field the existing tests edit; not the "Lý do" select `#cash-reason`, whose change runs `handleReasonChange` and can clear the partner) → `disabled: true`, `disabledReason: 'Lưu thay đổi trước khi in'`.
   - `a cancelled voucher can be printed`: status `Cancelled` → rendered and enabled.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-vouchers/cash-voucher-form-page.test.tsx`. Expected: FAIL — no `voucher-print` element.
3. **Write the minimal implementation:** in the header button group (`<div className="flex flex-wrap justify-end gap-2">`, before the first button), when `isEdit && voucher`: `<VoucherPrintButton kind="cash" type={type} vouchers={[{ id: voucher.id, code: voucher.code }]} disabled={isDirty || busy} disabledReason={isDirty ? 'Lưu thay đổi trước khi in' : undefined} />`.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx frontend/src/pages/cash-vouchers/cash-voucher-form-page.test.tsx && git commit -m "feat(frontend): print button on receipts and payments"`

### Task 2.7 — Row selection hook and checkbox

1. **Write the failing tests:**
   - `lib/use-row-selection.test.tsx` (`renderHook` with `initialProps: { ids, key }`):
     - `toggle` selects and unselects one id; `selectedIds` keeps page order (`['a', 'b', 'c']`, toggle `c` then `a` → `['a', 'c']`);
     - `toggleAll(true)` selects every page id, `allSelected` true; then `toggle('b', false)` → `allSelected` false, `someSelected` true; `toggleAll(false)` → none;
     - an empty page → `allSelected` false;
     - `clear()` empties the selection;
     - rerender with another `key` → empty; rerender with ids that no longer contain a selected id → `selectedIds` leaves it out.
   - `components/ui/selection-checkbox.test.tsx`: `indeterminate` sets the element's `indeterminate` property; a click calls `onCheckedChange(true)` for an unchecked box and does not reach a parent `onClick` (a `<div onClick>` wrapper with the eslint-disable line of the Executor conventions); the box has its `aria-label`.
2. **Run the tests to verify they fail:** `npx vitest run src/lib/use-row-selection.test.tsx src/components/ui/selection-checkbox.test.tsx`. Expected: FAIL — cannot resolve the modules.
3. **Write the minimal implementation:** `useRowSelection` (a `Set` in `useState`, copy-on-write updates as in `role-create-dialog.tsx`, reset in a `useEffect` on `resetKey` only; `selectedIds`, `allSelected` and `someSelected` are derived during render from `pageIds`, which is a new array on every render of the list page and must never be an effect dependency) and `SelectionCheckbox` (native `<input type="checkbox" className="h-4 w-4 rounded border-input accent-primary">`, `indeterminate` through a ref as in `role-matrix-table.tsx`, `onClick={(e) => e.stopPropagation()}`).
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/lib/use-row-selection.ts frontend/src/lib/use-row-selection.test.tsx frontend/src/components/ui/selection-checkbox.tsx frontend/src/components/ui/selection-checkbox.test.tsx && git commit -m "feat(frontend): list row selection"`

### Task 2.8 — Selection and print on the receipt and payment lists

1. **Write the failing tests** in `pages/cash-vouchers/cash-voucher-list-page.test.tsx` (stub `VoucherPrintButton` and record every call's props; the stub of the `icon` variant also renders `createPortal(<button>portal-{code}</button>, document.body)`, standing in for the menu and dialog portals; the list hook mock returns three receipts `PT00001`, `PT00002`, `PT00003` and, for the next-page case, `hasNextPage: true` — the current fixture's `false` keeps "Sau" disabled):
   - each row has a checkbox `"Chọn PT00001"` … and an icon print stub with `variant: 'icon'`, `vouchers: [{ id, code }]` of the row;
   - ticking `PT00001` and `PT00003` shows `"Đã chọn 2 phiếu"` and a `button` stub with both vouchers in page order;
   - the header checkbox `"Chọn tất cả phiếu trong trang"` selects all three; unticking one leaves it indeterminate;
   - ticking a checkbox does not navigate (the `LocationProbe` path is unchanged);
   - clicking `portal-PT00001` does not navigate (a click that bubbles from a portal is ignored by the row); clicking the row's own cell still navigates;
   - `"Bỏ chọn"` hides the bar;
   - clicking `"Sau"` (next page) clears the selection; typing in the search box clears it too, after the 300 ms debounce (`await waitFor(…)`).
   The existing assertion on the hook argument (`toEqual`, L160-172) stays unchanged.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-vouchers/cash-voucher-list-page.test.tsx`. Expected: FAIL — no checkboxes.
3. **Write the minimal implementation** in `cash-voucher-list-page.tsx`:
   - the page has no `items` / `listParams` variables today: it passes an inline object to `useCashVouchers` (L139-151) and reads rows as `data?.items`. Extract `const listParams = { … }` with exactly the same keys (the test asserts the hook argument with `toEqual`; add no key) and pass it to the hook; add `const items = data?.items ?? []`;
   - `const selection = useRowSelection(items.map((v) => v.id), JSON.stringify(listParams))`;
   - a first column `select` (`SelectionCheckbox` in the header — `checked={selection.allSelected}`, `indeterminate={selection.someSelected}`, `onCheckedChange={selection.toggleAll}`, `aria-label="Chọn tất cả phiếu trong trang"` — and in each cell, `aria-label={\`Chọn ${row.original.code}\`}`) and a last column `actions` with `<VoucherPrintButton variant="icon" kind="cash" type={type} vouchers={[{ id, code }]} />`; both with `size` and `minSize` equal to their default and `enableResizing: false`, as the `actions` column of `quotation-list-page.tsx` L541-545 (the page's `COLUMN_MIN_SIZE` of 60 would override 36 / 48); add `select: 36` and `actions: 48` to `COLUMN_SIZING_DEFAULTS`;
   - the columns are built in a `useMemo` (L165-231) whose deps hold only labels: add `selection` and `type` to its deps (or build the two new columns outside the memo and concatenate), or the checkboxes keep the state of the first render;
   - the row's `onClick` (L409-413) navigates only when `e.currentTarget.contains(e.target as Node)`: React click events bubble out of portals (the print menu, the price-mode dialog and its overlay in Phase 04), and the row must ignore them;
   - above the table, when `selection.selectedIds.length > 0`: `<div role="region" aria-label="Phiếu đã chọn" className="flex flex-wrap items-center gap-3 rounded-md border bg-muted/40 px-3 py-2 text-sm">` with `Đã chọn {n} phiếu`, `<VoucherPrintButton kind="cash" type={type} vouchers={selected} />` (the selected rows' `{ id, code }` in page order) and `<Button variant="ghost" size="sm" onClick={selection.clear}>Bỏ chọn</Button>`.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git add frontend/src/pages/cash-vouchers/cash-voucher-list-page.tsx frontend/src/pages/cash-vouchers/cash-voucher-list-page.test.tsx && git commit -m "feat(frontend): select and print receipts and payments from the list"`

### Task 2.9 — Phase checkpoint

1. From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Compare the failures and the lint counts with `BASELINE.md`; a new failure or lint error is fixed (failing test first) before the phase ends.
3. Mark Phase 02 `[x]` in this file and in `SUMMARY.md`; commit `git add docs/plans/261014-1632-tt133-voucher-print-forms && git commit -m "chore(plan): phase 02 complete"`.

## Verification

```bash
cd frontend
npx vitest run src/lib src/features/voucher-print src/components/voucher-print src/components/tt133-export src/pages/cash-vouchers
npm run typecheck
npm run lint
npm run test -- --testTimeout=30000
npm run build
git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
```

## Exit Criteria

- The TT133 export button works as before on top of `requestBlob` / `saveBlob` / `CompanyProfileHint`.
- A saved receipt or payment page shows "In" (PDF in a new tab, Excel download), disabled while the form has unsaved changes.
- The receipt and payment lists have row checkboxes, a row print button and the "Đã chọn N phiếu" bar; the selection is cleared on page or filter change.
- No test, typecheck, lint or build result beyond `BASELINE.md`.
