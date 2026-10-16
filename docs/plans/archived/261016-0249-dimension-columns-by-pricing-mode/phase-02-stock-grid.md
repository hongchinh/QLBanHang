# Phase 02 — Stock grid and stock form page

**Status:** [x] done (2026-10-16)
**Complexity:** L

## Objective

Make the stock-in and stock-out grid follow the rule of the brainstorm (SUMMARY → Rule, Behaviour, Stock grid changes):

- **Số tấm**, **Dài**, **Rộng**, **Dày** show only as the highest pricing mode of the lines needs them, plus any column that has a dimension error on a line;
- a pick (typeahead, full catalog, quick add) lands on the first Enter stop of the picked mode, **Số tấm** by default, even when that column was hidden a moment before;
- a cell that its line's mode does not use shows "—", disabled, with the stored value kept in the form;
- an unknown mode counts as `PerUnit` for the Enter path too;
- a re-pick clears the line's dimension errors;
- the focused cell stays in view when the dimension columns change;
- columns that appear get the flash class `col-appeared`;
- **Cột & phím Enter** gets one note line;
- on the form page, a server error on a dimension that the line does not use sends the caret to that line's **Mã hàng**.

Frontend only. No backend, API, payload or database change. No help file changes here: Phase 04 writes them last.

**Depends on Phase 01 only.** This phase uses exactly these Phase 01 exports:

- `frontend/src/lib/dimension-columns.ts`: `type DimensionField`, `isDimensionField`, `normalizePricingMode`, `usesDimension`, `shownDimensions`, and `PRICING_MODES` (already imported by the two stock files, see below);
- `frontend/src/lib/use-appeared-keys.ts`: `useAppearedKeys(keys: readonly string[], durationMs = 1200): ReadonlySet<string>`;
- `isFieldDisabled` in `pages/stock-vouchers/utils/line-navigation.ts` already normalizes a missing or unknown mode to `PerUnit`;
- the `col-appeared` rules in `pages/quotations/components/line-items-grid.css` (the grid already imports that file at `stock-line-grid.tsx:41`).

**Before Task 2.1, check the Phase 01 state** from `$WT/frontend`:

```bash
WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
cd "$WT/frontend" && ls src/lib/dimension-columns.ts src/lib/use-appeared-keys.ts
cd "$WT/frontend" && grep -rn "PRICING_MODES" src/pages/stock-vouchers
```

The grep must show `PRICING_MODES` imported from `@/lib/dimension-columns` in `utils/use-stock-grid-prefs.ts` and `components/stock-grid-settings-popover.tsx`, and no definition in `utils/line-navigation.ts`. Phase 01 moved it without a re-export, so it had to change both importers in the same commit. If either file still imports it from `./line-navigation` or `@/pages/stock-vouchers/utils/line-navigation`, Phase 01 is not finished: stop and report.

Phase 01 does not edit `stock-line-grid.tsx`, `stock-voucher-form-page.tsx` or the stock test files, so the line numbers below (taken at `e61e4f1b`) still hold for them at the start of this phase; later tasks shift them, so each edit is anchored on its before snippet. `use-stock-grid-prefs.ts` and `stock-grid-settings-popover.tsx` changed only in their import lines. In `use-stock-grid-prefs.ts` the count is unchanged (`PRICING_MODES,` left the import block, one import line came in), so its `e61e4f1b` numbers hold. In `stock-grid-settings-popover.tsx` the import at :5 became two lines, so every later line moves down one: its number is one more than the `e61e4f1b` number cited here (the **Hiện cột** section is :37-48 after Phase 01, :36-47 before).

## Files

- `frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx` (modify)
- `frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx` (modify: harness, new tests, four updated expectations)
- `frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts` (modify: `dimensionErrorFields`, the `OPTIONAL_COLUMNS` comment)
- `frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.test.ts` (modify)
- `frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx` (modify: note line)
- `frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.test.tsx` (modify)
- `frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx` (modify: `focusIdsOf`, `focusFirstError`)
- `frontend/src/pages/stock-vouchers/stock-voucher-form-page.test.tsx` (modify: five tests)
- Unchanged, must keep passing: `utils/line-navigation.test.ts`, `utils/server-field-errors.test.ts`, every other file under `src/pages/stock-vouchers`.

Commands run from `$WT/frontend` (Phase 00). Shell variables and the cwd do not survive between Bash calls: begin every Bash call with `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns`, and chain each `cd` to its command with `&&` (`cd "$WT/frontend" && npx vitest run …`). The commit lines below are one chained line each, starting `cd "$WT" &&`, so their paths start with `frontend/`. The source files use CRLF line ends: edit them with the Edit tool and do not convert them. Add a second `-m` with the `Co-Authored-By` trailer from the session's attribution instructions to every commit line below, as Phase 00 does. Under load, add `--testTimeout=30000 --maxWorkers=3` to the vitest commands.

**Test harness rules for this phase** (SUMMARY → Testing):

- vitest runs with `css: false`, so tests assert the `col-appeared` class only. Colours and reduced motion are checked in the real browser (Phase 04).
- Do not switch on fake timers in the grid tests: RTL's `waitFor` does not advance them, and the pick and focus assertions rely on `waitFor`. A test that must see the flash class go uses `waitFor(…, { timeout: 2000 })`.
- `src/test/setup.ts:27-28` stubs `Element.prototype.scrollIntoView`, so `vi.spyOn(Element.prototype, 'scrollIntoView')` works. Restore it in a `finally`, as the `select` spies of this file do.
- The quick add dialog mock (`stock-line-grid.test.tsx:30-50`) gets a settable product in Task 2.1, reset in `beforeEach`. Only the new m² quick add test sets another product.

## Tasks

**Before Task 2.1:** set `**Status:** [-] in progress` in this file (in the worktree copy, `$WT/docs/plans/261016-0249-dimension-columns-by-pricing-mode/`) and commit it (SUMMARY → Execution notes):

`WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-02-stock-grid.md && git commit -m "docs(plan): phase 02 in progress"`

Then record the `<phase base>` of the Verification: `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git rev-parse HEAD`, taken before the first commit of Task 2.1.

### Task 2.1 — Settable quick add product in the grid test harness

The mocked `ProductQuickAddDialog` (`stock-line-grid.test.tsx:30-50`) always hands back the module-level `const quickAdded` (`:87-93`, `PerUnit`), and the existing quick add test (`:1332-1342`) relies on that. Task 2.3 needs an m² product from the same mock.

1–2. **No failing test.** This changes only the test harness, not production code, so no test can fail first. The proof is that the whole file, including the quick add tests at `:1332-1350`, stays green.
3. **Change the harness** in `stock-line-grid.test.tsx`:
   - In the mock factory (`:40`), before:
     ```ts
                   onCreated(quickAdded);
     ```
     after:
     ```ts
                   onCreated(quickAddResult);
     ```
   - After `const quickAdded` (`:87-93`), before:
     ```ts
       pricingMode: 'PerUnit',
     };

     const listItem: ProductListItem = {
     ```
     after:
     ```ts
       pricingMode: 'PerUnit',
     };
     // What the mocked quick add dialog hands back; reset to quickAdded before each test.
     let quickAddResult: ProductSuggestion = quickAdded;

     const listItem: ProductListItem = {
     ```
   - In `beforeEach` (`:216-229`), before:
     ```ts
         localStorage.clear();
         permissions = ['products.create'];
         useProductSearchMock.mockReset();
     ```
     after:
     ```ts
         localStorage.clear();
         permissions = ['products.create'];
         quickAddResult = quickAdded;
         useProductSearchMock.mockReset();
     ```
   The factory reads `quickAddResult` only when the fake save button is clicked, after the module has run, so the hoisted `vi.mock` can use the `let`.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx`. Expected: PASS, the same count as before the change.
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx && git commit -m "test(stock-vouchers): let the quick add mock hand back a settable product"`

### Task 2.2 — `dimensionErrorFields`

A pure function next to `forcedColumns` (`use-stock-grid-prefs.ts:119-138`). It lists the dimension columns with an error on any line. Error keys are `sheetCount`, `length`, `width`, `thickness` (`server-field-errors.ts:13-26`). Like `forcedColumns`, it ignores a non-array `lines` error (the array-level error).

1. **Write the failing test.** In `use-stock-grid-prefs.test.ts`, add `dimensionErrorFields` to the import (`:3-9`):
   ```ts
   import {
     DEFAULT_STOCK_GRID_PREFS,
     dimensionErrorFields,
     forcedColumns,
     readStockGridPrefs,
     stockGridPrefsKey,
     useStockGridPrefs,
   } from './use-stock-grid-prefs';
   ```
   Append after the `forcedColumns` describe (ends at `:129`):
   ```ts
   describe('dimensionErrorFields', () => {
     it('lists the dimension columns with an error on any line', () => {
       const errors = [
         undefined,
         { width: { message: 'a' }, note: { message: 'b' } },
         { sheetCount: { message: 'c' }, quantity: { message: 'd' } },
       ];
       expect([...dimensionErrorFields(errors)].sort()).toEqual(['sheet-count', 'width']);
       expect([...dimensionErrorFields([{ length: { message: 'e' }, thickness: { message: 'f' } }])].sort()).toEqual([
         'length',
         'thickness',
       ]);
       expect([...dimensionErrorFields(undefined)]).toEqual([]);
     });

     it('ignores an array-level error, which react-hook-form gives as an object, not an array', () => {
       expect([...dimensionErrorFields({ root: { message: 'x' } })]).toEqual([]);
       expect([...dimensionErrorFields({ message: 'x', type: 'too_small' })]).toEqual([]);
     });
   });
   ```
