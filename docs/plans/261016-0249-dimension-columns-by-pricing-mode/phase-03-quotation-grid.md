# Phase 03 — Quotation grid

**Status:** [ ] pending
**Complexity:** L
**Depends on:** Phase 01 only (`frontend/src/lib/dimension-columns.ts`, `frontend/src/lib/use-appeared-keys.ts`, the `col-appeared` rules in `line-items-grid.css` and the token `--qldh-col-appeared-head-bg`). Phase 02 is not needed.
**Worktree:** If run in the parallel worktree of SUMMARY → Order, `$WT` below means `/D/Projects/QLBanHang/.claude/worktrees/dimension-columns-quotation`.

## Objective

Make the quotation grid (`frontend/src/pages/quotations/components/line-items-grid.tsx`) follow the shared rule, and fix the three quotation defects in the same code (brainstorm SUMMARY → Quotation grid changes, Behaviour, Testing):

- the one 220 px column **D × R × Dày × Tấm** becomes four columns **Số tấm**, **Dài**, **Rộng**, **Dày**, with the stock grid's widths and order, built from one ordered column list;
- the dimension columns follow the highest pricing mode of the lines; a cell that its row does not use shows "—"; a missing or unknown mode counts as `PerUnit`;
- the table gets an inline `min-width` (fixed widths plus 200 px for **Tên hàng**) and the empty row spans the shown columns;
- Enter skips cells that are not rendered, so a user without `quotations.view_cost` is no longer stuck in **Đơn giá bán**;
- **Mã hàng** no longer opens its list on focus (decision 11), so an Enter into a filled code cell does not re-pick the product;
- a row delete (Ctrl+Delete or the trash button) moves the focus to the row that takes its place before the remove, and the editing state follows the row;
- new columns flash, and the focused cell is scrolled back into view when the columns change.

Frontend only. No backend, API, payload or database change. The help text (`quotations.form.md`) is Phase 04, committed after the real-browser check.

### Section 01 cases covered here

| Case | Where |
|---|---|
| 1 Blank line counts as `PerUnit` | Task 3.3, test "shows none for a blank line or unit lines" (`createEmptyLine(0)`) |
| 4 md rows, then an m³ row | Task 3.4 ("—" cells), Task 3.10 (flash test: md + unit rows, pick m³) |
| 6 Delete on the quotation | Task 3.8 (survivor focus, `line-add`, rows from `getValues`, trash button, real typeahead), Task 3.9 (editing keys), Task 3.7 (`openOnFocus={false}`) |
| 7 Delete or re-pick while scrolled | Task 3.11 (scroll anchor) |
| 8 Typing in **Mã hàng** without a pick | Task 3.3, tests "shows none … while a code is typed" and the m³ re-pick test (typing first) |
| 9 Saved quotation, draft, **Nhân bản** | Task 3.5 (missing or unknown mode), Task 3.10 ("marks nothing on mount") |
| 14 md + m² | Task 3.3, test "shows Số tấm, Dài and Rộng for an md line next to an m² line" |
| 15 Re-pick m³ as m² | Task 3.3, test "hides Dày at once …, and keeps its value" |
| 16 Enter without `view_cost` | Task 3.6, Task 3.7 (real typeahead, both permissions) |
| 17 Long names, columns change | Task 3.3 (`min-width` keeps 200 px for **Tên hàng**); re-wrap is accepted, checked in the real browser (Phase 04) |
| 18 50+ rows | Task 3.3: `shownDimensions(rows)` is one pass per render, no memo. No test: there is nothing to measure in jsdom |

## Files

- `frontend/src/pages/quotations/components/line-items-grid.tsx`
- `frontend/src/pages/quotations/components/line-items-grid.test.tsx`
- `frontend/src/pages/quotations/components/line-items-grid.typeahead.test.tsx` (new)
- `frontend/src/pages/quotations/components/line-items-grid.css` (the `.dxr-cell` rules go; `.accounting-grid { min-width: 1360px }` stays, because the cash, allocation and debt grids import this file and set their widths inline)
- `frontend/src/pages/quotations/components/product-typeahead-cell.tsx` (prop comment only)
- `frontend/src/pages/quotations/components/product-typeahead-cell.test.tsx` (comment only)

Commands run from `frontend/`. Line numbers are at `qlbanhang/main` e61e4f1b. Tasks run in order: each task's RED is measured on the code left by the task before it. Every Bash call starts with the `WT=` assignment of Phase 00 (in the parallel worktree: with the path of the **Worktree** line above). Every command that runs npm, npx, vitest, a grep of `src` or `git checkout -- tsconfig.app.tsbuildinfo` starts with `cd "$WT/frontend" &&` (a new Bash call starts in the main checkout, so a bare command would run against the wrong tree; in the parallel worktree `$WT` is that worktree's path). Commit lines start at `$WT`, so their paths begin with `frontend/`. Add a second `-m` with the `Co-Authored-By` trailer from the session's attribution instructions to every commit line below, as Phase 00 does. Under load, add `--testTimeout=30000 --maxWorkers=3` to the vitest commands (SUMMARY → Risks).

**Test harness rules** (SUMMARY → Testing):

- vitest runs with `css: false`, so only the `col-appeared` class can be asserted; colours and reduced motion are checked in the real browser (Phase 04).
- Do not switch on fake timers in the grid tests: RTL's `waitFor` does not advance them. To see the flash class go, use `waitFor(…, { timeout: 2000 })`.
- `src/test/setup.ts:27-29` stubs `Element.prototype.scrollIntoView`, so `vi.spyOn(Element.prototype, 'scrollIntoView')` works; restore it after the test.
- `line-items-grid.test.tsx` mocks `ProductTypeaheadCell` for the whole file (`:14-25`). Task 3.1 makes the mock render `id={props.inputId}` and record props per id. Every fixture line of a new test has a `_uiKey`: without it the `<tr>` key and the editing key fall back to `field.id`, which react-hook-form 7.76 regenerates on every changed nested `setValue`.

## Tasks

Before Task 3.1: set **Status:** to `[-] in progress` in this file and commit it (SUMMARY → Execution notes): `cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-03-quotation-grid.md && git commit -m "docs(plan): phase 03 in progress"`. Then record the `<phase base>` (plan SUMMARY → Execution notes): `cd "$WT" && git rev-parse HEAD`; write the hash down for the Verification.

### Task 3.1 — Test harness: ids on the mocked code cell, props per id, keyed fixtures

