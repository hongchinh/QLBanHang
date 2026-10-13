# Phase 04 — Grid: columns, sticky, settings popover, keyboard navigation

**Status:** [ ] pending
**Complexity:** L

## Objective

Make the line grid fast to type into, following brainstorm §2:

- The grid renders from **one column definition array**. Optional columns follow the user's prefs and are force-shown when needed (`forcedColumns`).
- # / Mã hàng / Tên hàng stick to the left, Số còn lại and the delete button to the right, and the header row sticks to the top. The grid fills the Hàng hóa tab and scrolls inside it.
- A toolbar above the grid holds "Thêm dòng", the **"⚙ Cột & phím Enter"** popover and the keyboard guide.
- **Enter** follows the row's pricing-mode path (`enterPath`):
  - Shift+Enter goes backwards across rows.
  - Enter on a Mã without a product does nothing.
  - Picking a product focuses its second stop.
- **↑/↓** move to the same column in the row above/below. **Ctrl+Delete** removes the row.

## Files

- `frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx` (modify)
- `frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx` (modify)
- `frontend/src/pages/stock-vouchers/components/stock-line-grid.css` (new)
- `frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.test.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.tsx` (modify: the lines panel no longer scrolls itself)

## Shared test helper (add once, in Task 4.1)

In `stock-line-grid.test.tsx`:

- Add `localStorage.clear();` to `beforeEach`.
- Add these helpers. The grid reads the user id from the real auth store, which is empty in these tests, so the key is `stockGridPrefsKey('', type)`.
  ```ts
  import { stockGridPrefsKey } from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';

  function storePrefs(prefs: { hiddenColumns?: string[]; enterStops?: Record<string, string[]> }, type: 'In' | 'Out' = 'In') {
    localStorage.setItem(
      stockGridPrefsKey('', type),
      JSON.stringify({ version: 1, hiddenColumns: prefs.hiddenColumns ?? [], enterStops: prefs.enterStops ?? {} }),
    );
  }
  const showAllColumns = () => storePrefs({ hiddenColumns: [] });

  function headers() {
    return screen.getAllByRole('columnheader').map((th) => th.textContent);
  }
  ```

## Tasks

### Task 4.1 — Column definitions, optional and forced columns

1. **Write the failing tests:**
   ```ts
   it('hides Kho, Tiền CK, Tiền VAT and Ghi chú by default', () => {
     renderGrid({ lines: [line()] });
     expect(headers()).toEqual([
       'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'SL tồn',
       'Đơn giá', 'Số tiền', '%CK', '%VAT', 'Số còn lại', '',
     ]);
     expect(document.getElementById('stock-line-warehouse-0')).toBeNull();
     expect(document.getElementById('stock-line-note-0')).toBeNull();
   });

   it('stored prefs show every column', () => {
     showAllColumns();
     renderGrid({ lines: [line()] });
     expect(headers()).toEqual([
       'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Kho', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'SL tồn',
       'Đơn giá', 'Số tiền', '%CK', 'Tiền CK', '%VAT', 'Tiền VAT', 'Ghi chú', 'Số còn lại', '',
     ]);
   });

   it('forces Kho when a line leaves the header warehouse', () => {
     renderGrid({ lines: [line(), line({ _uiKey: 'b', warehouseId: WH2 })] });
     expect(headers()).toContain('Kho');
   });
   ```
   For the error case, use the harness's form: change `renderGrid` to also return `formRef`. Return `{ values: () => formRef.current!.getValues(), form: () => formRef.current! }`, and update existing callers (`const values = renderGrid(...)` → `const { values } = renderGrid(...)`). Then:
   ```ts
   it('a line error on a hidden column shows it', async () => {
     const { form } = renderGrid({ lines: [line()] });
     expect(headers()).not.toContain('Ghi chú');
     act(() => form().setError('lines.0.note', { type: 'server', message: 'Quá dài' }));
     await waitFor(() => expect(headers()).toContain('Ghi chú'));
   });
   ```
   Update `'editing discount amount switches the line to manual discount'` to call `showAllColumns()` first, because Tiền CK is hidden by default.