2. **Run it and check it fails:** `npx vitest run src/pages/stock-vouchers/utils/use-stock-grid-prefs.test.ts`. Expected: both new tests FAIL with `TypeError: dimensionErrorFields is not a function or its return value is not iterable`. The module has no such export yet, so the import is `undefined`. The 10 existing tests pass.
3. **Write the minimal implementation** in `use-stock-grid-prefs.ts`:
   - Add `type DimensionField` to the `@/lib/dimension-columns` import that Phase 01 added:
     ```ts
     import { PRICING_MODES, type DimensionField } from '@/lib/dimension-columns';
     ```
   - The comment on `OPTIONAL_COLUMNS` (`:12`) says the other columns are always shown, which stops being true (SUMMARY → Stock grid changes). The four dimension keys stay out of `OPTIONAL_COLUMNS`. Before:
     ```ts
     // Columns the user may hide (brainstorm §2); the rest are always shown.
     ```
     after:
     ```ts
     // Columns the user may hide (brainstorm §2). Số tấm/Dài/Rộng/Dày follow the pricing-mode rule (`shownDimensions`);
     // the rest are always shown.
     ```
   - Share the loop over the line errors. Before (`:119`):
     ```ts
     // Hidden columns that must still show: Kho when a line leaves the header warehouse, and any column with an error.
     export function forcedColumns(
     ```
     after:
     ```ts
     // The line form fields that have an error on any line. `form.formState.errors.lines` is an array of per-line errors,
     // but an object when the array itself is rejected (for example no line), so it is checked here instead of trusting
     // the type.
     function lineErrorKeys(lineErrors: unknown): string[] {
       if (!Array.isArray(lineErrors)) return [];
       return (lineErrors as (Record<string, unknown> | undefined)[]).flatMap((lineError) => Object.keys(lineError ?? {}));
     }

     // Hidden columns that must still show: Kho when a line leaves the header warehouse, and any column with an error.
     export function forcedColumns(
     ```
   - Before (`:128-138`):
     ```ts
       if (lines.some((l) => !!l.warehouseId && l.warehouseId !== headerWarehouseId)) forced.add('warehouse');
       if (Array.isArray(lineErrors)) {
         for (const lineError of lineErrors as (Record<string, unknown> | undefined)[]) {
           for (const key of Object.keys(lineError ?? {})) {
             const column = ERROR_COLUMNS[key];
             if (column) forced.add(column);
           }
         }
       }
       return forced;
     }
     ```
     after:
     ```ts
       if (lines.some((l) => !!l.warehouseId && l.warehouseId !== headerWarehouseId)) forced.add('warehouse');
       for (const key of lineErrorKeys(lineErrors)) {
         const column = ERROR_COLUMNS[key];
         if (column) forced.add(column);
       }
       return forced;
     }

     // Line form fields of the four dimension columns.
     const DIMENSION_ERROR_FIELDS: Partial<Record<string, DimensionField>> = {
       sheetCount: 'sheet-count',
       length: 'length',
       width: 'width',
       thickness: 'thickness',
     };

     // Dimension columns with an error on any line. The grid shows them even where the highest pricing mode would hide
     // them: the server can reject a dimension that the line's mode on screen does not use.
     export function dimensionErrorFields(lineErrors: unknown): Set<DimensionField> {
       const fields = new Set<DimensionField>();
       for (const key of lineErrorKeys(lineErrors)) {
         const field = DIMENSION_ERROR_FIELDS[key];
         if (field) fields.add(field);
       }
       return fields;
     }
     ```
     The parameter keeps the type of `forcedColumns`' `lineErrors` (`unknown`). Shorten the comment on the `lineErrors` parameter of `forcedColumns` (`:123-124`), which `lineErrorKeys` now explains. Before:
     ```ts
       // `form.formState.errors.lines`: an array of per-line errors, but an object when the array itself is
       // rejected (for example no line), so it is checked here instead of trusting the type.
       lineErrors: unknown,
     ```
     after:
     ```ts
       // `form.formState.errors.lines`; `lineErrorKeys` handles the array-level error.
       lineErrors: unknown,
     ```
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers/utils`. Expected: PASS, including the two `forcedColumns` tests (`:113-129`), `line-navigation.test.ts` and `server-field-errors.test.ts`. No test can see the `OPTIONAL_COLUMNS` comment: check it in `git diff`.
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.test.ts && git commit -m "feat(stock-vouchers): dimensionErrorFields lists the dimension columns with a line error"`

### Task 2.3 — Dimension columns follow the rule, and a pick lands on its first stop

One task, as section 01 → Stock pick-path tests requires. Once the columns hide, the existing pick test (`stock-line-grid.test.tsx:616-628`) and the catalog and quick add paths land on **Đơn giá** unless `onSelect` builds its Enter path from the picked mode: the shown set is computed once per render, so `isVisible` inside `onSelect` belongs to the render before the pick, where the picked mode's columns are hidden. A prototype confirmed it: with the hiding alone, all three pick tests fail because the caret lands on `stock-line-unit-price-0` (the typeahead and catalog tests print `expected 'stock-line-unit-price-0' to be 'stock-line-sheet-count-0'`, the quick add test a `toHaveFocus()` failure). The same task wires the forced error columns, so that `stock-voucher-form-page.test.tsx:859-869` (width error on a `PerUnit` line) never breaks.

