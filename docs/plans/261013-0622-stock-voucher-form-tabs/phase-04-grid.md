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
- `frontend/src/pages/quotations/components/product-typeahead-cell.tsx` (modify: optional `openOnFocus` prop, default `true`, so quotation behavior is unchanged)
- `docs/plans/261013-0622-stock-voucher-form-tabs/phase-04-grid.md` and `SUMMARY.md` (Task 4.6 only)

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

   it('keeps Kho hidden while every line uses the header warehouse', () => {
     renderGrid({ lines: [line(), line({ _uiKey: 'b' })] });
     expect(headers()).not.toContain('Kho');
   });

   it('a read-only grid has no delete column (15 headers by default)', () => {
     renderGrid({ lines: [line()], readOnly: true });
     expect(headers()).toHaveLength(15);
     expect(headers()).not.toContain('');
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

   it('an array-level lines error (empty grid) does not crash the render', async () => {
     const { form } = renderGrid({ lines: [line()] });
     act(() => form().setError('lines', { type: 'server', message: 'Phiếu phải có ít nhất 1 dòng' }));
     await waitFor(() => expect(headers()).toContain('STT'));
   });
   ```
   Update `'editing discount amount switches the line to manual discount'` to call `showAllColumns()` first, because Tiền CK is hidden by default.
2. **Run:** `npx vitest run src/pages/stock-vouchers/components/stock-line-grid.test.tsx` → FAIL. The default-columns, unforced-Kho, read-only and line-error tests fail (the old grid has all 20 columns). The `forces Kho`, `stored prefs show every column` and array-level-error tests may already pass: that is expected, they guard the new code from regressing.
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
     const forced = forcedColumns(rows, headerWarehouseId, lineErrors);
     const isShown = (key: ColumnKey) =>
       !isOptional(key) || !prefs.hiddenColumns.includes(key) || forced.has(key);
     const columns = COLUMNS.filter((c) => isShown(c.key) && !(readOnly && c.key === 'action'));
     const minWidth = columns.reduce((sum, c) => sum + c.width, 0);
     ```
     `lineErrors` is `form.formState.errors.lines` as the grid already reads it; `forcedColumns` takes `unknown` (Phase 02), so no cast is needed and an object-shaped error (empty-grid submit) is ignored safely.

     `toggleColumn`, `toggleEnterStop` and `reset` are used in Task 4.3. Until then add `void toggleColumn; void toggleEnterStop; void reset;` right after the hook call (`noUnusedLocals` fails typecheck, not only lint). Task 4.3 deletes that line.
   - **Cell wrapper (defined once, used by every `th` and `td`).** Add inside the component:
     ```ts
     const stickyOffset = new Map<ColumnKey, CSSProperties>(); // filled in Task 4.2
     const cellClass = (c: GridColumn, extra?: string) =>
       cn(c.numeric && 'cell-number', c.key === 'row-no' && 'row-no', c.key === 'action' && 'cell-action', extra);
     ```
   - **Render.** `colgroup`, `thead` and the `tbody` row all render with `columns.map(...)`. `renderCell(key: ColumnKey, idx: number, line, result)` is a `switch` that returns the cell **content only** (the inner JSX of each existing `<td>`; the literal `<td className="cell-number">` wrappers inside `numberInput` / `percentInput` are dropped, they return the input fragment). The row renders:
     ```tsx
     {columns.map((c) => (
       <td key={c.key} className={cellClass(c)} style={stickyOffset.get(c.key)}>
         {renderCell(c.key, idx, line, result)}
       </td>
     ))}
     ```
     and the header renders `<th key={c.key} className={cellClass(c)} style={stickyOffset.get(c.key)}>{c.header}</th>`. The `key` on each `td`/`th` keeps cell identity stable when a column is toggled (`NumberCell` holds local `editing` state). The empty-row placeholder uses `colSpan={columns.length}`.
   - Imports: `import { useAuthStore } from '@/stores/auth-store';`, `import { cn } from '@/lib/utils';`, `type CSSProperties` from `react`, and `useStockGridPrefs`, `forcedColumns`, `OPTIONAL_COLUMNS`, `OptionalColumn` from `@/pages/stock-vouchers/utils/use-stock-grid-prefs`.
4. **Run:** same command → PASS (old and new grid tests).
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx
   git commit -m "feat(frontend): stock grid column definitions with optional columns"
   ```

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
     // The fixed-layout table is exactly as wide as the column sum, so the sticky offsets match real widths.
     expect(screen.getByRole('table')).toHaveStyle({ width: '1428px', minWidth: '1428px' });
   });

   it('a read-only grid keeps the toolbar settings but has no Thêm dòng', () => {
     renderGrid({ lines: [line()], readOnly: true });
     expect(screen.queryByRole('button', { name: /Thêm dòng/ })).toBeNull();
     expect(document.querySelector('.stock-grid-toolbar')).not.toBeNull();
   });
   ```
2. **Run:** same file → FAIL (no `sticky-left` class, no table width, no toolbar in read-only).
3. **Minimal implementation.**
   - **Offsets.** Compute them once per render:
     ```ts
     // stickyOffset is the Map declared in Task 4.1; fill it here.
     let left = 0;
     for (const c of columns) if (c.sticky === 'left') { stickyOffset.set(c.key, { left }); left += c.width; }
     let right = 0;
     for (const c of [...columns].reverse()) if (c.sticky === 'right') { stickyOffset.set(c.key, { right }); right += c.width; }
     const stickyRight = right; // total width of the right-pinned columns
     ```
     Extend the Task 4.1 `cellClass` with `c.sticky === 'left' && 'sticky-left', c.sticky === 'right' && 'sticky-right'`. Every `th` and `td` already goes through `cellClass(c)` / `stickyOffset.get(c.key)` from Task 4.1, so nothing else changes in the cells.
   - **Layout.** The component root becomes `<div className="flex min-h-0 flex-1 flex-col">` (not `h-full`: the Phase 03 lines slot also holds the root-message paragraph, which must not push the grid's bottom edge out of the panel). The toolbar is **always rendered** (the settings popover and the keyboard guide are view preferences, not edits, so a read-only or cancelled voucher can still reveal Tiền CK, Tiền VAT, Ghi chú or Kho); only "Thêm dòng" is gated by `readOnly`:
     ```tsx
     <div className="stock-grid-toolbar flex shrink-0 flex-wrap items-center justify-between gap-2 border-b px-2 py-1.5">
       <div className="flex items-center gap-2">
         {!readOnly && (
           <button type="button" className="lib-btn" onClick={addLine}><Plus className="h-4 w-4 text-cyan-600" />Thêm dòng</button>
         )}
         {/* Task 4.3 adds <StockGridSettingsPopover … /> here */}
       </div>
       <div className="keyboard-guide">
         <span><span className="kbd">Enter</span> Tiếp</span>
         <span><span className="kbd">Shift</span>+<span className="kbd">Enter</span> Lùi</span>
         <span><span className="kbd">Ctrl</span>+<span className="kbd">S</span> Lưu</span>
         <span><span className="kbd">Insert</span> Thêm dòng</span>
       </div>
     </div>
     ```
     (Task 4.5 replaces the guide spans.)
     Then the scroll box: `<div className="accounting-grid-wrap stock-grid-wrap min-h-0 flex-1" tabIndex={-1} onKeyDown={handleGridKeyDown} style={{ '--stock-grid-left': `${left}px`, '--stock-grid-right': `${stickyRight}px` } as CSSProperties}>`, with `<table className="accounting-grid stock-grid" style={{ width: minWidth, minWidth }}>`. The table width must equal the column sum: `.accounting-grid` is `width: 100%; table-layout: fixed`, so without an explicit width the browser spreads the surplus over the columns and the sticky offsets drift off the real column edges. Remove the old footer and toolbar blocks below the table (the `.line-items-footer` / `.line-items-toolbar` wrappers go away, which is why the toolbar gets its own scoped CSS below).
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
       height: 36px; /* fixed so scroll-margin-top below is exact */
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

     /* The pinned # cell keeps its muted tint (same specificity as .accounting-grid td.row-no, so restate it). */
     .stock-grid td.row-no.sticky-left {
       background: hsl(var(--muted));
     }

     /* Disabled cells: the shared grid hard-codes a light grey that glares in dark mode. */
     .stock-grid .cell-input:disabled {
       background: hsl(var(--muted));
     }

     /* A focused cell scrolls into view clear of the sticky header and of the pinned columns.
      * Cells inside a pinned column are always visible, so only the scrolling cells get side margins. */
     .stock-grid .cell-input {
       scroll-margin-top: 36px;
     }

     .stock-grid td:not(.sticky-left):not(.sticky-right) .cell-input {
       scroll-margin-left: var(--stock-grid-left, 0px);
       scroll-margin-right: var(--stock-grid-right, 0px);
     }

     /* Toolbar: the shared .line-items-toolbar / .line-items-footer wrappers no longer exist, so restate the
      * button and key-cap look under .stock-grid-toolbar. */
     .stock-grid-toolbar .lib-btn {
       height: 30px;
       border: 1px solid hsl(var(--border));
       border-radius: 4px;
       background: hsl(var(--card));
       padding: 0 10px;
       cursor: pointer;
       font-family: inherit;
       font-size: 13px;
       color: hsl(var(--foreground));
       display: inline-flex;
       align-items: center;
       gap: 6px;
       transition: border-color .15s, color .15s, background .15s;
     }

     .stock-grid-toolbar .lib-btn:hover,
     .stock-grid-toolbar .lib-btn:focus-visible {
       border-color: hsl(var(--ring));
       color: hsl(var(--primary));
       background: hsl(var(--accent) / 0.5);
       outline: none;
     }

     .stock-grid-toolbar .lib-btn:disabled {
       opacity: 0.5;
       cursor: not-allowed;
     }

     .stock-grid-toolbar .keyboard-guide {
       display: flex;
       flex-wrap: wrap;
       gap: 10px;
       align-items: center;
       font-size: 12px;
       color: hsl(var(--muted-foreground));
     }

     .stock-grid-toolbar .keyboard-guide .kbd {
       min-width: 18px;
       padding: 1px 5px;
       border: 1px solid hsl(var(--border));
       border-bottom-width: 2px;
       border-radius: 3px;
       background: hsl(var(--card));
       color: hsl(var(--foreground));
       font-size: 11px;
       font-family: Consolas, monospace;
       margin: 0 2px;
     }
     ```
     Import the file in `stock-line-grid.tsx` **after** `line-items-grid.css` (the equal-specificity rules above rely on that order).
   - **Tabs.** In `stock-voucher-tabs.tsx`, change the lines `TabsContent` class (Phase 03 Task 3.4: `${PANEL} min-h-[280px] flex-1 overflow-auto rounded-b-md border border-t-0 bg-card`) to `${PANEL} min-h-[280px] flex flex-1 flex-col overflow-hidden rounded-b-md border border-t-0 bg-card`, so the grid's own box scrolls instead of the tab. Keep whatever `min-h-*` Phase 03 settled on. `PANEL` carries `data-[state=inactive]:hidden`; Tailwind emits variant rules after plain utilities, so the inactive Hàng hóa panel still hides despite `flex` (confirm in the browser, see Verification).
4. **Run:** `npx vitest run src/pages/stock-vouchers` → PASS.
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.css frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.tsx
   git commit -m "feat(frontend): sticky stock grid columns and full-height scroll"
   ```

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
2. **Run:** `npx vitest run src/pages/stock-vouchers/components` → FAIL: the popover test cannot import the module, and the new grid tests find no "Cột & phím Enter" button.
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
                 <label key={c} className="flex items-center gap-2">
                   <input type="checkbox" aria-label={COLUMN_LABELS[c]} checked={!prefs.hiddenColumns.includes(c)} onChange={() => onToggleColumn(c)} />
                   {COLUMN_LABELS[c]}
                   {c === 'warehouse' && <span className="text-xs text-muted-foreground">(tự hiện khi có dòng khác kho phiếu)</span>}
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
   Check that `Settings2` exists in the installed `lucide-react`. If it does not, use `Settings`. The Kho note is visible text (touch and keyboard users never see a `title`); the `aria-label` keeps the checkbox name `Kho`. Then wire it into the grid toolbar (Task 4.2 placeholder; the toolbar is rendered in read-only too) and delete the `void toggleColumn; …` line from Task 4.1:
   ```tsx
   <StockGridSettingsPopover prefs={prefs} onToggleColumn={toggleColumn} onToggleEnterStop={toggleEnterStop} onReset={reset} />
   ```
   Also add a grid test:
   ```ts
   // Assert on the grid itself: the popover's own matrix table also has a "Ghi chú" columnheader,
   // so headers() would pass before the click.
   it('the settings popover shows a hidden column at once and stores the choice', () => {
     renderGrid({ lines: [line()] });
     expect(document.getElementById('stock-line-note-0')).toBeNull();
     fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));
     fireEvent.click(screen.getByRole('checkbox', { name: 'Ghi chú' }));
     expect(document.getElementById('stock-line-note-0')).not.toBeNull();
     const stored = JSON.parse(localStorage.getItem(stockGridPrefsKey('', 'In'))!);
     expect(stored.hiddenColumns).not.toContain('note');
   });

   it('a read-only grid can still reveal a hidden column from the popover', () => {
     renderGrid({ lines: [line()], readOnly: true });
     expect(document.getElementById('stock-line-note-0')).toBeNull();
     fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));
     fireEvent.click(screen.getByRole('checkbox', { name: 'Ghi chú' }));
     expect(document.getElementById('stock-line-note-0')).not.toBeNull();
   });
   ```
4. **Run:** `npx vitest run src/pages/stock-vouchers/components` → PASS.
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.test.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx
   git commit -m "feat(frontend): stock grid column and enter-stop settings"
   ```

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
     showAllColumns(); // Kho visible: the old code would move to the Kho select, so this test fails in RED
     renderGrid({ lines: [createEmptyStockLine(0, WH1)] });
     byId('stock-line-product-code-0').focus();
     enter('stock-line-product-code-0');
     expect(focused()).toBe('stock-line-product-code-0');
     expect(document.getElementById('stock-line-product-code-1')).toBeNull();
   });
   ```
   `line()` sets `productId: P1`, so those rows count as having a product.
2. **Run:** grid test file → FAIL, all four new tests (a pick focuses `stock-line-warehouse-0`; Enter on an empty code moves to the Kho select).
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
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx
   git commit -m "feat(frontend): stock grid enter path follows the pricing mode"
   ```

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

   // Programmatic focus on a filled Mã cell must not open the suggestion list: an open list would swallow
   // ↑/↓ (list highlight) and Enter (re-pick item 0, resetting quantity and price).
   it('focusing a filled Mã does not open the list; ArrowDown moves rows and keeps the quantity', () => {
     const { values } = renderGrid({ lines: [line({ quantity: 5 }), line({ _uiKey: 'b' })] });
     // If the harness's product-search mock returns nothing for the line's code, make it return the P1
     // suggestion for that code in this test, so the list would open without the fix.
     act(() => byId('stock-line-product-code-0').focus());
     expect(screen.queryByRole('listbox')).toBeNull();
     fireEvent.keyDown(byId('stock-line-product-code-0'), { key: 'ArrowDown' });
     expect(focused()).toBe('stock-line-product-code-1');
     expect(values().lines[0].quantity).toBe(5);
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
2. **Run:** grid test file → FAIL (ArrowDown/Ctrl+Delete are unhandled, the guide lacks the new keys, and a focused filled Mã opens the list). `arrows leave the warehouse select alone` passes already (ArrowDown was not handled at all); it guards the `SELECT` exclusion once the arrow branch exists.
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
   `ProductTypeaheadCell` calls `preventDefault()` on ↑/↓ while its list is open with results, so the `defaultPrevented` guard leaves the suggestion list alone. Import `verticalTarget`.

   **No list on focus.** In `product-typeahead-cell.tsx` add the prop `/** Mở danh sách gợi ý khi ô nhận focus. Mặc định true (báo giá giữ nguyên). */ openOnFocus?: boolean;`, destructure it with `openOnFocus = true`, and change `onFocus` to `syncDropdownRect(); if (openOnFocus) setOpen(true);`. Typing (`onChange`) still opens the list. In the stock grid pass `openOnFocus={false}` to its `ProductTypeaheadCell`. Without this, focus arriving by ↑/↓, Shift+Enter or a new row lands on a filled Mã, opens the list, and the next Enter re-picks the first match and resets the line.

   Replace the keyboard guide content (the guide lives in the Task 4.2 toolbar):
   ```tsx
   <span><span className="kbd">Enter</span> Tiếp</span>
   <span><span className="kbd">Shift</span>+<span className="kbd">Enter</span> Lùi</span>
   <span><span className="kbd">↑</span><span className="kbd">↓</span> Dòng trên/dưới</span>
   <span><span className="kbd">Insert</span> Thêm dòng</span>
   <span><span className="kbd">Ctrl</span>+<span className="kbd">Del</span> Xóa dòng</span>
   <span><span className="kbd">Ctrl</span>+<span className="kbd">S</span> Lưu</span>
   <span><span className="kbd">Alt</span>+<span className="kbd">1…4</span> Chuyển tab</span>
   ```
   `toHaveTextContent('Ctrl+Del')` reads the concatenated text "Ctrl+Del". If whitespace between the spans breaks the match, use a regex such as `/Ctrl\s*\+\s*Del/` in the test. The quotation tests (run below) confirm the `openOnFocus` default leaves them unchanged.
4. **Run:**
   ```bash
   npx vitest run src/pages/stock-vouchers src/pages/quotations
   npm run typecheck
   npm run lint
   ```
   Expected: PASS; lint counts ≤ baseline.
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx frontend/src/pages/quotations/components/product-typeahead-cell.tsx
   git commit -m "feat(frontend): stock grid arrow keys and ctrl+delete"
   ```

### Task 4.6 — Close the phase

1. Set `**Status:** [x] complete` at the top of this file.
2. In `docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md` tick `- [x] Phase 04`.
3. **Commit:**
   ```bash
   git add docs/plans/261013-0622-stock-voucher-form-tabs/phase-04-grid.md docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md
   git commit -m "chore(plan): stock voucher form phase 04 complete"
   ```

## Verification

From `frontend/`:

- `npx vitest run src/pages/stock-vouchers` → PASS
- `npm run test` → FAIL lines == `BASELINE.md`
- `npm run typecheck` → PASS
- `npm run lint` → counts ≤ `BASELINE.md`
- `npm run build && git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` → PASS
- Browser check (jsdom cannot see layout; record the result in the report): the inactive Hàng hóa panel is hidden; the grid scrolls inside its tab with a sticky header; the sticky offsets line up with the real column edges at 1920px and 1366px; Tab into a middle column scrolls it clear of the left pinned columns, the right pinned columns (Số còn lại + delete) and the header; the toolbar button and the key-caps are styled; in dark mode the pinned cells and disabled cells are readable.

## Exit Criteria

- With default prefs the grid shows 16 columns (15 when read-only). The popover changes columns and Enter stops at once (also on a read-only voucher), and they persist per user and direction.
- Kho shows automatically when a line uses another warehouse; any column with a line error shows.
- The header and the #/Mã/Tên and Số còn lại/delete columns stick; the grid scrolls inside the Hàng hóa tab.
- Enter, Shift+Enter, ↑/↓, Ctrl+Delete and the pick-focus behave as tested.
- Focus on a Mã cell does not open the suggestion list (`openOnFocus={false}`), so ↑/↓ and Enter walk the grid; quotations keep the default.
- Known limit, to note in the report: once the user types in a Mã cell and its suggestion list is open with results, ↑/↓ move in the list, not between rows; Esc closes the list.
- A team chat summary is posted.
- Task 4.6 committed: this file's status is `[x] complete` and SUMMARY.md has Phase 04 ticked.