2. **Run:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx` → FAIL (the header list still has all 20 columns).
3. **Minimal implementation** in `stock-line-grid.tsx`:
   - **Column array.** Add it at module level. Ghi chú moves before Số còn lại, so that the sticky-right columns are the trailing ones:
     ```ts
     type ColumnKey =
       | 'row-no' | 'product-code' | 'product-name' | 'unit' | 'warehouse' | 'sheet-count' | 'length' | 'width'
       | 'thickness' | 'quantity' | 'stock-at' | 'unit-price' | 'amount' | 'discount-rate' | 'discount-amount'
       | 'vat-rate' | 'vat-amount' | 'note' | 'net-amount' | 'action';

     interface GridColumn {
       key: ColumnKey;
       header: string;
       width: number;
       numeric?: boolean;
       sticky?: 'left' | 'right';
     }

     const COLUMNS: readonly GridColumn[] = [
       { key: 'row-no', header: 'STT', width: 42, sticky: 'left' },
       { key: 'product-code', header: 'Mã hàng', width: 120, sticky: 'left' },
       { key: 'product-name', header: 'Tên hàng', width: 220, sticky: 'left' },
       { key: 'unit', header: 'ĐVT', width: 56 },
       { key: 'warehouse', header: 'Kho', width: 130 },
       { key: 'sheet-count', header: 'Số tấm', width: 70, numeric: true },
       { key: 'length', header: 'Dài', width: 76, numeric: true },
       { key: 'width', header: 'Rộng', width: 76, numeric: true },
       { key: 'thickness', header: 'Dày', width: 64, numeric: true },
       { key: 'quantity', header: 'SL', width: 96, numeric: true },
       { key: 'stock-at', header: 'SL tồn', width: 96, numeric: true },
       { key: 'unit-price', header: 'Đơn giá', width: 110, numeric: true },
       { key: 'amount', header: 'Số tiền', width: 120, numeric: true },
       { key: 'discount-rate', header: '%CK', width: 60, numeric: true },
       { key: 'discount-amount', header: 'Tiền CK', width: 104, numeric: true },
       { key: 'vat-rate', header: '%VAT', width: 60, numeric: true },
       { key: 'vat-amount', header: 'Tiền VAT', width: 104, numeric: true },
       { key: 'note', header: 'Ghi chú', width: 160 },
       { key: 'net-amount', header: 'Số còn lại', width: 120, numeric: true, sticky: 'right' },
       { key: 'action', header: '', width: 42, sticky: 'right' },
     ];

     const isOptional = (key: ColumnKey): key is OptionalColumn => (OPTIONAL_COLUMNS as readonly string[]).includes(key);
     ```
   - **Inside the component:**
     ```ts
     const userId = useAuthStore((s) => s.user?.id ?? '');
     const { prefs, toggleColumn, toggleEnterStop, reset } = useStockGridPrefs(userId, type);
     const headerWarehouseId = useWatch({ control: form.control, name: 'warehouseId' }) ?? '';
     const forced = forcedColumns(rows, headerWarehouseId, lineErrors as (Record<string, unknown> | undefined)[] | undefined);
     const isShown = (key: ColumnKey) =>
       !isOptional(key) || !prefs.hiddenColumns.includes(key) || forced.has(key);
     const columns = COLUMNS.filter((c) => isShown(c.key) && !(readOnly && c.key === 'action'));
     const minWidth = columns.reduce((sum, c) => sum + c.width, 0);
     ```
     `toggleColumn`, `toggleEnterStop` and `reset` are used in Task 4.3. Until then, prefix the unused ones with `void` or wire them in 4.3 within the same commit, whichever keeps lint clean.
   - **Render.** `colgroup`, `thead` and the `tbody` row all render with `columns.map(...)`. Move each existing `<td>` body into `function renderCell(key: ColumnKey, idx: number, line, result)`, a `switch` returning the existing JSX for that key (the `numberInput` / `percentInput` helpers stay). The empty-row placeholder uses `colSpan={columns.length}`. Header classes: `row-no` for STT, `cell-number` when `numeric`.
   - Import `useAuthStore`, `useStockGridPrefs`, `forcedColumns`, `OPTIONAL_COLUMNS` and `OptionalColumn`.
4. **Run:** same command → PASS (old and new grid tests).
5. **Commit:** `git commit -m "feat(frontend): stock grid column definitions with optional columns"`.

### Task 4.2 — Sticky columns and header, full-height scroll, toolbar on top

1. **Write the failing test:**
   ```ts
   it('pins #, Mã and Tên left and Số còn lại + delete right with cumulative offsets', () => {
     renderGrid({ lines: [line()] });
     const ths = screen.getAllByRole('columnheader');
     const byText = (t: string) => ths.find((th) => th.textContent === t)!;
     expect(byText('STT')).toHaveClass('sticky-left');
     expect(byText('STT')).toHaveStyle({ left: '0px' });
     expect(byText('Mã hàng')).toHaveStyle({ left: '42px' });
     expect(byText('Tên hàng')).toHaveStyle({ left: '162px' });
     expect(byText('Số còn lại')).toHaveClass('sticky-right');
     expect(byText('Số còn lại')).toHaveStyle({ right: '42px' });
     expect(byText('ĐVT')).not.toHaveClass('sticky-left');
     // Toolbar sits above the table.
     const toolbar = screen.getByRole('button', { name: /Thêm dòng/ });
     expect(toolbar.compareDocumentPosition(screen.getByRole('table')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
   });
   ```
2. **Run:** same file → FAIL (no `sticky-left` class).
3. **Minimal implementation.**
   - **Offsets.** Compute them once per render:
     ```ts
     const stickyOffset = new Map<ColumnKey, CSSProperties>();
     let left = 0;
     for (const c of columns) if (c.sticky === 'left') { stickyOffset.set(c.key, { left }); left += c.width; }
     let right = 0;
     for (const c of [...columns].reverse()) if (c.sticky === 'right') { stickyOffset.set(c.key, { right }); right += c.width; }
     const cellClass = (c: GridColumn, extra?: string) =>
       cn(c.numeric && 'cell-number', c.key === 'row-no' && 'row-no', c.key === 'action' && 'cell-action',
          c.sticky === 'left' && 'sticky-left', c.sticky === 'right' && 'sticky-right', extra);
     ```
     Apply `className={cellClass(c)}` and `style={stickyOffset.get(c.key)}` on every `th` and `td`. Import `cn` from `@/lib/utils` and `type CSSProperties` from `react`.
   - **Layout.** The component root becomes `<div className="flex h-full min-h-0 flex-col">`. The toolbar comes first:
     ```tsx
     {!readOnly && (
       <div className="flex shrink-0 flex-wrap items-center justify-between gap-2 border-b px-2 py-1.5">
         <div className="flex items-center gap-2">
           <button type="button" className="lib-btn" onClick={addLine}><Plus className="h-4 w-4 text-cyan-600" />Thêm dòng</button>
           {/* Task 4.3: <StockGridSettingsPopover … /> */}
         </div>
         <div className="keyboard-guide">…</div>
       </div>
     )}
     ```
     Then the scroll box: `<div className="accounting-grid-wrap stock-grid-wrap min-h-0 flex-1" tabIndex={-1} onKeyDown={handleGridKeyDown} style={{ '--stock-grid-left': `${left}px` } as CSSProperties}>`, with `<table className="accounting-grid stock-grid" style={{ minWidth }}>`. Remove the old footer and toolbar blocks below the table.
   - **CSS.** Create `stock-line-grid.css` and import it after `line-items-grid.css`:
     ```css
     /* stock-line-grid.css — stock voucher grid on top of the shared accounting-grid styles:
      * the grid scrolls inside its tab, the header row and the #/Mã/Tên and Số còn lại/delete columns stick. */
     .stock-grid-wrap {
       overflow: auto;
     }

     .stock-grid thead th {
       position: sticky;
       top: 0;
       z-index: 3;
     }

     .stock-grid .sticky-left,
     .stock-grid .sticky-right {
       position: sticky;
       z-index: 1;
     }

     .stock-grid thead th.sticky-left,
     .stock-grid thead th.sticky-right {
       z-index: 4;
     }

     /* Sticky cells need an opaque background (the hover tint of the shared grid is translucent). */
     .stock-grid td.sticky-left,
     .stock-grid td.sticky-right {
       background: hsl(var(--card));
     }

     .stock-grid tr:hover td.sticky-left,
     .stock-grid tr:hover td.sticky-right {
       background: hsl(var(--accent));
     }

     /* border-collapse drops borders on sticky cells while scrolling: draw the edges as shadows. */
     .stock-grid .sticky-left {
       box-shadow: inset -1px 0 0 hsl(var(--border));
     }

     .stock-grid .sticky-right {
       box-shadow: inset 1px 0 0 hsl(var(--border));
     }

     /* A focused cell scrolls into view clear of the sticky header and left columns. */
     .stock-grid .cell-input {
       scroll-margin-top: 40px;
       scroll-margin-left: var(--stock-grid-left, 0px);
     }
     ```
   - **Tabs.** In `stock-voucher-tabs.tsx`, change the lines panel class from `… flex-1 overflow-auto …` to `… flex flex-1 flex-col overflow-hidden …`, so the grid's own box scrolls instead of the tab.
4. **Run:** `npx vitest run src/pages/stock-vouchers` → PASS.
5. **Commit:** `git commit -m "feat(frontend): sticky stock grid columns and full-height scroll"`.

### Task 4.3 — "⚙ Cột & phím Enter" popover

1. **Write the failing test** `stock-grid-settings-popover.test.tsx`:
   ```ts
   import { describe, expect, it, vi } from 'vitest';
   import { fireEvent, render, screen } from '@testing-library/react';
   import { StockGridSettingsPopover } from './stock-grid-settings-popover';
   import { DEFAULT_STOCK_GRID_PREFS } from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';

   describe('StockGridSettingsPopover', () => {
     it('toggles columns and enter stops, and resets', () => {
       const onToggleColumn = vi.fn();
       const onToggleEnterStop = vi.fn();
       const onReset = vi.fn();
       render(
         <StockGridSettingsPopover
           prefs={DEFAULT_STOCK_GRID_PREFS}
           onToggleColumn={onToggleColumn}
           onToggleEnterStop={onToggleEnterStop}
           onReset={onReset}
         />,
       );
       fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));

       expect(screen.getByRole('checkbox', { name: 'Ghi chú' })).not.toBeChecked();
       expect(screen.getByRole('checkbox', { name: '%VAT' })).toBeChecked();
       fireEvent.click(screen.getByRole('checkbox', { name: 'Ghi chú' }));
       expect(onToggleColumn).toHaveBeenCalledWith('note');

       expect(screen.getByRole('checkbox', { name: 'Enter dừng ở SL — Cái' })).toBeChecked();
       expect(screen.getByRole('checkbox', { name: 'Enter dừng ở Rộng — m²' })).toBeChecked();
       expect(screen.queryByRole('checkbox', { name: 'Enter dừng ở Rộng — Cái' })).not.toBeInTheDocument();
       fireEvent.click(screen.getByRole('checkbox', { name: 'Enter dừng ở %VAT — Cái' }));
       expect(onToggleEnterStop).toHaveBeenCalledWith('PerUnit', 'vat-rate');

       fireEvent.click(screen.getByRole('button', { name: 'Khôi phục mặc định' }));
       expect(onReset).toHaveBeenCalled();
     });
   });
   ```
2. **Run:** `npx vitest run src/pages/stock-vouchers/components/stock-grid-settings-popover.test.tsx` → FAIL, module not found.
3. **Minimal implementation** `stock-grid-settings-popover.tsx`:
   ```tsx
   import { Settings2 } from 'lucide-react';
   import { Button } from '@/components/ui/button';
   import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
   import type { PricingMode } from '@/features/products/types';
   import { ENTER_STOP_FIELDS, PRICING_MODES, isFieldDisabled, type EnterStopField } from '@/pages/stock-vouchers/utils/line-navigation';
   import { OPTIONAL_COLUMNS, type OptionalColumn, type StockGridPrefs } from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';

   const COLUMN_LABELS: Record<OptionalColumn, string> = {
     warehouse: 'Kho', 'stock-at': 'SL tồn', amount: 'Số tiền', 'discount-rate': '%CK',
     'discount-amount': 'Tiền CK', 'vat-rate': '%VAT', 'vat-amount': 'Tiền VAT', note: 'Ghi chú',
   };
   const FIELD_LABELS: Record<EnterStopField, string> = {
     warehouse: 'Kho', 'sheet-count': 'Tấm', length: 'Dài', width: 'Rộng', thickness: 'Dày', quantity: 'SL',
     'unit-price': 'Đơn giá', 'discount-rate': '%CK', 'discount-amount': 'Tiền CK', 'vat-rate': '%VAT', note: 'Ghi chú',
   };
   const MODE_LABELS: Record<PricingMode, string> = { PerUnit: 'Cái', PerLinearMeter: 'md', PerSquareMeter: 'm²', PerCubicMeter: 'm³' };

   export interface StockGridSettingsPopoverProps {
     prefs: StockGridPrefs;
     onToggleColumn: (column: OptionalColumn) => void;
     onToggleEnterStop: (mode: PricingMode, field: EnterStopField) => void;
     onReset: () => void;
   }

   // Per-user grid settings: which optional columns show and where Enter stops for each pricing mode.
   export function StockGridSettingsPopover({ prefs, onToggleColumn, onToggleEnterStop, onReset }: StockGridSettingsPopoverProps) {
     return (
       <Popover>
         <PopoverTrigger asChild>
           <Button type="button" variant="outline" size="sm" className="h-7">
             <Settings2 className="mr-1.5 h-4 w-4" />
             Cột &amp; phím Enter
           </Button>
         </PopoverTrigger>
         <PopoverContent align="start" className="w-[560px] max-w-[calc(100vw-32px)] space-y-3 text-sm">
           <section>
             <h3 className="mb-1.5 font-semibold">Hiện cột</h3>
             <div className="grid grid-cols-2 gap-x-4 gap-y-1">
               {OPTIONAL_COLUMNS.map((c) => (
                 <label key={c} className="flex items-center gap-2" title={c === 'warehouse' ? 'Tự hiện khi có dòng khác kho phiếu' : undefined}>
                   <input type="checkbox" checked={!prefs.hiddenColumns.includes(c)} onChange={() => onToggleColumn(c)} />
                   {COLUMN_LABELS[c]}
                 </label>
               ))}
             </div>
           </section>
           <section>
             <h3 className="mb-1.5 font-semibold">Phím Enter dừng ở (Mã hàng luôn là ô đầu)</h3>
             <div className="overflow-x-auto">
               <table className="w-full border-collapse text-center text-xs">
                 <thead>
                   <tr>
                     <th className="border p-1" />
                     {ENTER_STOP_FIELDS.map((f) => <th key={f} className="border bg-muted p-1 font-semibold">{FIELD_LABELS[f]}</th>)}
                   </tr>
                 </thead>
                 <tbody>
                   {PRICING_MODES.map((mode) => (
                     <tr key={mode}>
                       <th className="border p-1 text-left font-semibold">{MODE_LABELS[mode]}</th>
                       {ENTER_STOP_FIELDS.map((f) => (
                         <td key={f} className="border p-1">
                           {isFieldDisabled(mode, f) ? (
                             <span className="text-muted-foreground">–</span>
                           ) : (
                             <input
                               type="checkbox"
                               aria-label={`Enter dừng ở ${FIELD_LABELS[f]} — ${MODE_LABELS[mode]}`}
                               checked={prefs.enterStops[mode].includes(f)}
                               onChange={() => onToggleEnterStop(mode, f)}
                             />
                           )}
                         </td>
                       ))}
                     </tr>
                   ))}
                 </tbody>
               </table>
             </div>
           </section>
           <div className="text-right">
             <Button type="button" variant="outline" size="sm" onClick={onReset}>Khôi phục mặc định</Button>
           </div>
         </PopoverContent>
       </Popover>
     );
   }
   ```
   Check that `Settings2` exists in the installed `lucide-react`. If it does not, use `Settings`. Then wire it into the grid toolbar (Task 4.2 placeholder):
   ```tsx
   <StockGridSettingsPopover prefs={prefs} onToggleColumn={toggleColumn} onToggleEnterStop={toggleEnterStop} onReset={reset} />
   ```
   Also add a grid test:
   ```ts
   it('the settings popover shows a hidden column at once', () => {
     renderGrid({ lines: [line()] });
     fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));
     fireEvent.click(screen.getByRole('checkbox', { name: 'Ghi chú' }));
     expect(headers()).toContain('Ghi chú');
   });
   ```
4. **Run:** `npx vitest run src/pages/stock-vouchers/components` → PASS.
5. **Commit:** `git commit -m "feat(frontend): stock grid column and enter-stop settings"`.

### Task 4.4 — Enter path by pricing mode, focus after product pick

1. **Write the failing tests** (grid file):
   ```ts
   const enter = (id: string, shift = false) => fireEvent.keyDown(byId(id), { key: 'Enter', shiftKey: shift });
   const focused = () => document.activeElement?.id;

   it('picking a m² product focuses Số tấm, then Enter walks Dài, Rộng, Đơn giá and opens a new line', async () => {
     renderGrid({ lines: [createEmptyStockLine(0, WH1)] });
     await pickFromTypeahead('TON01');
     await waitFor(() => expect(focused()).toBe('stock-line-sheet-count-0'));
     enter('stock-line-sheet-count-0');
     expect(focused()).toBe('stock-line-length-0');
     enter('stock-line-length-0');
     expect(focused()).toBe('stock-line-width-0');
     enter('stock-line-width-0');
     expect(focused()).toBe('stock-line-unit-price-0');
     enter('stock-line-unit-price-0');
     await waitFor(() => expect(focused()).toBe('stock-line-product-code-1'));
   });

   it('a PerUnit line stops at SL and Đơn giá; stored stops add %VAT', () => {
     storePrefs({ hiddenColumns: [], enterStops: { PerUnit: ['quantity', 'unit-price', 'vat-rate'] } });
     renderGrid({ lines: [line({ pricingMode: 'PerUnit' }), line({ _uiKey: 'b', pricingMode: 'PerUnit' })] });
     enter('stock-line-product-code-0');
     expect(focused()).toBe('stock-line-quantity-0');
     enter('stock-line-quantity-0');
     expect(focused()).toBe('stock-line-unit-price-0');
     enter('stock-line-unit-price-0');
     expect(focused()).toBe('stock-line-vat-rate-0');
     enter('stock-line-vat-rate-0');
     expect(focused()).toBe('stock-line-product-code-1');
   });

   it('Shift+Enter from a product code goes to the last stop of the line above, by its mode', () => {
     renderGrid({ lines: [line({ pricingMode: 'PerSquareMeter' }), line({ _uiKey: 'b' })] });
     enter('stock-line-product-code-1', true);
     expect(focused()).toBe('stock-line-unit-price-0');
     enter('stock-line-unit-price-0', true);
     expect(focused()).toBe('stock-line-width-0');
   });

   it('Enter on a product code without a product does nothing', () => {
     renderGrid({ lines: [createEmptyStockLine(0, WH1)] });
     byId('stock-line-product-code-0').focus();
     enter('stock-line-product-code-0');
     expect(focused()).toBe('stock-line-product-code-0');
     expect(document.getElementById('stock-line-product-code-1')).toBeNull();
   });
   ```
   `line()` sets `productId: P1`, so those rows count as having a product.
2. **Run:** grid test file → FAIL (focus goes to `stock-line-warehouse-0` / Enter on an empty code moves on).
3. **Minimal implementation** in `stock-line-grid.tsx`:
   - **Delete `enabledFields`.** Add:
     ```ts
     const isVisible = (field: LineFocusField) => isShown(field);
     const pathOf = (rowIndex: number) => {
       const mode = rows[rowIndex]?.pricingMode ?? 'PerUnit';
       return enterPath(mode, prefs.enterStops[mode], isVisible);
     };

     function goNextRow(rowIndex: number) {
       if (rowIndex < fields.length - 1) focusCell('product-code', rowIndex + 1);
       else if (!readOnly) addLine();
     }

     function moveFocus(current: { field: LineFocusField; rowIndex: number }, direction: 1 | -1) {
       if (direction === 1) {
         // A row needs a product before Enter leaves the code cell (no empty rows).
         if (current.field === 'product-code' && !rows[current.rowIndex]?.productId) return;
         const next = nextStop(pathOf(current.rowIndex), current.field);
         if (next) focusCell(next, current.rowIndex);
         else goNextRow(current.rowIndex);
         return;
       }
       const previous = prevStop(pathOf(current.rowIndex), current.field);
       if (previous) return focusCell(previous, current.rowIndex);
       if (current.rowIndex > 0) {
         const above = pathOf(current.rowIndex - 1);
         focusCell(above[above.length - 1], current.rowIndex - 1);
       }
     }
     ```
     `ColumnKey` includes every `LineFocusField`, so `isShown(field)` type-checks. Import `enterPath`, `nextStop` and `prevStop`.
   - **Product pick.** On `ProductTypeaheadCell`, remove `nextFocusId` and change `onSelect` (D4):
     ```tsx
     onSelect={(s) => {
       applyProduct(idx, s);
       const next = nextStop(enterPath(s.pricingMode, prefs.enterStops[s.pricingMode], isVisible), 'product-code');
       if (next) focusCell(next, idx, true);
       else setTimeout(() => goNextRow(idx), 0);
     }}
     ```
4. **Run:** `npx vitest run src/pages/stock-vouchers` → PASS (including the page tests; `fillPerUnitLine` waits on `quantity` being enabled, which still holds).
5. **Commit:** `git commit -m "feat(frontend): stock grid enter path follows the pricing mode"`.

### Task 4.5 — ↑/↓, Ctrl+Delete, keyboard guide

1. **Write the failing tests:**
   ```ts
   it('ArrowDown keeps the column, or falls back to Mã when the row below cannot use it', () => {
     renderGrid({
       lines: [line({ pricingMode: 'PerSquareMeter' }), line({ _uiKey: 'b', pricingMode: 'PerUnit' }), line({ _uiKey: 'c', pricingMode: 'PerUnit' })],
     });
     fireEvent.keyDown(byId('stock-line-width-0'), { key: 'ArrowDown' });
     expect(focused()).toBe('stock-line-product-code-1');
     fireEvent.keyDown(byId('stock-line-unit-price-1'), { key: 'ArrowDown' });
     expect(focused()).toBe('stock-line-unit-price-2');
     fireEvent.keyDown(byId('stock-line-unit-price-2'), { key: 'ArrowUp' });
     expect(focused()).toBe('stock-line-unit-price-1');
   });

   it('arrows leave the warehouse select alone', () => {
     showAllColumns();
     renderGrid({ lines: [line(), line({ _uiKey: 'b' })] });
     byId('stock-line-warehouse-0').focus();
     fireEvent.keyDown(byId('stock-line-warehouse-0'), { key: 'ArrowDown' });
     expect(focused()).toBe('stock-line-warehouse-0');
   });

   it('Ctrl+Delete removes the focused line and keeps the column on the next one', async () => {
     const { values } = renderGrid({
       lines: [line({ productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' })],
     });
     fireEvent.keyDown(byId('stock-line-quantity-0'), { key: 'Delete', ctrlKey: true });
     await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['B']));
     await waitFor(() => expect(focused()).toBe('stock-line-quantity-0'));
   });

   it('the keyboard guide lists the new keys', () => {
     renderGrid({ lines: [line()] });
     const guide = document.querySelector('.keyboard-guide')!;
     expect(guide).toHaveTextContent('Ctrl+Del');
     expect(guide).toHaveTextContent('Alt+1…4');
     expect(guide).toHaveTextContent('Dòng trên/dưới');
   });
   ```
2. **Run:** grid test file → FAIL.
3. **Minimal implementation.** Extend `handleGridKeyDown`, after the `defaultPrevented` check and the Insert branch:
   ```ts
   const target = e.target as HTMLElement;
   const noMods = !e.ctrlKey && !e.altKey && !e.shiftKey && !e.metaKey;

   if ((e.key === 'ArrowUp' || e.key === 'ArrowDown') && noMods && target.tagName !== 'SELECT') {
     const current = parseCellId(target.id);
     const to = current ? current.rowIndex + (e.key === 'ArrowDown' ? 1 : -1) : -1;
     if (!current || to < 0 || to >= fields.length) return;
     e.preventDefault();
     focusCell(verticalTarget(rows[to]?.pricingMode ?? 'PerUnit', current.field, isVisible), to);
     return;
   }

   if (e.key === 'Delete' && e.ctrlKey && !e.altKey && !e.shiftKey && !e.metaKey && !readOnly) {
     const current = parseCellId(target.id);
     if (!current) return;
     e.preventDefault();
     remove(current.rowIndex);
     setTimeout(() => {
       const lines = form.getValues('lines');
       const to = Math.min(current.rowIndex, lines.length - 1);
       if (to >= 0) focusCell(verticalTarget(lines[to].pricingMode, current.field, isVisible), to);
     }, 0);
     return;
   }
   ```
   `ProductTypeaheadCell` calls `preventDefault()` on ↑/↓ while its list is open with results, so the `defaultPrevented` guard leaves the suggestion list alone. Import `verticalTarget`. Replace the keyboard guide content:
   ```tsx
   <span><span className="kbd">Enter</span> Tiếp</span>
   <span><span className="kbd">Shift</span>+<span className="kbd">Enter</span> Lùi</span>
   <span><span className="kbd">↑</span><span className="kbd">↓</span> Dòng trên/dưới</span>
   <span><span className="kbd">Insert</span> Thêm dòng</span>
   <span><span className="kbd">Ctrl</span>+<span className="kbd">Del</span> Xóa dòng</span>
   <span><span className="kbd">Ctrl</span>+<span className="kbd">S</span> Lưu</span>
   <span><span className="kbd">Alt</span>+<span className="kbd">1…4</span> Chuyển tab</span>
   ```
   `toHaveTextContent('Ctrl+Del')` reads the concatenated text "Ctrl+Del". If whitespace between the spans breaks the match, use a regex such as `/Ctrl\s*\+\s*Del/` in the test.
4. **Run:**
   ```bash
   npx vitest run src/pages/stock-vouchers
   npm run typecheck
   npm run lint
   ```
   Expected: PASS; lint counts ≤ baseline.
5. **Commit:** `git commit -m "feat(frontend): stock grid arrow keys and ctrl+delete"`.

## Verification

From `frontend/`:

- `npx vitest run src/pages/stock-vouchers` → PASS
- `npm run test` → FAIL lines == `BASELINE.md`
- `npm run typecheck` → PASS
- `npm run lint` → counts ≤ `BASELINE.md`
- `npm run build && git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` → PASS

## Exit Criteria

- With default prefs the grid shows 16 columns (15 when read-only). The popover changes columns and Enter stops at once, and they persist per user and direction.
- Kho shows automatically when a line uses another warehouse; any column with a line error shows.
- The header and the #/Mã/Tên and Số còn lại/delete columns stick; the grid scrolls inside the Hàng hóa tab.
- Enter, Shift+Enter, ↑/↓, Ctrl+Delete and the pick-focus behave as tested.
- Known limit, to note in the report: on a Mã cell whose suggestion list is open with results, ↑/↓ move in the list, not between rows; Esc closes the list.
- A team chat summary is posted.