1. **Write the failing tests** in `stock-line-grid.test.tsx`:
   - (a) **Helpers.** Change the products type import (`:14`) to:
     ```ts
     import type { PricingMode, ProductListItem, ProductSuggestion } from '@/features/products/types';
     ```
     After `function line(…)` (`:114-125`), before `function zeroComputed(`, add:
     ```ts
     // A m³ line and a m² line with every dimension their mode uses.
     const m3Line = (overrides: Partial<StockLineFormValues> = {}) =>
       line({ pricingMode: 'PerCubicMeter', sheetCount: 2, length: 3000, width: 100, thickness: 50, ...overrides });
     const m2Line = (overrides: Partial<StockLineFormValues> = {}) =>
       line({ pricingMode: 'PerSquareMeter', sheetCount: 1, length: 2000, width: 1200, ...overrides });
     ```
     After `function headers()` (`:181-183`), add:
     ```ts
     const DIMENSION_HEADERS = ['Số tấm', 'Dài', 'Rộng', 'Dày'];
     // The dimension columns the grid shows, in column order.
     function dimensionHeaders() {
       return headers().filter((h) => DIMENSION_HEADERS.includes(h ?? ''));
     }
     ```
   - (b) **Rule and typing.** After the test `dimension inputs follow the pricing mode` (`:262-280`), add:
     ```ts
       // The highest pricing mode of the lines decides; a missing or unknown mode counts as PerUnit.
       it.each<[string, unknown[], string[]]>([
         ['unit lines', ['PerUnit', 'PerUnit'], []],
         ['a md line', ['PerUnit', 'PerLinearMeter'], ['Số tấm', 'Dài']],
         ['a md and a m² line', ['PerLinearMeter', 'PerSquareMeter'], ['Số tấm', 'Dài', 'Rộng']],
         ['a m³ line', ['PerUnit', 'PerCubicMeter'], ['Số tấm', 'Dài', 'Rộng', 'Dày']],
         ['a line without a mode', [undefined], []],
         ['a line with an unknown mode', ['Legacy'], []],
       ])('dimension columns with %s', (_case, modes, expected) => {
         renderGrid({ lines: modes.map((mode) => line({ pricingMode: mode as PricingMode })) });
         expect(dimensionHeaders()).toEqual(expected);
       });

       // A line's mode changes only on a pick: a code typed with the list open changes no column.
       it.each<[string, StockLineFormValues, string[]]>([
         ['a blank line', createEmptyStockLine(0, WH1), []],
         ['a m³ line', m3Line(), ['Số tấm', 'Dài', 'Rộng', 'Dày']],
       ])('typing a code over %s without a pick changes no column', async (_case, start, expected) => {
         renderGrid({ lines: [start] });
         fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'TON' } });
         await screen.findByRole('listbox'); // the m² product is offered, not picked
         expect(dimensionHeaders()).toEqual(expected);
       });
     ```
   - (c) **Catalog path** (`:319-338`). Before:
     ```ts
       it('selecting through the catalog dialog applies the same autofill', async () => {
         const { values } = renderGrid({ type: 'In' });
         fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'GO' } });
     ```
     after:
     ```ts
       it('selecting through the catalog dialog applies the same autofill', async () => {
         const { values } = renderGrid({ type: 'In' });
         expect(dimensionHeaders()).toEqual([]);
         fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'GO' } });
     ```
     and at its end, before:
     ```ts
             length: 3000,
             width: 100,
             thickness: 50,
           }),
         );
       });
     ```
     after:
     ```ts
             length: 3000,
             width: 100,
             thickness: 50,
           }),
         );
         // The m³ product's first Enter stop, although its column was hidden before the pick.
         await waitFor(() => expect(document.activeElement?.id).toBe('stock-line-sheet-count-0'));
         expect(dimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng', 'Dày']);
       });
     ```
     The catalog dialog gives the focus back to its opener only when the focus is lost (`components/ui/dialog.tsx:50-62`), and the grid's 0 ms `focusCell` timer runs first (section 01, case 3).
   - (d) **The four tests whose expected values change** (unit lines now show 4 fewer columns, 286 px narrower):
     - `:413-421`, before:
       ```ts
           expect(headers()).toEqual([
             'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'SL tồn',
             'Đơn giá', 'Số tiền', '%CK', '%VAT', 'Số còn lại', '',
           ]);
       ```
       after:
       ```ts
           expect(headers()).toEqual([
             'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'SL', 'SL tồn', 'Đơn giá', 'Số tiền', '%CK', '%VAT', 'Số còn lại', '',
           ]);
       ```
     - `:423-430`, before:
       ```ts
           expect(headers()).toEqual([
             'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Kho', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'SL tồn',
             'Đơn giá', 'Số tiền', '%CK', 'Tiền CK', '%VAT', 'Tiền VAT', 'Ghi chú', 'Số còn lại', '',
           ]);
       ```
       after:
       ```ts
           expect(headers()).toEqual([
             'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Kho', 'SL', 'SL tồn',
             'Đơn giá', 'Số tiền', '%CK', 'Tiền CK', '%VAT', 'Tiền VAT', 'Ghi chú', 'Số còn lại', '',
           ]);
       ```
     - `:442-446`, before:
       ```ts
         it('a read-only grid has no delete column (15 headers by default)', () => {
           renderGrid({ lines: [line()], readOnly: true });
           expect(headers()).toHaveLength(15);
       ```
       after:
       ```ts
         it('a read-only grid has no delete column (11 headers by default for unit lines)', () => {
           renderGrid({ lines: [line()], readOnly: true });
           expect(headers()).toHaveLength(11);
       ```
     - `:582` (in `:567-583`), before:
       ```ts
           expect(screen.getByRole('table')).toHaveStyle({ width: '1428px', minWidth: '1428px' });
       ```
       after:
       ```ts
           expect(screen.getByRole('table')).toHaveStyle({ width: '1142px', minWidth: '1142px' });
       ```
       From `COLUMNS` (`stock-line-grid.tsx:66-87`) without the 4 default-hidden columns: 42 + 120 + 220 + 56 + 96 + 96 + 110 + 120 + 60 + 60 + 120 + 42 = 1142, which is 1428 − (70 + 76 + 76 + 64). The sticky offsets in the same test (0, 42, 162 px left; 42 px right) do not change.
   - (e) **Forced error column.** After the test `a line error on a hidden column shows it` (`:448-453`), add:
     ```ts
       // The server checks a line against the catalog's current mode, so it can reject a dimension the screen's mode does
       // not use: the column comes in so the message shows under the cell.
       it('a dimension error shows its column on a unit-only voucher', async () => {
         const { form } = renderGrid({ lines: [line()] });
         expect(dimensionHeaders()).toEqual([]);
         act(() => form().setError('lines.0.width', { type: 'server', message: 'Chiều rộng phải lớn hơn 0' }));
         await waitFor(() => expect(dimensionHeaders()).toEqual(['Rộng']));
         const width = byId('stock-line-width-0');
         expect(width).toBeDisabled();
         expect(width).toHaveAttribute('aria-invalid', 'true');
         expect(width).toHaveAccessibleDescription('Chiều rộng phải lớn hơn 0');
       });
     ```
   - (f) **Typeahead path** (`:616-628`). Before:
     ```ts
         renderGrid({ lines: [createEmptyStockLine(0, WH1)] });
         await pickFromTypeahead('TON01');
         await waitFor(() => expect(focused()).toBe('stock-line-sheet-count-0'));
         enter('stock-line-sheet-count-0');
     ```
     after:
     ```ts
         renderGrid({ lines: [createEmptyStockLine(0, WH1)] });
         expect(dimensionHeaders()).toEqual([]);
         await pickFromTypeahead('TON01');
         await waitFor(() => expect(focused()).toBe('stock-line-sheet-count-0'));
         expect(dimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
         enter('stock-line-sheet-count-0');
     ```
   - (g) **Row delete.** After the test `the delete button keeps focus in the grid, on the Mã of the line that takes its place` (`:1184-1189`), add:
     ```ts
       // The m² line does not use Dày, so the focus goes to its Mã hàng before the row goes; then Dày hides.
       it('Ctrl+Delete from Dày of the only m³ line focuses Mã hàng of the m² line and hides Dày', async () => {
         const { values } = renderGrid({ lines: [m3Line({ productCode: 'A' }), m2Line({ _uiKey: 'b', productCode: 'B' })] });
         const survivorCode = byId('stock-line-product-code-1');
         act(() => byId('stock-line-thickness-0').focus());

         fireEvent.keyDown(byId('stock-line-thickness-0'), { key: 'Delete', ctrlKey: true });

         await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['B']));
         expect(document.activeElement).toBe(survivorCode);
         expect(focused()).toBe('stock-line-product-code-0');
         expect(dimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
       });
     ```
   - (h) **Quick add path.** In `describe('product quick add')`, after the test `adds a product from the code cell and lands on the quantity cell like a pick` (`:1332-1342`), add:
     ```ts
         it('a quick-added m² product shows Số tấm, Dài and Rộng and lands on Số tấm', async () => {
           quickAddResult = { ...quickAdded, pricingMode: 'PerSquareMeter' };
           const input = typeNewCode();
           expect(dimensionHeaders()).toEqual([]);
           await screen.findByRole('option', { name: /Thêm mới hàng hóa "TL-50"/ });

           fireEvent.keyDown(input, { key: 'Enter' });
           await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' });
           fireEvent.click(screen.getByRole('button', { name: 'fake lưu' }));

           await waitFor(() => expect(byId('stock-line-sheet-count-0')).toHaveFocus());
           expect(dimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
         });
     ```
2. **Run them and check they fail:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx`. Expected: exactly 15 failures, all because the four columns are always shown today:
   - `dimension columns with unit lines`, `… a line without a mode`, `… a line with an unknown mode`: `expected [ 'Số tấm', 'Dài', 'Rộng', 'Dày' ] to deeply equal []`; `… a md line`: `… to deeply equal [ 'Số tấm', 'Dài' ]`; `… a md and a m² line`: `… to deeply equal [ 'Số tấm', 'Dài', 'Rộng' ]`. `dimension columns with a m³ line` passes (the nesting guard).
   - `typing a code over a blank line …`: `expected [ 'Số tấm', 'Dài', 'Rộng', 'Dày' ] to deeply equal []`. `typing a code over a m³ line …` passes (guard).
   - The catalog test, the forced error test, the typeahead pick test and the quick add m² test fail at their first `expect(dimensionHeaders()).toEqual([])`, before any pick or error.
   - The Ctrl+Delete test: the focus assertions pass (the stock grid already moves the focus before the remove), and `expected [ 'Số tấm', 'Dài', 'Rộng', 'Dày' ] to deeply equal [ 'Số tấm', 'Dài', 'Rộng' ]`.
   - `hides Kho, …`: `expected [ 'STT', 'Mã hàng', 'Tên hàng', …(13) ] to deeply equal [ 'STT', 'Mã hàng', 'Tên hàng', …(9) ]`; `stored prefs show every column`: `expected [ 'STT', 'Mã hàng', 'Tên hàng', …(17) ] to deeply equal [ 'STT', 'Mã hàng', 'Tên hàng', …(13) ]`; `a read-only grid has no delete column …`: `expected [ … ] to have a length of 11 but got 15`; `pins #, Mã and Tên …`: `toHaveStyle` expected `width: 1142px`, received `1428px`.