1. **Write the failing test:** none. This task changes only the test harness, not production code, so no test can fail for a behaviour reason. It adds one harness check, which passes at once.
2. **Run it and check it fails:** skipped (see step 1). The 11 existing tests must stay green after step 3.
3. **Write the minimal implementation:** the harness change in `line-items-grid.test.tsx`.

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   import { useForm } from 'react-hook-form';
   ```
   After:
   ```tsx
   import { useForm, type UseFormReturn } from 'react-hook-form';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   const cellProps = vi.hoisted(() => ({ last: undefined as Record<string, unknown> | undefined }));

   vi.mock('./product-typeahead-cell', () => ({
     ProductTypeaheadCell: (props: { value: string; onChange: (value: string) => void }) => {
       cellProps.last = props;
       return (
         <input
           aria-label="product-code"
   ```
   After:
   ```tsx
   // `last`: the props of the last product cell rendered. `byId`: the props of each product cell, by its input id.
   const cellProps = vi.hoisted(() => ({
     last: undefined as Record<string, unknown> | undefined,
     byId: {} as Record<string, Record<string, unknown>>,
   }));

   vi.mock('./product-typeahead-cell', () => ({
     ProductTypeaheadCell: (props: { value: string; onChange: (value: string) => void; inputId?: string }) => {
       cellProps.last = props;
       if (props.inputId) cellProps.byId[props.inputId] = props;
       return (
         <input
           id={props.inputId}
           aria-label="product-code"
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   function setCostPermission(enabled: boolean) {
   ```
   After:
   ```tsx
   // A line with a stable _uiKey, as every production line has (createEmptyLine, toFormDefaults). Without it the <tr>
   // key falls back to field.id, which react-hook-form regenerates on every changed nested setValue.
   function line(uiKey: string, overrides: Partial<QuotationLineFormValues> = {}): QuotationLineFormValues {
     return {
       _uiKey: uiKey,
       sortOrder: 0,
       productCode: `P-${uiKey}`,
       productName: `Name ${uiKey}`,
       unitName: 'pcs',
       pricingMode: 'PerUnit',
       quantity: 1,
       unitPrice: 100,
       ...overrides,
     };
   }

   function setCostPermission(enabled: boolean) {
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   function renderGrid(lines: QuotationLineFormValues[] = baseLines) {
     function Harness() {
   ```
   After:
   ```tsx
   function renderGrid(lines: QuotationLineFormValues[] = baseLines) {
     const formRef: { current: UseFormReturn<QuotationFormValues, unknown, QuotationFormParsed> | null } = {
       current: null,
     };
     function Harness() {
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
       return <LineItemsGrid form={form} />;
     }

     return render(<Harness />);
   }
   ```
   After:
   ```tsx
       formRef.current = form;
       return <LineItemsGrid form={form} />;
     }

     const view = render(<Harness />);
     return { ...view, form: () => formRef.current! };
   }

   function byId(id: string): HTMLInputElement {
     const el = document.getElementById(id);
     if (!el) throw new Error(`#${id} not found`);
     return el as HTMLInputElement;
   }
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
     beforeEach(() => {
       setCostPermission(false);
     });
   ```
   After:
   ```tsx
     beforeEach(() => {
       setCostPermission(false);
       cellProps.byId = {};
     });

     it('gives each code cell its id and keeps the props of each cell', () => {
       const { form } = renderGrid([line('a'), line('b')]);

       expect(byId('line-product-code-1')).toHaveValue('P-b');
       expect(cellProps.byId['line-product-code-1']?.nextFocusId).toBe('line-name-1');
       expect(form().getValues('lines.1._uiKey')).toBe('b');
     });
   ```
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: PASS, 12 tests (the 11 existing ones query by label and are not affected by the new id).
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "test(quotations): give the mocked code cell its id and key the grid fixtures"`

### Task 3.2 — Four dimension columns from one column list, and the new Enter order

1. **Write the failing test** in `line-items-grid.test.tsx`.

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   import { fireEvent, render, screen, waitFor } from '@testing-library/react';
   ```
   After:
   ```tsx
   import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   function setCostPermission(enabled: boolean) {
   ```
   After:
   ```tsx
   const m3 = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
     line(uiKey, {
       pricingMode: 'PerCubicMeter',
       unitName: 'm³',
       sheetCount: 2,
       length: 3000,
       width: 100,
       thickness: 50,
       quantity: 0,
       ...overrides,
     });

   function setCostPermission(enabled: boolean) {
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   describe('LineItemsGrid', () => {
   ```
   After:
   ```tsx
   const focused = () => document.activeElement?.id;

   function headers() {
     return screen.getAllByRole('columnheader').map((th) => th.textContent);
   }

   // Presses Enter (Shift+Enter with `shift`) in a cell, focusing it first.
   function enter(id: string, shift = false) {
     const el = byId(id);
     act(() => el.focus());
     fireEvent.keyDown(el, { key: 'Enter', shiftKey: shift });
   }

   describe('LineItemsGrid', () => {
   ```

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     it('shows the dimensions as four columns and walks them from Số tấm to Dày', () => {
       renderGrid([m3('a')]);

       expect(headers()).toEqual([
         '#', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'Đơn giá bán', 'Thành tiền bán', '',
       ]);
       for (const id of ['line-sheet-count-0', 'line-length-0', 'line-width-0', 'line-thickness-0']) {
         expect(byId(id)).toHaveClass('cell-input', 'cell-number', 'tabular-nums');
         expect(byId(id).closest('td')).toHaveClass('cell-number');
         expect(byId(id)).not.toHaveAttribute('placeholder');
       }

       enter('line-unit-0');
       expect(focused()).toBe('line-sheet-count-0');
       enter('line-sheet-count-0');
       expect(focused()).toBe('line-length-0');
       enter('line-length-0');
       expect(focused()).toBe('line-width-0');
       enter('line-width-0');
       expect(focused()).toBe('line-thickness-0');
       enter('line-thickness-0');
       expect(focused()).toBe('line-unit-price-0');
     });
   ```
2. **Run it and check it fails:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 1 FAIL, "shows the dimensions as four columns …", at `expect(headers()).toEqual([...])` with `expected [ '#', 'Mã hàng', 'Tên hàng', …(6) ] to deeply equal [ '#', 'Mã hàng', 'Tên hàng', …(9) ]`; the diff shows `"D × R × Dày × Tấm"` where `"Số tấm", "Dài", "Rộng", "Dày"` are expected. Why: the base renders one combined header (`line-items-grid.tsx:263`). The Enter walk would fail next, because `LINE_FOCUS_FIELDS` (`:25-36`) puts `sheet-count` after `thickness`.
3. **Write the minimal implementation.** The column list keeps `key`, `header`, `width` and a `cost` flag. It has no `numeric` flag: the cells stay inline JSX with their own classes, so nothing would read it. In this task all four dimensions are always shown; Task 3.3 applies the rule.

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
   import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
   ```
   After:
   ```tsx
   import { DIMENSION_FIELDS, isDimensionField, type DimensionField } from '@/lib/dimension-columns';
   import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     'unit',
     'length',
     'width',
     'thickness',
     'sheet-count',
     'quantity',
     'unit-price',
     'unit-cost',
   ] as const;

   type LineFocusField = (typeof LINE_FOCUS_FIELDS)[number];
   ```
   After:
   ```tsx
     'unit',
     'sheet-count',
     'length',
     'width',
     'thickness',
     'quantity',
     'unit-price',
     'unit-cost',
   ] as const;

   type LineFocusField = (typeof LINE_FOCUS_FIELDS)[number];

   type ColumnKey =
     | 'row-no' | 'product-code' | 'name' | 'unit' | DimensionField | 'quantity' | 'unit-price' | 'line-total'
     | 'unit-cost' | 'line-cost' | 'line-profit' | 'action';

   interface GridColumn {
     key: ColumnKey;
     header: string;
     /** Undefined for Tên hàng, which takes the rest of the table width. */
     width?: number;
     /** Shown only with quotations.view_cost. */
     cost?: boolean;
   }

   // One list for the <colgroup>, the <thead> and the empty row's colSpan. The dimension widths are the stock grid's.
   const COLUMNS: readonly GridColumn[] = [
     { key: 'row-no', header: '#', width: 42 },
     { key: 'product-code', header: 'Mã hàng', width: 130 },
     { key: 'name', header: 'Tên hàng' },
     { key: 'unit', header: 'ĐVT', width: 58 },
     { key: 'sheet-count', header: 'Số tấm', width: 70 },
     { key: 'length', header: 'Dài', width: 76 },
     { key: 'width', header: 'Rộng', width: 76 },
     { key: 'thickness', header: 'Dày', width: 64 },
     { key: 'quantity', header: 'SL', width: 82 },
     { key: 'unit-price', header: 'Đơn giá bán', width: 104 },
     { key: 'line-total', header: 'Thành tiền bán', width: 122 },
     { key: 'unit-cost', header: 'Đơn giá nhập', width: 112, cost: true },
     { key: 'line-cost', header: 'Thành tiền nhập', width: 122, cost: true },
     { key: 'line-profit', header: 'Lợi nhuận', width: 122, cost: true },
     { key: 'action', header: '', width: 42 },
   ];

   const DIMENSION_INPUTS: Record<DimensionField, { name: 'sheetCount' | 'length' | 'width' | 'thickness'; label: string }> = {
     'sheet-count': { name: 'sheetCount', label: 'Số tấm' },
     length: { name: 'length', label: 'Dài' },
     width: { name: 'width', label: 'Rộng' },
     thickness: { name: 'thickness', label: 'Dày' },
   };
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));
   ```
   After:
   ```tsx
     const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));
     const dims: readonly DimensionField[] = DIMENSION_FIELDS;
     const columns = COLUMNS.filter((c) => (isDimensionField(c.key) ? dims.includes(c.key) : !c.cost || canViewCost));
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
             <colgroup>
               <col style={{ width: 42 }} />
               <col style={{ width: 130 }} />
               <col />
               <col style={{ width: 58 }} />
               <col style={{ width: 220 }} />
               <col style={{ width: 82 }} />
               <col style={{ width: 104 }} />
               <col style={{ width: 122 }} />
               {canViewCost && <col style={{ width: 112 }} />}
               {canViewCost && <col style={{ width: 122 }} />}
               {canViewCost && <col style={{ width: 122 }} />}
               <col style={{ width: 42 }} />
             </colgroup>
             <thead>
               <tr>
                 <th className="row-no">#</th>
                 <th>Mã hàng</th>
                 <th>Tên hàng</th>
                 <th>ĐVT</th>
                 <th>D × R × Dày × Tấm</th>
                 <th>SL</th>
                 <th>Đơn giá bán</th>
                 <th>Thành tiền bán</th>
                 {canViewCost && <th>Đơn giá nhập</th>}
                 {canViewCost && <th>Thành tiền nhập</th>}
                 {canViewCost && <th>Lợi nhuận</th>}
                 <th></th>
               </tr>
             </thead>
   ```
   After:
   ```tsx
             <colgroup>
               {columns.map((c) => (
                 <col key={c.key} style={c.width ? { width: c.width } : undefined} />
               ))}
             </colgroup>
             <thead>
               <tr>
                 {columns.map((c) => (
                   <th key={c.key} className={c.key === 'row-no' ? 'row-no' : undefined}>
                     {c.header}
                   </th>
                 ))}
               </tr>
             </thead>
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                 const lengthDisabled = isLineFocusFieldDisabled(line, 'length');
                 const widthDisabled = isLineFocusFieldDisabled(line, 'width');
                 const thicknessDisabled = isLineFocusFieldDisabled(line, 'thickness');
                 const sheetCountDisabled = isLineFocusFieldDisabled(line, 'sheet-count');
                 return (
   ```
   After:
   ```tsx
                 return (
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx` (`:357-416`):
   ```tsx
                     <td>
                       <div className="dxr-cell">
                         <input
                           id={getLineCellId('length', idx)}
                           className="cell-input"
                           type="number"
                           step="any"
                           placeholder="D"
                           aria-label="Dài"
                           disabled={lengthDisabled}
                           value={numInput(line.length)}
                           onChange={(e) => {
                             const v = parseNum(e.target.value);
                             setLineField(idx, 'length', v as never);
                           }}
                         />
                         <input
                           id={getLineCellId('width', idx)}
                           className="cell-input"
                           type="number"
                           step="any"
                           placeholder="R"
                           aria-label="Rộng"
                           disabled={widthDisabled}
                           value={numInput(line.width)}
                           onChange={(e) => {
                             const v = parseNum(e.target.value);
                             setLineField(idx, 'width', v as never);
                           }}
                         />
                         <input
                           id={getLineCellId('thickness', idx)}
                           className="cell-input"
                           type="number"
                           step="any"
                           placeholder="Dày"
                           aria-label="Dày"
                           disabled={thicknessDisabled}
                           value={numInput(line.thickness)}
                           onChange={(e) => {
                             const v = parseNum(e.target.value);
                             setLineField(idx, 'thickness', v as never);
                           }}
                         />
                         <input
                           id={getLineCellId('sheet-count', idx)}
                           className="cell-input"
                           type="number"
                           step="any"
                           placeholder="Tấm"
                           aria-label="Số tấm"
                           disabled={sheetCountDisabled}
                           value={numInput(line.sheetCount)}
                           onChange={(e) => {
                             const v = parseNum(e.target.value);
                             setLineField(idx, 'sheetCount', v as never);
                           }}
                         />
                       </div>
                     </td>
   ```
   After:
   ```tsx
                     {dims.map((f) => {
                       const { name, label } = DIMENSION_INPUTS[f];
                       return (
                         <td key={f} className="cell-number">
                           <input
                             id={getLineCellId(f, idx)}
                             className="cell-input cell-number tabular-nums"
                             type="number"
                             step="any"
                             aria-label={label}
                             disabled={isLineFocusFieldDisabled(line, f)}
                             value={numInput(line[name])}
                             onChange={(e) => setLineField(idx, name, parseNum(e.target.value) as never)}
                           />
                         </td>
                       );
                     })}
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                   <td colSpan={canViewCost ? 12 : 9} className="empty-placeholder">
   ```
   After:
   ```tsx
                   <td colSpan={columns.length} className="empty-placeholder">
   ```

   The right alignment and tabular digits of the dimension inputs now come from `cell-number` (`line-items-grid.css:108-112`) and the `tabular-nums` utility, so remove the three `.dxr-cell` rules (`line-items-grid.css:154-168`; `grep -rn dxr-cell src` finds no other use once the JSX above is gone).

   Before, in `src/pages/quotations/components/line-items-grid.css`:
   ```css
   .accounting-grid .dxr-cell {
     display: grid;
     grid-template-columns: repeat(4, 1fr);
     height: 33px;
   }

   .accounting-grid .dxr-cell .cell-input {
     border-right: 1px solid hsl(var(--border));
     text-align: right;
     font-variant-numeric: tabular-nums;
   }

   .accounting-grid .dxr-cell .cell-input:last-child {
     border-right: 0;
   }

   .accounting-grid .cell-action {
   ```
   After:
   ```css
   .accounting-grid .cell-action {
   ```
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: PASS, 13 tests. Then `grep -rn "dxr-cell" src` prints nothing.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.css frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "feat(quotations): split the dimensions into four columns from one column list"`

### Task 3.3 — Shown columns follow the highest pricing mode; empty row span and table min-width

1. **Write the failing tests** in `line-items-grid.test.tsx`.

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   import { LineItemsGrid } from './line-items-grid';
   ```
   After:
   ```tsx
   import { createEmptyLine, LineItemsGrid } from './line-items-grid';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   import { useAuthStore } from '@/stores/auth-store';
   ```
   After:
   ```tsx
   import type { ProductSuggestion } from '@/features/products/types';
   import { useAuthStore } from '@/stores/auth-store';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   const m3 = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
   ```
   After:
   ```tsx
   const md = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
     line(uiKey, { pricingMode: 'PerLinearMeter', unitName: 'm', sheetCount: 4, length: 6000, quantity: 0, ...overrides });
   // Its thickness is stored (from an earlier product) but unused.
   const m2 = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
     line(uiKey, {
       pricingMode: 'PerSquareMeter',
       unitName: 'm²',
       sheetCount: 3,
       length: 2400,
       width: 1200,
       thickness: 5,
       quantity: 0,
       ...overrides,
     });
   const m3 = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   function setCostPermission(enabled: boolean) {
   ```
   After:
   ```tsx
   const sheetProduct: ProductSuggestion = {
     id: '33333333-3333-3333-3333-333333333333',
     code: 'TON01',
     name: 'Tôn lạnh',
     unitName: 'm²',
     pricingMode: 'PerSquareMeter',
     defaultPrice: 120_000,
     costPrice: 90_000,
     defaultTaxRate: 8,
     length: 2000,
     width: 1200,
     thickness: undefined,
     trackInventory: true,
     purchaseDiscountRate: 0,
     salesDiscountRate: 0,
     priceIncludesVat: false,
   };

   function setCostPermission(enabled: boolean) {
   ```

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   describe('LineItemsGrid', () => {
   ```
   After:
   ```tsx
   // Picks a product in a row's code cell, as the real typeahead's commit() does.
   function pick(rowIndex: number, product: ProductSuggestion) {
     const onSelect = cellProps.byId[`line-product-code-${rowIndex}`]?.onSelect as
       | ((s: ProductSuggestion) => void)
       | undefined;
     if (!onSelect) throw new Error(`no product cell in row ${rowIndex}`);
     act(() => onSelect(product));
   }

   const DIMENSION_HEADERS = ['Số tấm', 'Dài', 'Rộng', 'Dày'];
   const shownDimensionHeaders = () => headers().filter((h) => DIMENSION_HEADERS.includes(h ?? ''));

   describe('LineItemsGrid', () => {
   ```

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     describe('dimension columns follow the highest pricing mode', () => {
       it('shows none for a blank line or unit lines, also while a code is typed', () => {
         renderGrid([createEmptyLine(0), line('b')]);
         // The exact row: main has only the one combined dimension header, none of the four.
         expect(headers()).toEqual(['#', 'Mã hàng', 'Tên hàng', 'ĐVT', 'SL', 'Đơn giá bán', 'Thành tiền bán', '']);
         for (const id of ['line-sheet-count-0', 'line-length-0', 'line-width-0', 'line-thickness-0']) {
           expect(document.getElementById(id)).toBeNull();
         }

         fireEvent.change(byId('line-product-code-1'), { target: { value: 'TON' } });
         expect(shownDimensionHeaders()).toEqual([]);
       });

       it('shows Số tấm, Dài and Rộng for an md line next to an m² line', () => {
         renderGrid([md('a'), m2('b')]);
         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
       });

       it('spans the empty row over the shown columns', () => {
         renderGrid([]);
         expect(screen.getByText(/Chưa có dòng nào/).closest('td')).toHaveAttribute('colspan', '8');
       });

       it('spans the empty row over the cost columns too with quotations.view_cost', () => {
         setCostPermission(true);
         renderGrid([]);
         expect(screen.getByText(/Chưa có dòng nào/).closest('td')).toHaveAttribute('colspan', '11');
       });

       it.each([
         ['unit lines', false, [line('a')], '780px'],
         ['unit lines with view_cost', true, [line('a')], '1136px'],
         ['an m³ line', false, [m3('a')], '1066px'],
         ['an m³ line with view_cost', true, [m3('a')], '1422px'],
       ])('sizes the table to its columns plus 200px for Tên hàng: %s', (_name, cost, lines, minWidth) => {
         setCostPermission(cost);
         const { container } = renderGrid(lines);
         expect((container.querySelector('table.accounting-grid') as HTMLTableElement).style.minWidth).toBe(minWidth);
       });

       it('shows the m² columns at once when a unit-only quotation picks an m² product, and Enter walks them', () => {
         renderGrid([line('a')]);
         expect(shownDimensionHeaders()).toEqual([]);

         pick(0, sheetProduct);

         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
         enter('line-name-0');
         expect(focused()).toBe('line-unit-0');
         enter('line-unit-0');
         expect(focused()).toBe('line-sheet-count-0');
         enter('line-sheet-count-0');
         expect(focused()).toBe('line-length-0');
         enter('line-length-0');
         expect(focused()).toBe('line-width-0');
         enter('line-width-0');
         expect(focused()).toBe('line-unit-price-0');
       });

       it('hides Dày at once when the only m³ line is re-picked as an m² product, and keeps its value', () => {
         const { form } = renderGrid([m3('a')]);
         fireEvent.change(byId('line-thickness-0'), { target: { value: '8' } });
         fireEvent.change(byId('line-product-code-0'), { target: { value: 'TON' } });
         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng', 'Dày']);

         pick(0, sheetProduct);

         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
         expect(document.getElementById('line-thickness-0')).toBeNull();
         expect(form().getValues('lines.0.thickness')).toBe(8);
       });
     });
   ```
   The widths: 42 + 130 + 200 (**Tên hàng**) + 58 + 82 + 104 + 122 + 42 = 780; the cost columns add 112 + 122 + 122 = 356 (1136); the four dimensions add 70 + 76 + 76 + 64 = 286 (1066 and 1422).
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 10 FAIL, 13 pass. Why: Task 3.2 always renders the four dimension columns and sets no inline `min-width`.
   - "shows none …": at the exact header row, `expected [ '#', 'Mã hàng', 'Tên hàng', …(9) ] to deeply equal [ '#', 'Mã hàng', 'Tên hàng', …(5) ]` (the four dimension headers are there). On `qlbanhang/main` it fails at the same line with `…(6)`, because of the combined **D × R × Dày × Tấm** header; a check for missing **Số tấm**/**Dài**/**Rộng**/**Dày** headers alone would pass there (SUMMARY → Testing).
   - "shows Số tấm, Dài and Rộng …": `expected [ 'Số tấm', 'Dài', 'Rộng', 'Dày' ] to deeply equal [ 'Số tấm', 'Dài', 'Rộng' ]` (on `qlbanhang/main` it is `[]`, since the base has the combined header only).
   - the two `colspan` tests: `toHaveAttribute("colspan", "8")` receives `12`, and `"11"` receives `15` (on `qlbanhang/main` 9 and 12).
   - the four `min-width` cases: `expected '' to be '780px'` (and `'1136px'`, `'1066px'`, `'1422px'`).
   - the m² pick: `expected [ 'Số tấm', 'Dài', 'Rộng', 'Dày' ] to deeply equal []` before the pick.
   - the m³ re-pick: `expected [ 'Số tấm', 'Dài', 'Rộng', 'Dày' ] to deeply equal [ 'Số tấm', 'Dài', 'Rộng' ]` after the pick.
3. **Write the minimal implementation.**

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
   import { DIMENSION_FIELDS, isDimensionField, type DimensionField } from '@/lib/dimension-columns';
   ```
   After:
   ```tsx
   import { isDimensionField, shownDimensions, type DimensionField } from '@/lib/dimension-columns';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
   const DIMENSION_INPUTS: Record<DimensionField, { name: 'sheetCount' | 'length' | 'width' | 'thickness'; label: string }> = {
   ```
   After:
   ```tsx
   // Tên hàng has no fixed width; the table's min-width keeps this much for it.
   const NAME_MIN_WIDTH = 200;

   const DIMENSION_INPUTS: Record<DimensionField, { name: 'sheetCount' | 'length' | 'width' | 'thickness'; label: string }> = {
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     const dims: readonly DimensionField[] = DIMENSION_FIELDS;
     const columns = COLUMNS.filter((c) => (isDimensionField(c.key) ? dims.includes(c.key) : !c.cost || canViewCost));
   ```
   After:
   ```tsx
     // Số tấm, Dài, Rộng, Dày follow the highest pricing mode of the lines: one pass over the rows on every render.
     const dims = shownDimensions(rows);
     const columns = COLUMNS.filter((c) => (isDimensionField(c.key) ? dims.includes(c.key) : !c.cost || canViewCost));
     const minWidth = columns.reduce((sum, c) => sum + (c.width ?? NAME_MIN_WIDTH), 0);
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
           <table className="accounting-grid">
   ```
   After:
   ```tsx
           <table className="accounting-grid" style={{ minWidth }}>
   ```
   The inline `minWidth` overrides `.accounting-grid { min-width: 1360px }` (`line-items-grid.css:16-24`, the `min-width` at `:18`), which stays for the other grids.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: PASS, 23 tests.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "feat(quotations): show the dimension columns the highest pricing mode needs"`

### Task 3.4 — A cell the row does not use shows "—"

1. **Write the failing test.**

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     it('shows "—" in a cell the row does not use, keeps its id and keeps the stored value', () => {
       const { form } = renderGrid([
         m3('a'),
         m2('b'),
         line('c', { sheetCount: 1, length: 2000, width: 1000, thickness: 3 }),
       ]);

       for (const id of ['line-thickness-1', 'line-sheet-count-2', 'line-length-2', 'line-width-2', 'line-thickness-2']) {
         expect(byId(id)).toBeDisabled();
         expect(byId(id)).toHaveValue(null);
         expect(byId(id)).toHaveAttribute('placeholder', '—');
       }
       expect(byId('line-thickness-0')).toHaveValue(50);
       expect(byId('line-thickness-0')).not.toHaveAttribute('placeholder');
       expect(byId('line-width-1')).toHaveValue(1200);
       expect(form().getValues('lines.1.thickness')).toBe(5);
       expect(form().getValues('lines.2')).toMatchObject({ sheetCount: 1, length: 2000, width: 1000, thickness: 3 });
     });
   ```
2. **Run it and check it fails:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 1 FAIL at `expect(byId('line-thickness-1')).toHaveValue(null)`, received `5`. Why: the disabled cell still renders the stored value (`value={numInput(line[name])}`) and has no placeholder.
3. **Write the minimal implementation.** The value is a plain `value` prop, so rendering "—" writes nothing back to the form and the payload still sends the stored dimensions (decision 4).

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                     {dims.map((f) => {
                       const { name, label } = DIMENSION_INPUTS[f];
                       return (
                         <td key={f} className="cell-number">
                           <input
                             id={getLineCellId(f, idx)}
                             className="cell-input cell-number tabular-nums"
                             type="number"
                             step="any"
                             aria-label={label}
                             disabled={isLineFocusFieldDisabled(line, f)}
                             value={numInput(line[name])}
   ```
   After:
   ```tsx
                     {dims.map((f) => {
                       const { name, label } = DIMENSION_INPUTS[f];
                       // A cell the row's mode does not use shows "—"; its stored value stays in the form, so it is
                       // still saved and printed.
                       const unused = isLineFocusFieldDisabled(line, f);
                       return (
                         <td key={f} className="cell-number">
                           <input
                             id={getLineCellId(f, idx)}
                             className="cell-input cell-number tabular-nums"
                             type="number"
                             step="any"
                             aria-label={label}
                             disabled={unused}
                             placeholder={unused ? '—' : undefined}
                             value={unused ? '' : numInput(line[name])}
   ```
   The quotation has no read-only mode (section 01 corrections), so `unused` and `disabled` are the same here.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: PASS, 24 tests.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "feat(quotations): show a dash in a dimension cell the line does not use"`

### Task 3.5 — A missing or unknown pricing mode counts as `PerUnit`

1. **Write the failing test.**

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     // An old draft or corrupt data: a missing or unknown mode counts as PerUnit (cells and Enter path).
     it.each([
       ['without a pricing mode', undefined],
       ['with an unknown pricing mode', 'Legacy'],
     ])('treats a line %s as a unit line', (_name, mode) => {
       renderGrid([m3('a'), { ...line('b', { sheetCount: 2, length: 3000 }), pricingMode: mode } as QuotationLineFormValues]);

       for (const f of ['sheet-count', 'length', 'width', 'thickness']) {
         expect(byId(`line-${f}-1`)).toBeDisabled();
         expect(byId(`line-${f}-1`)).toHaveAttribute('placeholder', '—');
       }
       expect(byId('line-quantity-1')).toBeEnabled();
       enter('line-unit-1');
       expect(focused()).toBe('line-quantity-1');
     });
   ```
2. **Run it and check it fails:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 2 FAIL at `expect(byId('line-sheet-count-1')).toBeDisabled()` ("Received element is not disabled"). Why: the `default:` branch of `isLineFocusFieldDisabled` (`line-items-grid.tsx:83-84` at `e61e4f1b`; about :126 after Task 3.4) enables every field for a mode it does not know, so the dimension cells are open and Enter from `line-unit-1` goes to `line-sheet-count-1` (on `qlbanhang/main`, to `line-length-1`).
3. **Write the minimal implementation.**

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
   import { isDimensionField, shownDimensions, type DimensionField } from '@/lib/dimension-columns';
   ```
   After:
   ```tsx
   import {
     isDimensionField,
     normalizePricingMode,
     shownDimensions,
     usesDimension,
     type DimensionField,
   } from '@/lib/dimension-columns';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
   function isLineFocusFieldDisabled(line: QuotationLineFormValues, field: LineFocusField): boolean {
     switch (line.pricingMode) {
       case 'PerUnit':
         return field === 'length' || field === 'width' || field === 'thickness' || field === 'sheet-count';
       case 'PerLinearMeter':
         return field === 'width' || field === 'thickness' || field === 'quantity';
       case 'PerSquareMeter':
         return field === 'thickness' || field === 'quantity';
       case 'PerCubicMeter':
         return field === 'quantity';
       default:
         return false;
     }
   }
   ```
   After:
   ```tsx
   // A line's mode opens the dimensions it uses, and SL only on a unit line. A missing or unknown mode (an old draft)
   // counts as PerUnit.
   function isLineFocusFieldDisabled(line: QuotationLineFormValues, field: LineFocusField): boolean {
     if (isDimensionField(field)) return !usesDimension(line.pricingMode, field);
     if (field === 'quantity') return normalizePricingMode(line.pricingMode) !== 'PerUnit';
     return false;
   }
   ```
   For the four valid modes this returns exactly what the old `switch` returned. The **SL** value of such a line is unchanged: `computeLineQuantity` falls back to the typed quantity (`lib/pricing-quantity.ts`, `default:` branch). Saving it is still refused by the zod enum (`features/quotations/schema.ts:20`).
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: PASS, 26 tests.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "fix(quotations): treat a line without a known pricing mode as a unit line"`

### Task 3.6 — Enter skips cells that are not rendered (`quotations.view_cost`)

1. **Write the failing tests.**

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     describe('Enter without quotations.view_cost (no Đơn giá nhập cell)', () => {
       it("goes from Đơn giá bán to the next row's Mã hàng", () => {
         renderGrid([line('a'), line('b')]);
         enter('line-unit-price-0');
         expect(focused()).toBe('line-product-code-1');
       });

       it("adds a row from the last row's Đơn giá bán and focuses its Mã hàng", async () => {
         renderGrid([line('a'), line('b')]);
         enter('line-unit-price-1');
         await waitFor(() => expect(focused()).toBe('line-product-code-2'));
       });

       it("goes back from Mã hàng to the previous row's Đơn giá bán with Shift+Enter", () => {
         renderGrid([line('a'), line('b')]);
         enter('line-product-code-1', true);
         expect(focused()).toBe('line-unit-price-0');
       });
     });

     it("goes from Đơn giá nhập to the next row's Mã hàng with quotations.view_cost", () => {
       setCostPermission(true);
       renderGrid([line('a'), line('b')]);
       enter('line-unit-cost-0');
       expect(focused()).toBe('line-product-code-1');
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 3 FAIL: `expected 'line-unit-price-0' to be 'line-product-code-1'`, `expected 'line-unit-price-1' to be 'line-product-code-2'` (after the 1000 ms `waitFor`), `expected 'line-product-code-1' to be 'line-unit-price-0'`. Why: `getEnabledFocusFields` (`:139-143` at `e61e4f1b`; about :185 after Task 3.5) lists `unit-cost`, which is rendered only under `canViewCost` (`:469-487` at `e61e4f1b`; the unit-cost `{canViewCost && (` is about :456 after Task 3.5), so `focusLineCell('unit-cost', …)` finds no element and the caret stays. The `view_cost` test passes on the base; it guards the path that already worked.
3. **Write the minimal implementation.** The filter also drops dimension columns that are not shown. With the nesting md ⊂ m² ⊂ m³ a row's own open dimensions are always shown, so no test can make that half fail; it keeps the path true to the rendered cells.

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     function getEnabledFocusFields(rowIndex: number): readonly LineFocusField[] {
       const line = getLineForNavigation(rowIndex);
       if (!line) return LINE_FOCUS_FIELDS;
       return LINE_FOCUS_FIELDS.filter((field) => !isLineFocusFieldDisabled(line, field));
     }
   ```
   After:
   ```tsx
     // Đơn giá nhập without quotations.view_cost and a hidden dimension column have no cell to focus.
     const isRendered = (field: LineFocusField) =>
       (field !== 'unit-cost' || canViewCost) && (!isDimensionField(field) || dims.includes(field));

     function getEnabledFocusFields(rowIndex: number): readonly LineFocusField[] {
       const rendered = LINE_FOCUS_FIELDS.filter(isRendered);
       const line = getLineForNavigation(rowIndex);
       if (!line) return rendered;
       return rendered.filter((field) => !isLineFocusFieldDisabled(line, field));
     }
   ```
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: PASS, 30 tests.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "fix(quotations): Enter skips cost and dimension cells that are not shown"`

### Task 3.7 — Mã hàng keeps its list closed on focus (decision 11), with a real-typeahead test

After Task 3.6 every user can Enter into the next row's filled **Mã hàng**. With `openOnFocus` on (the component default, `product-typeahead-cell.tsx:64`, `:281`) its list opens there, and the next Enter commits the highlighted result: **SL** 0, **Tên hàng** and the prices from the catalog (`line-items-grid.tsx:298-308`).

1. **Write the failing tests.**

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     it('keeps the suggestion list of a filled Mã hàng closed when the cell takes the focus', () => {
       renderGrid([line('a'), line('b')]);
       expect(cellProps.byId['line-product-code-0']?.openOnFocus).toBe(false);
       expect(cellProps.byId['line-product-code-1']?.openOnFocus).toBe(false);
     });
   ```

   The grid test file mocks the typeahead for the whole file, so it cannot see the list. A new file uses the real `ProductTypeaheadCell`. It mocks only `@/features/products/hooks`, as `product-typeahead-cell.test.tsx:74-80` does, plus the catalog and quick add dialogs; the auth store is the real one, set as in `line-items-grid.test.tsx`.

   New file `src/pages/quotations/components/line-items-grid.typeahead.test.tsx`:
   ```tsx
   import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
   import { useForm, type UseFormReturn } from 'react-hook-form';
   import { LineItemsGrid } from './line-items-grid';
   import type {
     QuotationFormParsed,
     QuotationFormValues,
     QuotationLineFormValues,
   } from '@/features/quotations/schema';
   import type { ProductSuggestion } from '@/features/products/types';
   import { useAuthStore } from '@/stores/auth-store';

   // The real typeahead, unlike line-items-grid.test.tsx: when the grid moves the caret into a filled Mã hàng, its list
   // must stay closed, so the next Enter moves on instead of re-picking the product.

   // Every search finds this product. A re-pick puts its name, SL 0 and its price on the row.
   const catalogP1: ProductSuggestion = {
     id: '11111111-1111-1111-1111-111111111111',
     code: 'P1',
     name: 'Catalog P1',
     unitName: 'pcs',
     pricingMode: 'PerUnit',
     defaultPrice: 999,
     costPrice: 500,
     defaultTaxRate: 0,
     length: undefined,
     width: undefined,
     thickness: undefined,
     trackInventory: true,
     purchaseDiscountRate: 0,
     salesDiscountRate: 0,
     priceIncludesVat: false,
   };

   vi.mock('@/features/products/hooks', () => ({
     useProductSearch: () => ({
       data: [catalogP1],
       isLoading: false,
       isError: false,
       isFetching: false,
       settledQuery: undefined,
     }),
     useProducts: () => ({ data: { items: [], totalPages: 1, totalItems: 0 }, isLoading: false }),
     useProductGroups: () => ({ data: [] }),
     useProduct: () => ({ data: undefined, isLoading: false }),
   }));
   vi.mock('@/features/products/components/product-catalog-dialog', () => ({ ProductCatalogDialog: () => null }));
   vi.mock('@/features/products/components/product-quick-add-dialog', () => ({ ProductQuickAddDialog: () => null }));

   // Rows 'a', 'b', 'c' (indices 0-2). Row 'b' has the code P1, the same as catalogP1.
   const lines: QuotationLineFormValues[] = [0, 1, 2].map((i) => ({
     _uiKey: ['a', 'b', 'c'][i],
     sortOrder: i,
     productCode: `P${i}`,
     productName: `Name${i}`,
     unitName: 'pcs',
     pricingMode: 'PerUnit',
     quantity: 5,
     unitPrice: 123,
   }));

   function setCostPermission(enabled: boolean) {
     useAuthStore.setState({
       accessToken: 'token',
       expiresAt: '2099-01-01T00:00:00Z',
       user: {
         id: '11111111-1111-1111-1111-111111111111',
         username: 'tester',
         email: 'tester@example.com',
         fullName: 'Tester',
         roles: [],
         permissions: enabled ? ['quotations.view_cost'] : [],
       },
     });
   }

   function renderGrid() {
     const formRef: { current: UseFormReturn<QuotationFormValues, unknown, QuotationFormParsed> | null } = {
       current: null,
     };
     function Harness() {
       const form = useForm<QuotationFormValues, unknown, QuotationFormParsed>({
         defaultValues: {
           customerId: '11111111-1111-1111-1111-111111111111',
           customerName: '',
           quotationDate: '2026-05-13',
           deliveryAddress: '',
           deliveryRecipient: '',
           deliveryPhone: '',
           deliveryDate: '',
           deliveryNote: '',
           taxRate: 0,
           discount: 0,
           freight: 0,
           internalNote: '',
           lines,
         },
       });
       formRef.current = form;
       return <LineItemsGrid form={form} />;
     }
     render(<Harness />);
     return { form: () => formRef.current! };
   }

   function byId(id: string): HTMLElement {
     const el = document.getElementById(id);
     if (!el) throw new Error(`#${id} not found`);
     return el;
   }

   describe('LineItemsGrid with the real product typeahead', () => {
     beforeEach(() => {
       setCostPermission(false);
     });

     it.each([
       ['without quotations.view_cost', false, 'line-unit-price-0'],
       ['with quotations.view_cost', true, 'line-unit-cost-0'],
     ])("Enter from row 0's last cell into row 1's Mã hàng leaves its list closed and keeps row 1 (%s)", async (_name, cost, lastCell) => {
       setCostPermission(cost);
       const { form } = renderGrid();

       act(() => byId(lastCell).focus());
       fireEvent.keyDown(byId(lastCell), { key: 'Enter' });
       expect(document.activeElement).toBe(byId('line-product-code-1'));
       expect(screen.queryByRole('listbox')).toBeNull();

       fireEvent.keyDown(document.activeElement!, { key: 'Enter' });

       await waitFor(() => expect(document.activeElement).toBe(byId('line-name-1')));
       expect(form().getValues('lines.1')).toMatchObject({ productCode: 'P1', productName: 'Name1', quantity: 5, unitPrice: 123 });
     });
   });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx src/pages/quotations/components/line-items-grid.typeahead.test.tsx`. Expected:
   - grid file: 1 FAIL, `expected undefined to be false`: the grid passes no `openOnFocus` (`:291-309`).
   - typeahead file: 2 FAIL at the first `expect(screen.queryByRole('listbox')).toBeNull()`, with `expected <table …(3)>…(2)</table> to be null` (the `qldh-lookup-table` with `role="listbox"`): the focus opened the list. Were that line removed, the test would fail at `toMatchObject` instead, because the Enter re-picks `catalogP1` (row 1 becomes `Catalog P1`, `quantity: 0`, `unitPrice: 999`; checked by the brainstorm's round 2 prototype and again while writing this plan). On `qlbanhang/main` the case without `view_cost` fails earlier, because Enter is stuck in **Đơn giá bán**.
3. **Write the minimal implementation.**

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                         nextFocusId={getLineCellId('name', idx)}
                         allowQuickAdd
   ```
   After:
   ```tsx
                         nextFocusId={getLineCellId('name', idx)}
                         // Closed until the user types: Enter, Shift+Enter or a row delete into a filled cell must
                         // not re-pick its product with the next Enter (decision 11).
                         openOnFocus={false}
                         allowQuickAdd
   ```
   Typing still opens the list (`product-typeahead-cell.tsx:288-293`), and Enter on a closed list falls through to the grid (`:230-237`), which moves to **Tên hàng**. The component default stays `true`. Since PR #31 (`438b0db4`), every other call site passes `openOnFocus={false}` (`opening-stock-page.tsx:252`, `stock-card-page.tsx:36` and `:158`, `recalc-cost-page.tsx:152`, `stock-line-grid.tsx:571`), so after this task no screen uses the default. Update the two comments that no longer hold:

   Before, in `src/pages/quotations/components/product-typeahead-cell.tsx`:
   ```tsx
     /** Mở danh sách gợi ý khi ô nhận focus. Mặc định true (báo giá giữ nguyên). */
   ```
   After:
   ```tsx
     /** Mở danh sách gợi ý khi ô nhận focus. Mặc định true; mọi màn hình đang dùng ô này đều truyền false, danh sách chỉ mở khi gõ. */
   ```

   Before, in `src/pages/quotations/components/product-typeahead-cell.test.tsx`:
   ```tsx
     // Quotations rely on this default; the stock grid opts out.
   ```
   After:
   ```tsx
     // The component default. Every screen that uses the cell passes openOnFocus={false}; this test keeps the default's contract.
   ```
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components`. Expected: PASS for `line-items-grid.test.tsx` (31), `line-items-grid.typeahead.test.tsx` (2), `product-typeahead-cell.test.tsx` and `quotation-date-filter.test.tsx`, unchanged.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx frontend/src/pages/quotations/components/line-items-grid.typeahead.test.tsx frontend/src/pages/quotations/components/product-typeahead-cell.tsx frontend/src/pages/quotations/components/product-typeahead-cell.test.tsx && git commit -m "fix(quotations): keep the product list closed when the code cell takes the focus"`

### Task 3.8 — A row delete moves the focus to the row that takes its place

One helper `removeLine(rowIndex, field)` serves Ctrl+Delete and the trash button, as on the stock grid (`stock-line-grid.tsx:366-371`, `:717-720`). It reads the rows from `form.getValues('lines')` after `useWatch`, as the stock grid does (`stock-line-grid.tsx:342-348`): read from `useWatch` (`line-items-grid.tsx:101-102`), the rows are one render behind `remove()`, so the moved-up row is keyed with the deleted line for one render, remounts, and drops the focus. Without that change five of the new tests below still fail.

1. **Write the failing tests.**

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   // Picks a product in a row's code cell, as the real typeahead's commit() does.
   ```
   After:
   ```tsx
   // Presses Ctrl+Delete in a cell, focusing it first.
   function ctrlDelete(id: string) {
     const el = byId(id);
     act(() => el.focus());
     fireEvent.keyDown(el, { key: 'Delete', ctrlKey: true });
   }

   // Picks a product in a row's code cell, as the real typeahead's commit() does.
   ```

   Extend the existing test `removes active row with Ctrl+Delete` (`line-items-grid.test.tsx:205-216` at `e61e4f1b`; find it by its name, since Tasks 3.1–3.3 move it to about :317).

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
       await waitFor(() => {
         expect(screen.getByDisplayValue('Alpha')).toBeInTheDocument();
         expect(screen.queryByDisplayValue('Beta')).not.toBeInTheDocument();
       });
     });
   ```
   After:
   ```tsx
       await waitFor(() => {
         expect(screen.getByDisplayValue('Alpha')).toBeInTheDocument();
         expect(screen.queryByDisplayValue('Beta')).not.toBeInTheDocument();
       });
       // The last row went: the row above takes the focus, in the same cell.
       expect(document.activeElement).toBe(screen.getByDisplayValue('Alpha'));
     });
   ```

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     describe('a row delete moves the focus to the row that takes its place', () => {
       it('keeps the cell when the row below uses it; that row moves up with the focus', () => {
         renderGrid([line('a'), line('b'), line('c')]);
         ctrlDelete('line-name-1');
         expect(focused()).toBe('line-name-1');
         expect(byId('line-name-1')).toHaveValue('Name c');
         expect(screen.queryByDisplayValue('Name b')).toBeNull();
       });

       it("goes to the row's Mã hàng from SL when that row is an m² line, which does not use SL", () => {
         renderGrid([line('a'), m2('b')]);
         ctrlDelete('line-quantity-0');
         expect(focused()).toBe('line-product-code-0');
         expect(byId('line-product-code-0')).toHaveValue('P-b');
       });

       it("goes to the m² row's Mã hàng from Dày of the only m³ row, then hides Dày", () => {
         renderGrid([m3('a'), m2('b')]);
         ctrlDelete('line-thickness-0');
         expect(focused()).toBe('line-product-code-0');
         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
       });

       it('goes to the row above when the bottom row goes', () => {
         renderGrid([line('a'), line('b'), line('c')]);
         ctrlDelete('line-name-2');
         expect(focused()).toBe('line-name-1');
         expect(byId('line-name-1')).toHaveValue('Name b');
       });

       it('goes to Thêm dòng when the only row goes', () => {
         renderGrid([line('a')]);
         ctrlDelete('line-name-0');
         expect(screen.getByText(/Chưa có dòng nào/)).toBeInTheDocument();
         expect(focused()).toBe('line-add');
         expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Thêm dòng' }));
       });

       it("goes to the next row's Mã hàng from a click on the trash button", async () => {
         const user = userEvent.setup();
         renderGrid([line('a'), line('b')]);
         await user.click(screen.getAllByRole('button', { name: 'Xóa dòng' })[0]);
         expect(screen.queryByDisplayValue('Name a')).toBeNull();
         expect(focused()).toBe('line-product-code-0');
         expect(byId('line-product-code-0')).toHaveValue('P-b');
       });

       it("goes to the next row's Mã hàng from Ctrl+Delete on the focused trash button", () => {
         renderGrid([line('a'), line('b')]);
         const trash = screen.getAllByRole('button', { name: 'Xóa dòng' })[0];
         act(() => trash.focus());
         fireEvent.keyDown(trash, { key: 'Delete', ctrlKey: true });
         expect(screen.queryByDisplayValue('Name a')).toBeNull();
         expect(focused()).toBe('line-product-code-0');
       });

       // React passes a key from a portal (the catalog dialog opened from a cell) to the grid; it is no row.
       it('deletes nothing from Ctrl+Delete outside a row', () => {
         const { container } = renderGrid([line('a'), line('b')]);
         act(() => byId('line-name-1').focus());
         fireEvent.keyDown(container.querySelector('.accounting-grid-wrap')!, { key: 'Delete', ctrlKey: true });
         expect(screen.getByDisplayValue('Name b')).toBeInTheDocument();
       });
     });
   ```

   The two real-typeahead delete scenarios of SUMMARY → Testing go in the new file: (a) a click on row 0's trash button, and (b) Ctrl+Delete in `line-product-code-0`. Each ends in the next row's filled **Mã hàng** with its list closed, and the next Enter moves on to **Tên hàng** without a re-pick.

   Before, in `src/pages/quotations/components/line-items-grid.typeahead.test.tsx`:
   ```tsx
   import { useForm, type UseFormReturn } from 'react-hook-form';
   ```
   After:
   ```tsx
   import userEvent from '@testing-library/user-event';
   import { useForm, type UseFormReturn } from 'react-hook-form';
   ```

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.typeahead.test.tsx`, before its last line `});`:
   ```tsx
     it("Ctrl+Delete in row 0's Mã hàng moves the caret into the next Mã hàng with its list closed, and Enter keeps the row", async () => {
       const { form } = renderGrid();

       act(() => byId('line-product-code-0').focus());
       expect(screen.queryByRole('listbox')).toBeNull();
       fireEvent.keyDown(byId('line-product-code-0'), { key: 'Delete', ctrlKey: true });

       expect(form().getValues('lines.0._uiKey')).toBe('b');
       expect(document.activeElement).toBe(byId('line-product-code-0'));
       expect(screen.queryByRole('listbox')).toBeNull();

       fireEvent.keyDown(document.activeElement!, { key: 'Enter' });

       await waitFor(() => expect(document.activeElement).toBe(byId('line-name-0')));
       expect(form().getValues('lines.0')).toMatchObject({ productCode: 'P1', productName: 'Name1', quantity: 5, unitPrice: 123 });
     });

     it("a click on row 0's trash button moves the caret into the next Mã hàng with its list closed, and Enter keeps the row", async () => {
       const user = userEvent.setup();
       const { form } = renderGrid();

       await user.click(screen.getAllByRole('button', { name: 'Xóa dòng' })[0]);
       expect(document.activeElement).toBe(byId('line-product-code-0'));
       expect(screen.queryByRole('listbox')).toBeNull();

       fireEvent.keyDown(document.activeElement!, { key: 'Enter' });

       await waitFor(() => expect(document.activeElement).toBe(byId('line-name-0')));
       expect(form().getValues('lines.0')).toMatchObject({ productCode: 'P1', productName: 'Name1', quantity: 5, unitPrice: 123 });
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx src/pages/quotations/components/line-items-grid.typeahead.test.tsx`. Expected: 11 FAIL. Why: Ctrl+Delete and the trash button call `remove()` without moving the focus (`:184-191`, `:506-509`), so the focus falls to `<body>`; Ctrl+Delete takes the row from `activeRowIndex`, which any focus inside a `<tr>` sets (`:288`).
   - the extended test: `expected <body …> to be <textarea …>`;
   - the seven focus tests: `expected '' to be 'line-name-1'` (or `'line-product-code-0'`, `'line-add'`);
   - "deletes nothing …": `Unable to find an element with the display value: Name b` (the focus set `activeRowIndex` to 1, and the key on the wrapper removed that row);
   - typeahead file, both new tests: `expected <body><div>…(1)</div></body> to be <input class="cell-input" …(8)></input>`, at the first `document.activeElement` check. In (b) the row is gone (`lines.0._uiKey` is already `'b'`): `activeRowIndex` removed it, but its focused input went with it. Task 3.7 already keeps the list closed, so after this task the Enter moves on to `line-name-0`.
3. **Write the minimal implementation.**

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     const watched = useWatch({ control: form.control, name: 'lines' }) as QuotationLineFormValues[] | undefined;
     const rows = watched ?? [];
     const wrapRef = useRef<HTMLDivElement>(null);
     const [activeRowIndex, setActiveRowIndex] = useState<number | null>(null);
   ```
   After:
   ```tsx
     // The watch renders the grid on every line change, but the rows are read from the form itself, which always matches
     // the field array. react-hook-form hands remove() to its watchers one render late: read from the watched copy, the
     // rows would key the row that moves up with the deleted line, and that row would remount and drop the focus.
     useWatch({ control: form.control, name: 'lines' });
     const rows = (form.getValues('lines') ?? []) as QuotationLineFormValues[];
     const wrapRef = useRef<HTMLDivElement>(null);
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     function handleGridKeyDown(e: KeyboardEvent<HTMLDivElement>) {
   ```
   After:
   ```tsx
     // Remove a row without a focus gap, as the stock grid does: focus the row that takes its place (the row below, else
     // the row above) BEFORE removing it. Its <tr> stays mounted, so the focus stays there. The same cell when that row
     // renders and enables it (SL on a metre line does not count), otherwise its Mã hàng; with no row left, Thêm dòng.
     function removeLine(rowIndex: number, field: LineFocusField) {
       const survivor = rowIndex + 1 < fields.length ? rowIndex + 1 : rowIndex - 1;
       if (survivor >= 0) {
         focusLineCell(getEnabledFocusFields(survivor).includes(field) ? field : 'product-code', survivor);
       } else {
         document.getElementById('line-add')?.focus();
       }
       remove(rowIndex);
     }

     function handleGridKeyDown(e: KeyboardEvent<HTMLDivElement>) {
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
       if (e.key === 'Delete' && e.ctrlKey) {
         e.preventDefault();
         if (activeRowIndex != null && activeRowIndex < fields.length) {
           remove(activeRowIndex);
           setActiveRowIndex(null);
         }
         return;
       }
   ```
   After:
   ```tsx
       if (e.key === 'Delete' && e.ctrlKey) {
         const target = e.target as HTMLElement;
         const cell = parseLineCellId(target.id);
         // The trash button has no id: its row comes from the <tr>. A key from anywhere else, such as a dialog opened
         // from a cell, deletes nothing.
         const rowAttr = cell ? null : target.closest('tr[data-row-index]')?.getAttribute('data-row-index');
         const rowIndex = cell ? cell.rowIndex : rowAttr != null ? Number(rowAttr) : -1;
         if (rowIndex < 0 || rowIndex >= fields.length) return;
         e.preventDefault();
         removeLine(rowIndex, cell?.field ?? 'product-code');
         return;
       }
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
       for (let i = fields.length - 1; i >= 0; i--) remove(i);
       setActiveRowIndex(null);
   ```
   After:
   ```tsx
       for (let i = fields.length - 1; i >= 0; i--) remove(i);
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                   <tr key={line._uiKey ?? field.id} onFocus={() => setActiveRowIndex(idx)}>
   ```
   After:
   ```tsx
                   <tr key={line._uiKey ?? field.id} data-row-index={idx}>
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                         aria-label="Xóa dòng"
                         onClick={() => {
                           remove(idx);
                           if (activeRowIndex === idx) setActiveRowIndex(null);
                         }}
   ```
   After:
   ```tsx
                         aria-label="Xóa dòng"
                         // The button goes with its row: the focus moves to the row that takes its place.
                         onClick={() => removeLine(idx, 'product-code')}
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
           <button type="button" className="lib-btn" onClick={addLine}>
             <AppIcon name="add" />
             Thêm dòng
   ```
   After:
   ```tsx
           <button type="button" id="line-add" className="lib-btn" onClick={addLine}>
             <AppIcon name="add" />
             Thêm dòng
   ```
   The empty-placeholder row gets no `data-row-index`. Removing the `<tr onFocus>` also stops a Ctrl+Delete in the catalog dialog's search box (a portal, so React passes its focus and keys to the grid) from deleting the row.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components`. Expected: PASS, `line-items-grid.test.tsx` 39 tests and `line-items-grid.typeahead.test.tsx` 4 tests. Then `grep -n "activeRowIndex" src/pages/quotations/components/line-items-grid.tsx` prints nothing.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx frontend/src/pages/quotations/components/line-items-grid.typeahead.test.tsx && git commit -m "fix(quotations): keep the focus on the row that takes a deleted row's place"`

### Task 3.9 — The editing state follows the row

`editingQuantityCellId` and `editingMoneyCellId` (`:105-107`, compared at `:426`, `:456`, `:477`) hold a cell id. After Task 3.8 the survivor below keeps the focus while its id changes from row i+1 to row i, so it shows its formatted value and drops what is typed into **SL**, and the row below it shows its editing text. Key the state by `${rowKey}:${field}`, where `rowKey` is the `<tr>` key. Do not switch to cell components like the stock grid's `NumberCell`: it commits on every keystroke, while the quotation's **SL** commits on blur, so totals would update at a different time.

1. **Write the failing tests.**

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     describe('the cell being edited stays with its row when the row above it goes', () => {
       it('SL: the row that moves up keeps its raw text and takes the next digit; the row below shows its own value', () => {
         const { form } = renderGrid([
           line('a', { quantity: 2 }),
           line('b', { quantity: 1234.5 }),
           line('c', { quantity: 5678 }),
         ]);

         ctrlDelete('line-quantity-0');

         expect(focused()).toBe('line-quantity-0');
         expect(byId('line-quantity-0')).toHaveValue('1234.5');
         expect(byId('line-quantity-1')).toHaveValue('5,678');

         fireEvent.change(byId('line-quantity-0'), { target: { value: '9' } });
         act(() => byId('line-quantity-0').blur());

         expect(form().getValues('lines.0.quantity')).toBe(9);
         expect(form().getValues('lines.1.quantity')).toBe(5678);
       });

       it('Đơn giá bán: the row that moves up keeps its raw value; the row below shows its own formatted value', () => {
         renderGrid([
           line('a', { unitPrice: 100 }),
           line('b', { unitPrice: 1_234_500 }),
           line('c', { unitPrice: 5_678_000 }),
         ]);

         ctrlDelete('line-unit-price-0');

         expect(focused()).toBe('line-unit-price-0');
         expect(byId('line-unit-price-0')).toHaveValue('1234500');
         expect(byId('line-unit-price-1')).toHaveValue('5.678.000');

         fireEvent.change(byId('line-unit-price-0'), { target: { value: '9' } });
         expect(byId('line-unit-price-0')).toHaveValue('9');
         expect(byId('line-unit-price-1')).toHaveValue('5.678.000');
       });
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 2 FAIL: `toHaveValue("1234.5")` receives `1,234.50`, and `toHaveValue("1234500")` receives `1.234.500`. Why: the editing id is `line-quantity-1` / `line-unit-price-1`, set when the survivor took the focus as row 1; after the remove that row is row 0, so it no longer matches, and the row that is now row 1 (`c`) matches instead. The typed `9` would be dropped (the blur parses `1,234.50`, which `parseQuantityInput` rejects).
3. **Write the minimal implementation.** Rename the two states, since they no longer hold a cell id.

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     const [editingMoneyCellId, setEditingMoneyCellId] = useState<string | null>(null);
     const [editingQuantityCellId, setEditingQuantityCellId] = useState<string | null>(null);
   ```
   After:
   ```tsx
     // The cell being edited, as `${rowKey}:${field}`: a cell id would change when a row above it goes.
     const [editingMoneyKey, setEditingMoneyKey] = useState<string | null>(null);
     const [editingQuantityKey, setEditingQuantityKey] = useState<string | null>(null);
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                 const lineProfit = lineCost != null ? lineTotal - lineCost : undefined;
                 return (
                   <tr key={line._uiKey ?? field.id} data-row-index={idx}>
   ```
   After:
   ```tsx
                 const lineProfit = lineCost != null ? lineTotal - lineCost : undefined;
                 const rowKey = line._uiKey ?? field.id;
                 return (
                   <tr key={rowKey} data-row-index={idx}>
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                           editingQuantityCellId === quantityCellId
   ```
   After:
   ```tsx
                           editingQuantityKey === `${rowKey}:quantity`
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                           setEditingQuantityCellId(quantityCellId);
   ```
   After:
   ```tsx
                           setEditingQuantityKey(`${rowKey}:quantity`);
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                           setEditingQuantityCellId(null);
   ```
   After:
   ```tsx
                           setEditingQuantityKey(null);
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                         value={editingMoneyCellId === getLineCellId('unit-price', idx) ? (line.unitPrice ?? '') : formatMoneyForDisplay(line.unitPrice)}
                         onFocus={(e) => {
                           const el = e.currentTarget;
                           setEditingMoneyCellId(getLineCellId('unit-price', idx));
                           requestAnimationFrame(() => el.select());
                         }}
                         onBlur={() => setEditingMoneyCellId(null)}
   ```
   After:
   ```tsx
                         value={editingMoneyKey === `${rowKey}:unit-price` ? (line.unitPrice ?? '') : formatMoneyForDisplay(line.unitPrice)}
                         onFocus={(e) => {
                           const el = e.currentTarget;
                           setEditingMoneyKey(`${rowKey}:unit-price`);
                           requestAnimationFrame(() => el.select());
                         }}
                         onBlur={() => setEditingMoneyKey(null)}
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                           value={editingMoneyCellId === getLineCellId('unit-cost', idx) ? (line.unitCost ?? '') : formatMoneyForDisplay(line.unitCost)}
                           onFocus={(e) => {
                             const el = e.currentTarget;
                             setEditingMoneyCellId(getLineCellId('unit-cost', idx));
                             requestAnimationFrame(() => el.select());
                           }}
                           onBlur={() => setEditingMoneyCellId(null)}
   ```
   After:
   ```tsx
                           value={editingMoneyKey === `${rowKey}:unit-cost` ? (line.unitCost ?? '') : formatMoneyForDisplay(line.unitCost)}
                           onFocus={(e) => {
                             const el = e.currentTarget;
                             setEditingMoneyKey(`${rowKey}:unit-cost`);
                             requestAnimationFrame(() => el.select());
                           }}
                           onBlur={() => setEditingMoneyKey(null)}
   ```
   `quantityCellId` stays: it is still the input's id.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components`. Expected: PASS, `line-items-grid.test.tsx` 41 tests, the typeahead file 4. Then `grep -n "CellId" src/pages/quotations/components/line-items-grid.tsx` shows only `getLineCellId`, `parseLineCellId` and `quantityCellId`.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "fix(quotations): key the editing state by row so a delete above keeps the typed text"`

### Task 3.10 — New dimension columns flash

1. **Write the failing tests.** Case 4: md lines, then an m³ line.

   Before, in `src/pages/quotations/components/line-items-grid.test.tsx`:
   ```tsx
   function setCostPermission(enabled: boolean) {
   ```
   After:
   ```tsx
   const woodProduct: ProductSuggestion = {
     ...sheetProduct,
     id: '44444444-4444-4444-4444-444444444444',
     code: 'GO01',
     name: 'Gỗ thông',
     unitName: 'm³',
     pricingMode: 'PerCubicMeter',
   };

   function setCostPermission(enabled: boolean) {
   ```

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     describe('dimension columns that appear flash', () => {
       it('marks the new headers and every cell of the new columns, "—" cells included, when an m³ line joins md lines', async () => {
         renderGrid([md('a'), line('b')]);
         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài']);

         pick(1, woodProduct);

         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng', 'Dày']);
         for (const header of ['Rộng', 'Dày']) {
           expect(screen.getByRole('columnheader', { name: header })).toHaveClass('col-appeared');
         }
         for (const header of ['Số tấm', 'Dài', 'SL']) {
           expect(screen.getByRole('columnheader', { name: header })).not.toHaveClass('col-appeared');
         }
         for (const f of ['width', 'thickness']) {
           expect(byId(`line-${f}-0`)).toHaveAttribute('placeholder', '—');
           expect(byId(`line-${f}-0`).closest('td')).toHaveClass('col-appeared');
           expect(byId(`line-${f}-1`).closest('td')).toHaveClass('col-appeared');
         }
         expect(byId('line-length-1').closest('td')).not.toHaveClass('col-appeared');
         // The flash lasts 1.2 s, longer than waitFor's default 1000 ms.
         await waitFor(() => expect(document.querySelector('.col-appeared')).toBeNull(), { timeout: 2000 });
       });

       it('marks nothing on mount', () => {
         renderGrid([m3('a'), line('b')]);
         expect(document.querySelector('.col-appeared')).toBeNull();
       });
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 1 FAIL, `expect(element).toHaveClass("col-appeared")` on the **Rộng** header: no code sets the class yet. "marks nothing on mount" passes on the base; it guards opening a saved quotation, a draft and **Nhân bản** (case 9).
3. **Write the minimal implementation.** The `col-appeared` rules and their reduced-motion rule come from Phase 01 in `line-items-grid.css`, which this grid already imports.

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
   import { toOptionalNumber } from '@/lib/to-number';
   ```
   After:
   ```tsx
   import { toOptionalNumber } from '@/lib/to-number';
   import { useAppearedKeys } from '@/lib/use-appeared-keys';
   import { cn } from '@/lib/utils';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     const dims = shownDimensions(rows);
   ```
   After:
   ```tsx
     const dims = shownDimensions(rows);
     // Columns that appear flash for a moment (class col-appeared in line-items-grid.css), never on the first render.
     const appeared = useAppearedKeys(dims);
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                   <th key={c.key} className={c.key === 'row-no' ? 'row-no' : undefined}>
                     {c.header}
                   </th>
   ```
   After:
   ```tsx
                   <th
                     key={c.key}
                     className={cn(c.key === 'row-no' && 'row-no', appeared.has(c.key) && 'col-appeared') || undefined}
                   >
                     {c.header}
                   </th>
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
                         <td key={f} className="cell-number">
   ```
   After:
   ```tsx
                         <td key={f} className={cn('cell-number', appeared.has(f) && 'col-appeared')}>
   ```
   Do not put `cell-number` on the `<th>`: `.accounting-grid .cell-number` would right-align the header text, which today is centred.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: PASS, 43 tests (the flash test takes about 1.2 s).
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "feat(quotations): flash dimension columns that appear"`

### Task 3.11 — Scroll anchor when the columns change

The trigger is the shown list as a string (`dims.join(',')`), never the array, which is new on every render. A `useLayoutEffect` keyed on that string skips the mount through a ref. When the string changes and `document.activeElement` is inside the wrapper, it calls `scrollIntoView({ block: 'nearest', inline: 'nearest' })` on it. The wrapper is the existing `wrapRef` (`line-items-grid.tsx:103`). The quotation has no pinned columns, so it needs no scroll margin.

1. **Write the failing tests.**

   Append inside the outer `describe` of `src/pages/quotations/components/line-items-grid.test.tsx`, before its last line `});`:
   ```tsx
     describe('the focused cell stays in view when the dimension columns change', () => {
       afterEach(() => vi.restoreAllMocks());

       it('scrolls the focused cell into view once after a delete hides Dày', () => {
         const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
         renderGrid([m3('a'), m2('b')]);

         ctrlDelete('line-thickness-0');

         expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
         expect(focused()).toBe('line-product-code-0');
         expect(scroll).toHaveBeenCalledTimes(1);
         expect(scroll).toHaveBeenCalledWith({ block: 'nearest', inline: 'nearest' });
         expect(scroll.mock.contexts[0]).toBe(document.activeElement);
       });

       it('does not scroll when the columns change with the focus outside the grid', () => {
         const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
         const { form } = renderGrid([m3('a')]);

         act(() => form().reset({ ...form().getValues(), lines: [line('b')] }));

         expect(shownDimensionHeaders()).toEqual([]);
         expect(scroll).not.toHaveBeenCalled();
       });

       it('does not scroll on a re-render that keeps the columns', () => {
         const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
         renderGrid([m3('a')]);
         act(() => byId('line-length-0').focus());

         fireEvent.change(byId('line-length-0'), { target: { value: '3500' } });

         expect(byId('line-length-0')).toHaveValue(3500);
         expect(scroll).not.toHaveBeenCalled();
       });
     });
   ```
2. **Run them and check they fail:** `npx vitest run src/pages/quotations/components/line-items-grid.test.tsx`. Expected: 1 FAIL, `expected "Mock" to be called 1 times, but got 0 times`: nothing calls `scrollIntoView` yet. The two "does not scroll" tests pass on the base; they guard the trigger (focus outside the grid, as after a 409 reload or clearing a draft; a re-render without a column change, as on every keystroke).
3. **Write the minimal implementation.**

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
   import { forwardRef, useImperativeHandle, useRef, useState, type KeyboardEvent } from 'react';
   ```
   After:
   ```tsx
   import { forwardRef, useImperativeHandle, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react';
   ```

   Before, in `src/pages/quotations/components/line-items-grid.tsx`:
   ```tsx
     const minWidth = columns.reduce((sum, c) => sum + (c.width ?? NAME_MIN_WIDTH), 0);
   ```
   After:
   ```tsx
     const minWidth = columns.reduce((sum, c) => sum + (c.width ?? NAME_MIN_WIDTH), 0);
     // Showing or hiding a dimension column shifts the columns to its right: keep the focused cell in view. Keyed on the
     // joined list (the array is new on every render); the first render only records it.
     const dimsKey = dims.join(',');
     const dimsKeyRef = useRef<string | null>(null);
     useLayoutEffect(() => {
       const previous = dimsKeyRef.current;
       dimsKeyRef.current = dimsKey;
       if (previous === null || previous === dimsKey) return;
       const active = document.activeElement;
       if (active && wrapRef.current?.contains(active)) active.scrollIntoView({ block: 'nearest', inline: 'nearest' });
     }, [dimsKey]);
   ```
   `previous === dimsKey` covers the second effect run of React 18 `StrictMode` in development, which re-runs the effect with an unchanged string.
4. **Run the tests and check they pass:** `npx vitest run src/pages/quotations/components`. Expected: PASS, `line-items-grid.test.tsx` 46 tests, `line-items-grid.typeahead.test.tsx` 4, and the other two files unchanged.
5. **Commit:** `cd "$WT" && git add frontend/src/pages/quotations/components/line-items-grid.tsx frontend/src/pages/quotations/components/line-items-grid.test.tsx && git commit -m "feat(quotations): keep the focused cell in view when dimension columns change"`

## Verification

- `npx vitest run src/pages/quotations src/pages/stock-vouchers src/pages/inventory src/pages/settings` (the quotation page tests mock the grid; the stock grid shares `line-items-grid.css` and the typeahead; opening stock, the stock card and recalc cost use the typeahead and pass `openOnFocus={false}` (PR #31)).
- `npm run typecheck`
- `npm run build` (expected: success; this is the only check that parses `line-items-grid.css` after the `.dxr-cell` removal), then `git checkout -- tsconfig.app.tsbuildinfo`; `git status --short` is empty.
- `npm run lint` (no new warning; `line-items-grid.tsx` keeps its existing `react-refresh/only-export-components` warning for `createEmptyLine`).
- `grep -rn "dxr-cell\|activeRowIndex\|D × R × Dày × Tấm" src/pages/quotations` prints nothing.
- `grep -n "col-appeared" src/pages/quotations/components/line-items-grid.css` shows the Phase 01 rules (header, cell, disabled input, reduced motion).
- The real-browser walk of mockup 03 steps 1–8 on the quotation, both themes and reduced motion, is in Phase 04.
- No build artifact or line-ending change slipped in: `cd "$WT" && git diff <phase base> --numstat` equals the same command with `--ignore-cr-at-eol`, and `git log <phase base>.. -- frontend/tsconfig.app.tsbuildinfo` prints nothing (`<phase base>` is the hash recorded in "Before Task 3.1").
- Set **Status:** to `[x] done (<today>)` in this file and commit it: `cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-03-quotation-grid.md && git commit -m "docs(plan): phase 03 done"`.

## Exit Criteria

- The quotation grid shows **Số tấm**, **Dài**, **Rộng**, **Dày** as four columns (70, 76, 76, 64 px) between **ĐVT** and **SL**, and only those the highest pricing mode of the lines needs; a blank, unit-only or empty quotation shows none. Enter walks them in that order.
- A dimension cell that its row does not use keeps its id, is disabled, is empty with the placeholder "—", and the form keeps the stored value. A line without a known mode behaves as `PerUnit`.
- The table's inline `min-width` is 780, 1136, 1066 or 1422 px for the four combinations; the empty row spans 8 columns, or 11 with `quotations.view_cost`. `.accounting-grid { min-width: 1360px }` is unchanged.
- Without `quotations.view_cost`, Enter and Shift+Enter cross rows from and to **Đơn giá bán**.
- The quotation's `ProductTypeaheadCell` has `openOnFocus={false}`; with the real typeahead, the caret arriving in a filled **Mã hàng** (by Enter, after a trash click, or after Ctrl+Delete in **Mã hàng**) leaves the list closed and the next Enter goes to **Tên hàng** without changing the row.
- Ctrl+Delete and the trash button move the focus to the survivor (below, else above): the same cell when it is rendered and enabled there, else its **Mã hàng**, else `line-add`. `activeRowIndex` and the `<tr onFocus>` are gone; every line `<tr>` has `data-row-index`.
- The editing state is keyed `${rowKey}:${field}`; a digit typed right after a delete lands in the row that moved up.
- New dimension columns carry `col-appeared` on the `<th>` and every `<td>` (none on mount), and a column change calls `scrollIntoView` on the focused cell inside the grid only.
- All tests listed in Verification pass; typecheck and lint are clean. No help file is changed in this phase (Phase 04 updates `quotations.form.md`, `inventory-opening-stock.md` and `inventory-stock-card.md` last).