3. **Write the minimal implementation** in `stock-line-grid.tsx`:
   - Imports. After `import { cn } from '@/lib/utils';` (`:17`) add:
     ```ts
     import { isDimensionField, shownDimensions, usesDimension } from '@/lib/dimension-columns';
     ```
     In the `use-stock-grid-prefs` import (`:35-40`), before:
     ```ts
     import {
       forcedColumns,
       OPTIONAL_COLUMNS,
     ```
     after:
     ```ts
     import {
       dimensionErrorFields,
       forcedColumns,
       OPTIONAL_COLUMNS,
     ```
   - The shown set (`:355`). Before:
     ```ts
       const forced = forcedColumns(rows, headerWarehouseId, lineErrors);
     ```
     after:
     ```ts
       const forced = forcedColumns(rows, headerWarehouseId, lineErrors);
       // Số tấm, Dài, Rộng, Dày follow the highest pricing mode of the lines, plus any with an error on a line. Computed once
       // per render, so the onSelect of a pick sees the columns of the render before the pick.
       const shownDims = shownDimensions(rows, dimensionErrorFields(lineErrors));
     ```
   - `isShown` (`:360-361`). Before:
     ```ts
       const isShown = (key: ColumnKey) =>
         !isOptional(key) || !prefs.hiddenColumns.includes(key) || forced.has(key) || key === focusedColumn;
     ```
     after:
     ```ts
       const isShown = (key: ColumnKey) =>
         isDimensionField(key)
           ? shownDims.includes(key)
           : !isOptional(key) || !prefs.hiddenColumns.includes(key) || forced.has(key) || key === focusedColumn;
     ```
     `columns`, `minWidth`, the sticky offsets, `enterPath` (through `isVisible`), `verticalTarget` and `removeLine` follow unchanged. Nesting (md ⊂ m² ⊂ m³) keeps every cell a row's own mode uses on screen, so Enter, the arrows and the survivor focus never target a missing cell.
   - `onSelect` (`:573-578`). Before:
     ```tsx
                     onSelect={(s) => {
                       applyProduct(idx, s);
                       const next = nextStop(enterPath(s.pricingMode, prefs.enterStops[s.pricingMode], isVisible), 'product-code');
                       if (next) focusCell(next, idx, true);
                       else setTimeout(() => goNextRow(idx), 0);
                     }}
     ```
     after:
     ```tsx
                     onSelect={(s) => {
                       applyProduct(idx, s);
                       // isVisible is the render before the pick: the picked mode's dimension columns may not be shown yet.
                       // They are once the pick renders, before the focus timer below runs.
                       const visibleAfterPick = (f: LineFocusField) =>
                         (isDimensionField(f) && usesDimension(s.pricingMode, f)) || isVisible(f);
                       const next = nextStop(enterPath(s.pricingMode, prefs.enterStops[s.pricingMode], visibleAfterPick), 'product-code');
                       if (next) focusCell(next, idx, true);
                       else setTimeout(() => goNextRow(idx), 0);
                     }}
     ```
     The focus keeps `focusCell(next, idx, true)`: the 0 ms timer runs after the render that shows the column. `commit()` in `product-typeahead-cell.tsx:172-183` is the only caller of `onSelect`, so this covers the list, Enter, the waiting Enter, the catalog and quick add. No pending-focus mechanism.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers`. Expected: PASS, including:
   - every test of `stock-line-grid.test.tsx`, among them the keyboard suites `type=number cells` (`:891-1094`) and `a key typed at once after a line is deleted` (`:1208-1266`), and the Ctrl+Delete and delete button tests (`:1104-1189`);
   - `stock-voucher-form-page.test.tsx`, in particular `validation details map lines[0].width onto the grid cell` (`:859-869`, now passing through the forced column) and `a server error on SL of a line priced %s …` (`:2566-2592`);
   - `utils/line-navigation.test.ts`, `utils/use-stock-grid-prefs.test.ts`, `components/stock-grid-settings-popover.test.tsx`.
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx && git commit -m "feat(stock-vouchers): dimension columns follow the highest pricing mode"`

### Task 2.4 — `pathOf` counts an unknown mode as `PerUnit`

`pathOf` (`stock-line-grid.tsx:440-443`) indexes `prefs.enterStops[mode]` with `?? 'PerUnit'`, which only covers a missing mode. `prefs.enterStops` holds only the four modes (`use-stock-grid-prefs.ts:58`), so an unknown string makes `enterPath` throw at `stops.includes` (`line-navigation.ts:55` at `e61e4f1b`). `removeLine` (`:368`) and ↑/↓ (`:484`) need no change: they go through `isFieldDisabled`, which Phase 01 made normalize.

1. **Write the failing test.** In `stock-line-grid.test.tsx`, after the test `Enter on a product code without a product does nothing` (`:651-658`), add:
   ```ts
     // An old draft can hold a mode the grid does not know: it counts as PerUnit, also for the Enter stops.
     it('Enter from Mã hàng of a line with an unknown pricing mode moves to SL', () => {
       renderGrid({ lines: [line({ pricingMode: 'Legacy' as PricingMode, quantity: 2 })] });
       act(() => byId('stock-line-product-code-0').focus());
       enter('stock-line-product-code-0');
       expect(focused()).toBe('stock-line-quantity-0');
     });
   ```
2. **Run it and check it fails:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx -t "unknown pricing mode moves to SL"`. Expected: FAIL with `expected 'stock-line-product-code-0' to be 'stock-line-quantity-0'`, and vitest lists under **Unhandled Errors** `TypeError: Cannot read properties of undefined (reading 'includes')`: the keydown handler throws in `enterPath` because `prefs.enterStops['Legacy']` is `undefined`, so the focus never moves.
3. **Write the minimal implementation** in `stock-line-grid.tsx`:
   - Import: change the Task 2.3 line to
     ```ts
     import { isDimensionField, normalizePricingMode, shownDimensions, usesDimension } from '@/lib/dimension-columns';
     ```
   - `pathOf` (`:440-443`). Before:
     ```ts
       const pathOf = (rowIndex: number) => {
         const mode = rows[rowIndex]?.pricingMode ?? 'PerUnit';
         return enterPath(mode, prefs.enterStops[mode], isVisible);
       };
     ```
     after:
     ```ts
       const pathOf = (rowIndex: number) => {
         // A missing or unknown mode (an old draft) counts as PerUnit: prefs.enterStops holds only the four modes.
         const mode = normalizePricingMode(rows[rowIndex]?.pricingMode);
         return enterPath(mode, prefs.enterStops[mode], isVisible);
       };
     ```
   **SL** of that line is enabled because `isFieldDisabled` normalizes the mode (Phase 01). Saving such a line is still refused by the zod enum (`features/stock-vouchers/schema.ts:31`), unchanged.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx src/pages/stock-vouchers/utils`. Expected: PASS, with no unhandled error.
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx && git commit -m "fix(stock-vouchers): an unknown pricing mode counts as PerUnit for the Enter stops"`

### Task 2.5 — A dimension cell the line does not use shows "—"

The `<input>` and its id stay. It is disabled, with an empty value and the placeholder "—". This depends on the line's mode, not on `disabled`, so a read-only voucher still shows the numbers its lines use. The value is a plain `value` prop, so "—" writes nothing back to the form, and the payload still sends the stored dimensions (decision 4).

1. **Write the failing tests.** In `stock-line-grid.test.tsx`, after the `typing a code over %s without a pick changes no column` test of Task 2.3, add:
   ```ts
     // A cell its line's mode does not use shows "—", keeps its id and leaves the stored value in the form.
     it('a unit line next to a m³ line shows "—" in its four dimension cells and keeps the stored values', () => {
       const { values } = renderGrid({
         lines: [line({ pricingMode: 'PerUnit', sheetCount: 3, length: 2400, width: 1200, thickness: 5 }), m3Line({ _uiKey: 'b' })],
       });
       for (const field of ['sheet-count', 'length', 'width', 'thickness']) {
         const unused = byId(`stock-line-${field}-0`);
         expect(unused).toBeDisabled();
         expect(unused).toHaveValue(null);
         expect(unused).toHaveAttribute('placeholder', '—');
         const used = byId(`stock-line-${field}-1`);
         expect(used).toBeEnabled();
         expect(used).not.toHaveAttribute('placeholder');
       }
       expect(byId('stock-line-length-1')).toHaveValue(3000);
       expect(values().lines[0]).toMatchObject({ sheetCount: 3, length: 2400, width: 1200, thickness: 5 });
     });

     // "—" follows the line's mode, not the disabled state, so a read-only voucher still shows the numbers it uses.
     it('a read-only voucher shows the dimensions a line uses and "—" where it does not', () => {
       renderGrid({
         readOnly: true,
         lines: [m2Line({ sheetCount: 2, length: 2400 }), line({ _uiKey: 'b', pricingMode: 'PerUnit', length: 2400 })],
       });
       expect(byId('stock-line-length-0')).toBeDisabled();
       expect(byId('stock-line-length-0')).toHaveValue(2400);
       expect(byId('stock-line-length-0')).not.toHaveAttribute('placeholder');
       expect(byId('stock-line-length-1')).toHaveValue(null);
       expect(byId('stock-line-length-1')).toHaveAttribute('placeholder', '—');
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx -t "keeps the stored values|where it does not"`. Expected: both FAIL at `expect(element).toHaveValue(null)`: the first receives `3` (`stock-line-sheet-count-0` shows the stored count of the unit line), the second `2400` (`stock-line-length-1`). Today a disabled cell shows the stored value.
3. **Write the minimal implementation** in `stock-line-grid.tsx`:
   - Import: replace the `@/lib/dimension-columns` line with
     ```ts
     import {
       isDimensionField,
       normalizePricingMode,
       shownDimensions,
       usesDimension,
       type DimensionField,
     } from '@/lib/dimension-columns';
     ```
   - `DimensionCell` (`:278-311`). Before:
     ```tsx
     // Số tấm / Dài / Rộng / Dày: an emptied cell means "no value".
     function DimensionCell({
       field,
       rowIndex,
       label,
       value,
       onCommit,
       disabled,
       error,
     }: {
       field: LineFocusField;
       rowIndex: number;
       label: string;
       value: number | string;
       onCommit: (value: number | '') => void;
       disabled: boolean;
       error?: string;
     }) {
     ```
     after:
     ```tsx
     // Số tấm / Dài / Rộng / Dày: an emptied cell means "no value". A cell its line's mode does not use (`unused`) shows "—"
     // and keeps the stored value in the form, which is still saved and printed.
     function DimensionCell({
       field,
       rowIndex,
       label,
       value,
       onCommit,
       disabled,
       unused,
       error,
     }: {
       field: DimensionField;
       rowIndex: number;
       label: string;
       value: number | string;
       onCommit: (value: number | '') => void;
       disabled: boolean;
       unused: boolean;
       error?: string;
     }) {
     ```
     and in its `<input>` (`:304-308`), before:
     ```tsx
           {...errorProps(field, rowIndex, error)}
           disabled={disabled}
           value={value}
           {...focusProps}
     ```
     after:
     ```tsx
           {...errorProps(field, rowIndex, error)}
           disabled={disabled || unused}
           value={unused ? '' : value}
           placeholder={unused ? '—' : undefined}
           {...focusProps}
     ```
   - `numberInput` (`:505`), before:
     ```ts
       const numberInput = (idx: number, field: 'sheetCount' | 'length' | 'width' | 'thickness', focusField: LineFocusField, label: string) => {
     ```
     after:
     ```ts
       const numberInput = (idx: number, field: 'sheetCount' | 'length' | 'width' | 'thickness', focusField: DimensionField, label: string) => {
     ```
     and in its `<DimensionCell>` (`:516-518`), before:
     ```tsx
               disabled={disabled}
               error={error}
               onCommit={(value) => setLineField(idx, field, value)}
     ```
     after:
     ```tsx
               disabled={disabled}
               unused={!usesDimension(line?.pricingMode, focusField)}
               error={error}
               onCommit={(value) => setLineField(idx, field, value)}
     ```
     `CellError` (`:520`) stays under the input, so a forced error column shows its message under the "—" cell.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers`. Expected: PASS, including `dimension inputs follow the pricing mode` (`:262-280`), the `an emptied %s leaves the line without that value` cases (`:396-411`), `read-only grid disables inputs` (`:340-348`), the forced error test of Task 2.3 and the cubic-line tests of `stock-voucher-form-page.test.tsx` (`on a cell computed from other cells of its line`, `:1743-1894`).
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx && git commit -m "feat(stock-vouchers): a dimension cell the line does not use shows a dash"`

### Task 2.6 — A re-pick clears the line's dimension errors

Grid writes do not re-validate (`setValue` without `shouldValidate`). The form page's watch (`stock-voucher-form-page.tsx:321-337`, whole-row branch of `staleServerErrorKeys`, `server-field-errors.ts:86-91`) clears a **server** error only on a dimension whose value the pick changes. A server error stays on a dimension the pick leaves equal (for example a **Số tấm** that was already `''`, since `applyProduct` sets `sheetCount: ''`), and a client error is never cleared by a pick. The test lives in the grid test file, whose harness has no page watch.

1. **Write the failing test.** In `stock-line-grid.test.tsx`, right after the catalog test `selecting through the catalog dialog applies the same autofill` (it now ends with the `dimensionHeaders()` line of Task 2.3), add:
   ```ts
     // The page's watch clears a server error only on a dimension whose value the pick changes; the grid clears the rest.
     it('a re-pick clears every dimension error of the line', async () => {
       useProductSearchMock.mockImplementation((text: string) => ({
         data: [quickAdded],
         isLoading: false,
         isError: false,
         isFetching: false,
         settledQuery: text,
       }));
       const { form } = renderGrid({ lines: [m2Line({ sheetCount: '' })] });
       act(() => {
         form().setError('lines.0.sheetCount', { type: 'server', message: 'Số tấm phải lớn hơn 0' });
         form().setError('lines.0.width', { type: 'server', message: 'Chiều rộng phải lớn hơn 0' });
         form().setError('lines.0.length', { type: 'custom', message: 'Chiều dài phải lớn hơn 0' });
         form().setError('lines.0.thickness', { type: 'server', message: 'Chiều dày phải lớn hơn 0' });
       });
       expect(byId('stock-line-sheet-count-0')).toHaveAttribute('aria-invalid', 'true');

       await pickFromTypeahead('TL-50'); // quickAdded: PerUnit

       await waitFor(() => expect(form().getValues('lines.0.pricingMode')).toBe('PerUnit'));
       for (const field of ['sheetCount', 'length', 'width', 'thickness'] as const) {
         expect(form().getFieldState(`lines.0.${field}`).error).toBeUndefined();
       }
       expect(dimensionHeaders()).toEqual([]);
     });
   ```
2. **Run it and check it fails:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx -t "a re-pick clears"`. Expected: FAIL with `expected { type: 'server', …(2) } to be undefined` for `lines.0.sheetCount`: nothing clears the errors on a pick. (They would also keep **Số tấm**, **Dài**, **Rộng** and **Dày** forced in after the line became `PerUnit`.)
3. **Write the minimal implementation** at the end of `applyProduct` (`stock-line-grid.tsx:405-432`). Before:
   ```ts
           warehouseId: current.warehouseId || form.getValues('warehouseId'),
         },
         { shouldDirty: true },
       );
     };
   ```
   after:
   ```ts
           warehouseId: current.warehouseId || form.getValues('warehouseId'),
         },
         { shouldDirty: true },
       );
       // A re-pick ends the line's dimension errors. The page's watch drops a server error only where the pick changed
       // the value (Số tấm often stays ''), and a grid write does not re-validate.
       form.clearErrors([`lines.${idx}.sheetCount`, `lines.${idx}.length`, `lines.${idx}.width`, `lines.${idx}.thickness`]);
     };
   ```
   Other errors of the line (Đơn giá, Kho, Ghi chú) are left to the page's watch, as today.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers`. Expected: PASS, including the `server errors clear when the user corrects the field` suite of `stock-voucher-form-page.test.tsx` (`:1523-1960`) and its `quick add and catalog from the stock form` suite (`:1351-1427`).
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx && git commit -m "fix(stock-vouchers): a re-pick clears the line's dimension errors"`

### Task 2.7 — Keep the focused cell in view when the dimension columns change

Hiding columns shifts the columns to their right by 64 to 286 px, and the browser clamps `scrollLeft` (section 01, case 7). The trigger is the shown dimension list as a string, never the array, which is new on every render. A `useLayoutEffect` keyed on that string skips the mount (a ref holds the previous string). When the string changes and `document.activeElement` is inside the grid wrapper, it calls `scrollIntoView({ block: 'nearest', inline: 'nearest' })` on it. The existing scroll margins (`stock-line-grid.css:63-66`, `--stock-grid-left/right`) keep the cell clear of the pinned columns.

1. **Write the failing tests.** In `stock-line-grid.test.tsx`, right before `describe('product quick add', () => {` (`:1316`), add:
   ```ts
     // Hiding columns shifts the columns to the right of them: the focused cell is scrolled back into view.
     describe('scroll anchor', () => {
       it('Ctrl+Delete from Dày of the only m³ line scrolls the cell that takes the focus into view, once', async () => {
         const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
         try {
           const { values } = renderGrid({ lines: [m3Line({ productCode: 'A' }), m2Line({ _uiKey: 'b', productCode: 'B' })] });
           act(() => byId('stock-line-thickness-0').focus());

           fireEvent.keyDown(byId('stock-line-thickness-0'), { key: 'Delete', ctrlKey: true });

           await waitFor(() => expect(values().lines).toHaveLength(1));
           expect(scroll).toHaveBeenCalledTimes(1);
           expect(scroll).toHaveBeenCalledWith({ block: 'nearest', inline: 'nearest' });
           expect(scroll.mock.contexts[0]).toBe(document.activeElement);
           expect(focused()).toBe('stock-line-product-code-0');
         } finally {
           scroll.mockRestore();
         }
       });

       it('a reset that hides columns while the focus is outside the grid scrolls nothing', async () => {
         const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
         try {
           const { form } = renderGrid({ lines: [m3Line()] });
           act(() => byId('stock-line-add').focus()); // the toolbar sits outside the grid wrapper

           act(() => form().reset({ ...form().getValues(), lines: [line()] }));

           await waitFor(() => expect(dimensionHeaders()).toEqual([]));
           expect(scroll).not.toHaveBeenCalled();
         } finally {
           scroll.mockRestore();
         }
       });

       it('typing in a cell, which changes no column, scrolls nothing', () => {
         const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
         try {
           renderGrid({ lines: [m3Line()] });
           const cell = byId('stock-line-length-0');
           act(() => cell.focus());

           fireEvent.change(cell, { target: { value: '2500' } });

           expect(byId('stock-line-length-0')).toHaveValue(2500);
           expect(scroll).not.toHaveBeenCalled();
         } finally {
           scroll.mockRestore();
         }
       });
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx -t "scroll anchor"`. Expected: the first test FAILS with `expected "Mock" to be called 1 times, but got 0 times` (the spy's name may print differently): nothing calls `scrollIntoView` today. The other two pass now; they are guards that fail against a wrong implementation (one that skips the `activeElement` check, or keys the effect on the array, which changes on every keystroke).
3. **Write the minimal implementation** in `stock-line-grid.tsx` (`useLayoutEffect` and `useRef` are already imported, `:2`):
   - After `const columns = …` (`:372`). Before:
     ```ts
       const columns = COLUMNS.filter((c) => isShown(c.key) && !(readOnly && c.key === 'action'));
       const minWidth = columns.reduce((sum, c) => sum + c.width, 0);
     ```
     after:
     ```ts
       const columns = COLUMNS.filter((c) => isShown(c.key) && !(readOnly && c.key === 'action'));
       // Hiding or showing dimension columns shifts the columns to their right: keep the focused cell in view. Keyed on the
       // joined list (a new array every render); the first render only records it.
       const wrapRef = useRef<HTMLDivElement>(null);
       const shownDimsKey = shownDims.join(',');
       const previousShownDimsKey = useRef<string | null>(null);
       useLayoutEffect(() => {
         const previous = previousShownDimsKey.current;
         previousShownDimsKey.current = shownDimsKey;
         if (previous === null || previous === shownDimsKey) return;
         const active = document.activeElement;
         if (active instanceof HTMLElement && wrapRef.current?.contains(active)) {
           active.scrollIntoView({ block: 'nearest', inline: 'nearest' });
         }
       }, [shownDimsKey]);
       const minWidth = columns.reduce((sum, c) => sum + c.width, 0);
     ```
   - The wrapper (`:767-768`). Before:
     ```tsx
           <div
             className="accounting-grid-wrap stock-grid-wrap min-h-0 flex-1"
     ```
     after:
     ```tsx
           <div
             ref={wrapRef}
             className="accounting-grid-wrap stock-grid-wrap min-h-0 flex-1"
     ```
   The `previous === shownDimsKey` check keeps React 18 `StrictMode`'s second mount run (dev only) from scrolling.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx`. Expected: PASS, including every Ctrl+Delete and delete button test (`:1104-1266`) and the column-forcing tests (`:448-559`).
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx && git commit -m "feat(stock-vouchers): keep the focused cell in view when dimension columns change"`

### Task 2.8 — Flash the dimension columns that appear

`useAppearedKeys(shownDims)` marks the newly shown dimension columns, whether the rule or a forced error shows them. The grid puts `col-appeared` on their `<th>` and on every `<td>`, "—" cells included. The hook marks nothing on the first render (opening a saved voucher, restoring a draft). The CSS is Phase 01's.

1. **Write the failing tests.** In `stock-line-grid.test.tsx`, after the `scroll anchor` describe of Task 2.7 (still before `describe('product quick add'`), add:
   ```ts
     describe('columns that appear flash', () => {
       const th = (text: string) => screen.getAllByRole('columnheader').find((el) => el.textContent === text)!;

       it('a m² pick on a unit-only voucher marks the new headers and every cell of the new columns, "—" cells included', async () => {
         renderGrid({ lines: [createEmptyStockLine(0, WH1), line({ _uiKey: 'b' })] });
         expect(document.querySelectorAll('.col-appeared')).toHaveLength(0);

         await pickFromTypeahead('TON01');

         await waitFor(() => expect(dimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']));
         for (const [header, field] of [['Số tấm', 'sheet-count'], ['Dài', 'length'], ['Rộng', 'width']]) {
           expect(th(header)).toHaveClass('col-appeared');
           expect(byId(`stock-line-${field}-0`).closest('td')).toHaveClass('col-appeared');
           expect(byId(`stock-line-${field}-1`).closest('td')).toHaveClass('col-appeared'); // the unit line's "—" cell
         }
         expect(th('SL')).not.toHaveClass('col-appeared');
         expect(byId('stock-line-quantity-1').closest('td')).not.toHaveClass('col-appeared');
         // The flash lasts about 1.2 s.
         await waitFor(() => expect(document.querySelectorAll('.col-appeared')).toHaveLength(0), { timeout: 2000 });
       });

       it('a saved m³ voucher flashes nothing when it opens', () => {
         renderGrid({ lines: [m3Line(), line({ _uiKey: 'b' })] });
         expect(dimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng', 'Dày']);
         expect(document.querySelectorAll('.col-appeared')).toHaveLength(0);
       });
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx -t "flash"`. Expected: the first test FAILS at `expect(th(header)).toHaveClass('col-appeared')` (`Expected the element to have class: col-appeared`, received `cell-number`): the grid sets no such class yet. The second passes now; it guards against marking on mount.
3. **Write the minimal implementation** in `stock-line-grid.tsx`:
   - After `import { formatStockQuantity } from '@/lib/stock-quantity';` (`:20`) add:
     ```ts
     import { useAppearedKeys } from '@/lib/use-appeared-keys';
     ```
   - After `const columns = …` (`:372`). Before:
     ```ts
       const columns = COLUMNS.filter((c) => isShown(c.key) && !(readOnly && c.key === 'action'));
       // Hiding or showing dimension columns shifts the columns to their right: keep the focused cell in view. Keyed on the
     ```
     after:
     ```ts
       const columns = COLUMNS.filter((c) => isShown(c.key) && !(readOnly && c.key === 'action'));
       // Dimension columns that have just appeared flash (`col-appeared`, line-items-grid.css); none on the first render.
       const appeared = useAppearedKeys(shownDims);
       const flashClass = (key: ColumnKey) => (appeared.has(key) ? 'col-appeared' : undefined);
       // Hiding or showing dimension columns shifts the columns to their right: keep the focused cell in view. Keyed on the
     ```
   - The header cell (`:797`). Before:
     ```tsx
                     <th key={c.key} className={cellClass(c)} style={stickyOffset.get(c.key)}>
     ```
     after:
     ```tsx
                     <th key={c.key} className={cellClass(c, flashClass(c.key))} style={stickyOffset.get(c.key)}>
     ```
   - The body cell (`:812`). Before:
     ```tsx
                         <td key={c.key} className={cellClass(c)} style={stickyOffset.get(c.key)}>
     ```
     after:
     ```tsx
                         <td key={c.key} className={cellClass(c, flashClass(c.key))} style={stickyOffset.get(c.key)}>
     ```
   `cellClass` already takes an `extra` class (`:381-389`). Hiding has no effect.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx`. Expected: PASS. The flash test takes about 1.3 s.
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx && git commit -m "feat(stock-vouchers): flash dimension columns that appear"`

### Task 2.9 — Note under **Hiện cột** in **Cột & phím Enter**

Decision 5: no per-user switch for the four columns, only a plain muted line under the **Hiện cột** checkboxes. The Enter-stop table is unchanged. (Mockup 03 draws a green box with a **Mới** badge; the text wins.)

1. **Write the failing test.** In `stock-grid-settings-popover.test.tsx`, change the import (`:2`) to:
   ```ts
   import { fireEvent, render, screen, within } from '@testing-library/react';
   ```
   After the existing test (ends at `:34`), add:
   ```ts
     it('says under the Hiện cột checkboxes that the dimension columns follow the goods of the voucher', () => {
       render(
         <StockGridSettingsPopover
           prefs={DEFAULT_STOCK_GRID_PREFS}
           onToggleColumn={vi.fn()}
           onToggleEnterStop={vi.fn()}
           onReset={vi.fn()}
         />,
       );
       fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));

       const note = screen.getByText('Số tấm, Dài, Rộng, Dày tự hiện theo hàng tính md, m², m³ có trong phiếu.');
       expect(note.tagName).toBe('P');
       expect(note).toHaveClass('text-xs', 'text-muted-foreground');
       const section = screen.getByRole('heading', { name: 'Hiện cột' }).closest('section')!;
       expect(section).toContainElement(note);
       const checkboxes = within(section).getAllByRole('checkbox');
       const last = checkboxes[checkboxes.length - 1];
       expect(last.compareDocumentPosition(note) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
     });
   ```
2. **Run it and check it fails:** `npx vitest run src/pages/stock-vouchers/components/stock-grid-settings-popover.test.tsx`. Expected: the new test FAILS with `TestingLibraryElementError: Unable to find an element with the text: Số tấm, Dài, Rộng, Dày tự hiện theo hàng tính md, m², m³ có trong phiếu.` The existing test passes.
3. **Write the minimal implementation** in `stock-grid-settings-popover.tsx` (`:38-47` at `e61e4f1b`, `:39-48` after Phase 01's import split). Before:
   ```tsx
                 </label>
               ))}
             </div>
           </section>
   ```
   after:
   ```tsx
                 </label>
               ))}
             </div>
             <p className="text-xs text-muted-foreground">Số tấm, Dài, Rộng, Dày tự hiện theo hàng tính md, m², m³ có trong phiếu.</p>
           </section>
   ```
   (The anchor is unique: the Enter-stop section ends with `</table>` and `</div>`, not `))}`.) If the real-browser check in Phase 04 finds the line too tight under the last checkbox row, add a top margin there.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers/components`. Expected: PASS, including the grid's popover tests `the settings popover shows a hidden column at once and stores the choice` and `a read-only grid can still reveal a hidden column from the popover` (`stock-line-grid.test.tsx:593-609`).
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.test.tsx && git commit -m "feat(stock-vouchers): note on automatic dimension columns in the grid settings"`

### Task 2.10 — Form page: a dimension error the line does not use focuses its **Mã hàng**

The server checks a new or changed line against the product's current mode, and the payload sends no `pricingMode`, so it can return a dimension error on a column the line's screen mode does not use (section 01, case 12). Task 2.3 forces that column in, but its cell is a disabled "—": `focusFirstError` (`stock-voucher-form-page.tsx:523-544`) cannot focus it and falls back to **Thêm dòng**, skipping later errors. `focusIdsOf` (`:189-200`) is module-level and does not know the line modes, so it gets an optional `lines` argument, which `focusFirstError` fills from `form.getValues('lines')`. Only a dimension key on a line whose mode does not use it maps to `stock-line-product-code-<row>`. Other keys keep their targets, quantity keys included (Risks: a metre line whose product became `PerUnit` keeps going to **Số tấm**; guard `:2566-2592`).

**Mã hàng is pinned** (`sticky: 'left'`), so focusing it scrolls nothing, and the forced "—" cell with its message can sit under the pinned columns when the grid is scrolled right (the Task 2.7 anchor may even scroll the other way, as the column comes in). So `focusFirstError` also scrolls that "—" cell into view, inside the same timeout and before the focus. Only when no main-area field (the partner) has an error, so that a header error never scrolls the grid. The helper `unusedDimensionCellId(key, lines)` gives the cell id for both the remap and the scroll (Phase 04 Task 4.4 step 9 checks it in the real browser).

1. **Write the failing tests.** In `stock-voucher-form-page.test.tsx`, after the `a server error on SL of a line priced %s focuses the cell that corrects it` test (`:2566-2592`), add:
   ```ts
     // The server checks a new or changed line against the catalog's current mode, so it can reject a dimension that the
     // line's mode on screen does not use. That cell is a disabled "—": the caret goes to the line's Mã hàng to re-pick.
     describe('a server error on a dimension', () => {
       function openWithSecondLine(overrides: Partial<StockVoucherLine>, details: Record<string, string[]>) {
         routeParams.id = V1;
         const first = voucher().lines[0];
         voucherState.data = voucher({ lines: [first, { ...first, id: L2, sortOrder: 1, ...overrides }] });
         updateMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', details));
         renderPage('In');
       }

       // Mã hàng is pinned, so the focus scrolls nothing: the "—" cell of the error is scrolled into view by the page.
       it('that the line does not use shows its column, scrolls its cell into view and focuses Mã hàng of that line', async () => {
         const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
         try {
           openWithSecondLine(
             { pricingMode: 'PerLinearMeter', unitName: 'm', sheetCount: 2, length: 3000, quantity: 6 },
             { 'lines[1].width': ['Chiều rộng phải lớn hơn 0'] },
           );
           expect(screen.queryByRole('columnheader', { name: 'Rộng' })).toBeNull();

           fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

           await waitFor(() => expect(byId('stock-line-width-1')).toHaveAttribute('aria-invalid', 'true'));
           expect(screen.getByRole('columnheader', { name: 'Rộng' })).toBeInTheDocument();
           await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-1')));
           expect(scroll.mock.contexts).toContain(byId('stock-line-width-1'));
         } finally {
           scroll.mockRestore();
         }
       });

       it('that the first line does not use focuses its Mã hàng, and the other errors of the save stay', async () => {
         openWithSecondLine({}, {
           'lines[0].width': ['Chiều rộng phải lớn hơn 0'],
           'lines[1].unitPrice': ['Đơn giá không hợp lệ'],
         });

         fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

         await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-0')));
         expect(byId('stock-line-width-0')).toHaveAttribute('aria-invalid', 'true');
         expect(byId('stock-line-unit-price-1')).toHaveAttribute('aria-invalid', 'true');
       });

       // What the server sends when a unit line's product became md: Số tấm first. Its form key is sheetCount, its cell
       // sheet-count, so a check on the form key would miss it.
       it('Số tấm and Dài on a unit line focus its Mã hàng', async () => {
         routeParams.id = V1;
         voucherState.data = voucher();
         updateMock.mockRejectedValueOnce(
           apiError(400, 'VALIDATION', {
             'lines[0].sheetCount': ['Số tấm phải lớn hơn 0'],
             'lines[0].length': ['Chiều dài phải lớn hơn 0'],
           }),
         );
         renderPage('In');

         fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

         await waitFor(() => expect(byId('stock-line-sheet-count-0')).toHaveAttribute('aria-invalid', 'true'));
         await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-0')));
       });

       // Rộng is shown by the m² line, not forced: the remap follows the mode of the line with the error.
       it('on a column that another line shows focuses Mã hàng of the line that does not use it', async () => {
         openWithSecondLine(
           { pricingMode: 'PerSquareMeter', unitName: 'm²', sheetCount: 2, length: 3000, width: 1000, quantity: 6 },
           { 'lines[0].width': ['Chiều rộng phải lớn hơn 0'] },
         );
         expect(screen.getByRole('columnheader', { name: 'Rộng' })).toBeInTheDocument();

         fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

         await waitFor(() => expect(byId('stock-line-width-0')).toHaveAttribute('aria-invalid', 'true'));
         await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-0')));
       });

       it('that the line uses still focuses that cell', async () => {
         openWithSecondLine(
           { pricingMode: 'PerSquareMeter', unitName: 'm²', sheetCount: 2, length: 3000, width: 1000, quantity: 6 },
           { 'lines[1].width': ['Chiều rộng quá lớn.'] },
         );

         fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

         await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-width-1')));
       });
     });
   ```
   The fixtures follow `:2582-2586`: `voucher()`'s `PerUnit` line, a second line `L2`, `updateMock` rejected with a 400 `VALIDATION`. These are the five stock form tests of SUMMARY → Testing, in its order: [unit, md] `lines[1].width`; [unit, unit] `lines[0].width` + `lines[1].unitPrice`; [unit] `lines[0].sheetCount` + `lines[0].length`; [unit, m²] `lines[0].width`; the companion guard.
2. **Run them and check they fail:** `npx vitest run src/pages/stock-vouchers/stock-voucher-form-page.test.tsx -t "a server error on a dimension"`. Expected: the first four FAIL at the focus `waitFor` with `expected <button type="button" …(2)>…(1)</button> to be <input class="cell-input" …(8)></input>`: the focus went to **Thêm dòng** (`stock-line-add`), because `focusIdsOf` gives the disabled `stock-line-<cell>-<row>` (`width`, or `sheet-count` for the third test), which refuses the focus. The column and `aria-invalid` assertions before it already pass (Task 2.3 forces the column; in the fourth test the m² line shows **Rộng**). The first test fails at that same focus `waitFor` with the same message, so its RED run never reaches the scroll assertion: that assertion is checked against GREEN. Once the remap is in, an implementation without the reveal (no `scrollIntoView` of the "—" cell in `focusFirstError`) fails it with `expected [ …(n) ] to include <input …>`. The fifth test passes now; it guards against mapping every dimension key to **Mã hàng**. Two of them guard against wrong fixes: the third fails against a check on the form key (`isDimensionField(line[2])` skips `sheetCount`, the first key), the fourth against a remap keyed on hidden or forced columns (`!shownDimensions(lines).includes(cell)`), since **Rộng** is shown by line 1. A review run on a copy of this phase confirmed all four RED reasons and both mutants.
3. **Write the minimal implementation** in `stock-voucher-form-page.tsx`:
   - After `import type { ConfirmFlags } from '@/lib/confirm-required';` (`:63`) add:
     ```ts
     import { isDimensionField, usesDimension } from '@/lib/dimension-columns';
     ```
   - `focusIdsOf` (`:189-193`). Before:
     ```ts
     // The ids of the controls that correct the error of a key, best first. Accepts form keys and server keys
     // (`lines[3].width`).
     function focusIdsOf(key: string): string[] {
       const line = /^lines(?:\.|\[)(\d+)(?:\]\.|\.)(\w+)$/.exec(key);
       if (line) {
     ```
     after:
     ```ts
     // The ids of the controls that correct the error of a key, best first. Accepts form keys and server keys
     // (`lines[3].width`). Given the lines, a dimension that the line's mode does not use maps to the line's Mã hàng: its
     // cell is a disabled "—", and only a re-pick fixes the line.
     function focusIdsOf(key: string, lines?: readonly { pricingMode?: unknown }[]): string[] {
       const line = /^lines(?:\.|\[)(\d+)(?:\]\.|\.)(\w+)$/.exec(key);
       if (line) {
         if (lines && unusedDimensionCellId(key, lines)) return [`stock-line-product-code-${line[1]}`];
     ```
     The rest of the function (the **SL** sources and the header ids) is unchanged.
   - After `focusIdsOf` (before `productCodeCell`), add the helper:
     ```ts
     // The "—" cell of a dimension error on a line whose mode does not use that dimension, or null.
     function unusedDimensionCellId(key: string, lines: readonly { pricingMode?: unknown }[]): string | null {
       const line = /^lines(?:\.|\[)(\d+)(?:\]\.|\.)(\w+)$/.exec(key);
       const cell = line ? LINE_CELL_IDS[line[2]] : undefined;
       if (!line || !cell || !isDimensionField(cell) || usesDimension(lines[Number(line[1])]?.pricingMode, cell)) return null;
       return `stock-line-${cell}-${line[1]}`;
     }
     ```
   - `focusFirstError` (`:526-533`). Before:
     ```ts
         const firstIdsOf = (match: (key: string) => boolean) =>
           keys.filter(match).map(focusIdsOf).find((ids) => ids.length > 0) ?? [];
         const mainIds = firstIdsOf((k) => tabOfField(k) === null);
         const tabIds = tab ? firstIdsOf((k) => tabOfField(k) === tab) : [];
         if (mainIds.length === 0 && !tab) return;
         // After the panel is shown; `form.setFocus` cannot reach these controlled inputs and grid cells.
         setTimeout(() => {
           const candidates = [
     ```
     after:
     ```ts
         const lines = form.getValues('lines');
         const firstIdsOf = (match: (key: string) => boolean) =>
           keys.filter(match).map((key) => focusIdsOf(key, lines)).find((ids) => ids.length > 0) ?? [];
         const mainIds = firstIdsOf((k) => tabOfField(k) === null);
         const tabIds = tab ? firstIdsOf((k) => tabOfField(k) === tab) : [];
         // Mã hàng is pinned, so focusing it scrolls nothing: bring the "—" cell of a remapped error and its message into
         // view. The key is the one `tabIds` was taken from; a main-area error (the partner) skips this.
         const firstTabKey = tab ? keys.find((k) => tabOfField(k) === tab && focusIdsOf(k, lines).length > 0) : undefined;
         const revealId = mainIds.length === 0 && firstTabKey ? unusedDimensionCellId(firstTabKey, lines) : null;
         if (mainIds.length === 0 && !tab) return;
         // After the panel is shown; `form.setFocus` cannot reach these controlled inputs and grid cells.
         setTimeout(() => {
           if (revealId) document.getElementById(revealId)?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
           const candidates = [
     ```
   The module-level `focusIdsOf` and `unusedDimensionCellId` are hoisted function declarations, and `LINE_CELL_IDS` (`:159`), `keys`, `tab`, `mainIds`, `tabIds` and `lines` are all in scope at those lines. `form.getValues('lines')` is `StockLineFormValues[]`, assignable to `readonly { pricingMode?: unknown }[]`. The scroll sits inside the timeout because the tab panel and the forced column render first; the "—" cell does not exist before the re-render.
   A client (zod) dimension error only arises on a mode that uses the column (`features/stock-vouchers/schema.ts:48-53`), so the client path keeps its targets.
4. **Run the tests and check they pass:** `npx vitest run src/pages/stock-vouchers`. Expected: PASS, including `validation details map lines[0].width onto the grid cell` (`:859-869`; its focus now lands on `stock-line-product-code-0`, which it does not assert), `a server error on SL of a line priced %s …` (`:2566-2592`, **Số tấm** for metre lines), `saving an incomplete line from another tab …` (`:2544-2552`) and `saving a voucher with no lines …` (`:2554-2564`).
5. **Commit:** `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx frontend/src/pages/stock-vouchers/stock-voucher-form-page.test.tsx && git commit -m "fix(stock-vouchers): a dimension error the line does not use focuses its Mã hàng"`

## Verification

`<phase base>` is the HEAD commit before Task 2.1, the first commit of this phase: the value recorded with `git rev-parse HEAD` at the start (Tasks, "Before Task 2.1"). Do not reuse Phase 01's base: that diff would list `pages/quotations/components/line-items-grid.css`, which Phase 01 Task 1.5 changed, and the "must be empty" check could not pass.

From `$WT/frontend`, each line its own call that begins with the `WT=` assignment (Phase 00), with `<phase base>` written out as the recorded hash:

```bash
WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
cd "$WT/frontend" && npx vitest run src/pages/stock-vouchers src/lib --testTimeout=30000 --maxWorkers=3
cd "$WT/frontend" && npm run typecheck
cd "$WT/frontend" && npm run lint
cd "$WT/frontend" && git diff --stat <phase base>.. -- src/pages/quotations src/features/help   # must be empty
cd "$WT/frontend" && git diff <phase base> --numstat
cd "$WT/frontend" && git diff <phase base> --numstat --ignore-cr-at-eol                         # same numbers: no line-ending change
```

- Tests: no failure beyond `BASELINE.md`. With Phase 01 and this phase applied, `src/lib` plus `src/pages/stock-vouchers` ran 47 files, 742 tests green (counts are indicative; the criterion is no failure beyond `BASELINE.md`), with typecheck (the four test files included) and lint clean. A review run added the two form tests of Task 2.10 (`Số tấm and Dài on a unit line …`, `on a column that another line shows …`) to a copy: `src/pages/stock-vouchers` stayed green and the test files still typecheck.
- Status: set `**Status:** [x] done (<today>)` in this file (the worktree copy) and commit it: `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-02-stock-grid.md && git commit -m "docs(plan): phase 02 done"`. Do not tick the SUMMARY Phases list (Task 4.15 step 8 does).
- `npm run typecheck` uses `tsconfig.app.json`, which excludes test files, and vitest does not check types. To check the four test files, write a throwaway `tsconfig.tests.json` next to `tsconfig.app.json` (`"extends": "./tsconfig.app.json"`, `include` = the four test files and `src/vite-env.d.ts`, `"exclude": []`), run `npx tsc -p tsconfig.tests.json`, expect no output, then delete the file; never stage it.
- Lint: no error or warning beyond `BASELINE.md`.
- `npm run help:stale` now also lists `stock-in.form` and `stock-out.form` (their `sources` cover `frontend/src/pages/stock-vouchers/**`). That is expected: Phase 04 updates them, committed last.

## Exit Criteria

- The four existing stock grid tests carry the new values (12 and 16 headers in the header lists, 11 read-only headers, 1142 px), and no other existing test was edited except the catalog test (`:319-338`) and the pick test (`:616-628`), which gained column and caret assertions.
- Regression guards pass unchanged: `stock-voucher-form-page.test.tsx:859-869` and `:2566-2592`, the keyboard suites `stock-line-grid.test.tsx:891-1094` and `:1208-1266`, `line-navigation.test.ts`, `use-stock-grid-prefs.test.ts` (extended), `stock-grid-settings-popover.test.tsx` (extended).
- No change under `src/pages/quotations`, `src/features/help`, the backend or the payload.
- **Brainstorm Behaviour bullets on the stock grid:** Pick → Task 2.3 (b, c, f, h); Typing in **Mã hàng** → Task 2.3 (b, typing test); Row delete → Task 2.3 (g), the survivor focus already exists (`stock-line-grid.tsx:366-371`); Column set changes → Task 2.7; Unused cell → Task 2.5; Flash (classes) → Task 2.8; Server or client error → Tasks 2.2, 2.3 (e), 2.5 (`CellError` under "—"), 2.10; Re-pick → Task 2.6; **Cột & phím Enter** → Task 2.9.
- **Section 01 cases on the stock grid:**

  | Case | Covered by |
  |---|---|
  | 1 Blank line ranks as `PerUnit`, only **SL** | Task 2.3: `dimension columns with unit lines`, the blank-line typing test, the "before" assertions of the pick tests |
  | 2 m² pick by typeahead, catalog or quick add | Task 2.3: `onSelect` path; tests (c), (f), (h) |
  | 3 Catalog pick ends on **Số tấm** | Task 2.3 (c): caret assertion after the catalog dialog |
  | 4 md lines, then an m³ line: new columns flash, other lines show "—" | Task 2.3 (rule), Task 2.5 ("—"), Task 2.8 (flash on a pick that adds columns, "—" cells included) |
  | 5 Ctrl+Delete from **Dày** of the only m³ line, m² neighbour | Task 2.3 (g); Task 2.7 (scroll anchor on the same path) |
  | 7 Delete or re-pick while scrolled right | Task 2.7 |
  | 8 Typing in **Mã hàng** without a pick | Task 2.3: `typing a code over %s without a pick changes no column` |
  | 9 Saved voucher or draft: right columns on first paint, no flash, missing mode = `PerUnit` | Task 2.3 (`a line without a mode`), Task 2.8 (`a saved m³ voucher flashes nothing`), Task 2.4 (Enter path) |
  | 10 Read-only voucher | Task 2.5: `a read-only voucher shows the dimensions a line uses and "—" where it does not` |
  | 11 Client error on a dimension of an m² line | No code: nesting keeps a line's own columns shown; Task 2.3 (e) shows a dimension message under its cell |
  | 12 Server 400 on a dimension the line's mode does not use | Task 2.2, Task 2.3 (e), Task 2.10 (the five form tests: [unit, md], [unit, unit], [unit] with `sheetCount` + `length`, [unit, m²], and the companion guard; the first also asserts the "—" cell is scrolled into view) |
  | 13 Re-pick after a failed save | Task 2.6 |
  | 14 One md and one m² line | Task 2.3: `dimension columns with a md and a m² line` (rank, not union order) |
  | 18 50+ rows | Task 2.3: `shownDimensions` is one pass over the rows per render; no memo |

- Every new behaviour test failed first for the reason given in its task, and passes now.
