# Phase 02 — Pure logic: line navigation, grid prefs, debt after voucher, error tabs

**Status:** [x] complete
**Complexity:** M

## Objective

Write the four logic units that the UI phases consume, each unit-tested in isolation, with no visible UI change:

1. **`line-navigation.ts`**: focus fields, the per-pricing-mode Enter path, previous/next stop and the ↑/↓ target. `isFieldDisabled` and `LINE_FOCUS_FIELDS` move here from the grid.
2. **`use-stock-grid-prefs.ts`**: optional columns, default prefs, a validated localStorage read/write per user and direction, the React hook, and `forcedColumns`.
3. **`debt-after-voucher.ts`**: the partner's receivable/payable after this voucher (brainstorm §3), and `savedImpactOf`, the stored impact of the voucher being edited.
4. **`voucher-tabs.ts`**: the form-field → tab mapping for error dots and for switching to the first tab with an error.

## Files

- `frontend/src/pages/stock-vouchers/utils/line-navigation.ts` (new)
- `frontend/src/pages/stock-vouchers/utils/line-navigation.test.ts` (new)
- `frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts` (new)
- `frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.test.ts` (new)
- `frontend/src/pages/stock-vouchers/utils/debt-after-voucher.ts` (new; `debtAfterVoucher` and `savedImpactOf`)
- `frontend/src/pages/stock-vouchers/utils/debt-after-voucher.test.ts` (new)
- `frontend/src/pages/stock-vouchers/utils/voucher-tabs.ts` (new)
- `frontend/src/pages/stock-vouchers/utils/voucher-tabs.test.ts` (new)
- `frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx` (modify: import moved symbols only)

## Tasks

### Task 2.1 — `line-navigation.ts`

1. **Write the failing test** in `line-navigation.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import {
     DEFAULT_ENTER_STOPS,
     enterPath,
     isFieldDisabled,
     nextStop,
     prevStop,
     verticalTarget,
     type LineFocusField,
   } from './line-navigation';

   const all = () => true;

   describe('enterPath', () => {
     it('follows the pricing mode with the default stops', () => {
       expect(enterPath('PerUnit', DEFAULT_ENTER_STOPS.PerUnit, all)).toEqual(['product-code', 'quantity', 'unit-price']);
       expect(enterPath('PerLinearMeter', DEFAULT_ENTER_STOPS.PerLinearMeter, all)).toEqual([
         'product-code', 'sheet-count', 'length', 'unit-price',
       ]);
       expect(enterPath('PerSquareMeter', DEFAULT_ENTER_STOPS.PerSquareMeter, all)).toEqual([
         'product-code', 'sheet-count', 'length', 'width', 'unit-price',
       ]);
       expect(enterPath('PerCubicMeter', DEFAULT_ENTER_STOPS.PerCubicMeter, all)).toEqual([
         'product-code', 'sheet-count', 'length', 'width', 'thickness', 'unit-price',
       ]);
     });

     it('keeps column order, drops fields disabled for the mode and hidden columns', () => {
       // Stops given out of order and with a field PerUnit cannot use.
       expect(enterPath('PerUnit', ['vat-rate', 'unit-price', 'length', 'quantity', 'warehouse'], all)).toEqual([
         'product-code', 'warehouse', 'quantity', 'unit-price', 'vat-rate',
       ]);
       const hideWarehouse = (f: LineFocusField) => f !== 'warehouse';
       expect(enterPath('PerUnit', ['warehouse', 'quantity'], hideWarehouse)).toEqual(['product-code', 'quantity']);
     });

     it('always starts with the product code, even with no stops', () => {
       expect(enterPath('PerSquareMeter', [], all)).toEqual(['product-code']);
     });
   });

   describe('nextStop / prevStop', () => {
     const path: LineFocusField[] = ['product-code', 'sheet-count', 'length', 'width', 'unit-price'];

     it('moves along the path and returns null past either end', () => {
       expect(nextStop(path, 'product-code')).toBe('sheet-count');
       expect(nextStop(path, 'unit-price')).toBeNull();
       expect(prevStop(path, 'sheet-count')).toBe('product-code');
       expect(prevStop(path, 'product-code')).toBeNull();
     });

     it('from a field outside the path, goes to the next/previous stop by column order', () => {
       // Reached by Tab: %CK sits after Đơn giá, so Enter leaves the row.
       expect(nextStop(path, 'discount-rate')).toBeNull();
       expect(prevStop(path, 'discount-rate')).toBe('unit-price');
       // Kho sits between Mã and Tấm.
       expect(nextStop(path, 'warehouse')).toBe('sheet-count');
     });
   });

   describe('verticalTarget', () => {
     it('keeps the column when the field is enabled and visible in the target row', () => {
       expect(verticalTarget('PerSquareMeter', 'width', all)).toBe('width');
     });

     it('falls back to the product code when the field is disabled or hidden there', () => {
       expect(verticalTarget('PerUnit', 'width', all)).toBe('product-code');
       expect(verticalTarget('PerUnit', 'note', (f) => f !== 'note')).toBe('product-code');
     });
   });

   describe('isFieldDisabled', () => {
     it('matches the pricing mode rules', () => {
       expect(isFieldDisabled('PerUnit', 'sheet-count')).toBe(true);
       expect(isFieldDisabled('PerUnit', 'quantity')).toBe(false);
       expect(isFieldDisabled('PerLinearMeter', 'width')).toBe(true);
       expect(isFieldDisabled('PerSquareMeter', 'thickness')).toBe(true);
       expect(isFieldDisabled('PerCubicMeter', 'thickness')).toBe(false);
       expect(isFieldDisabled('PerCubicMeter', 'quantity')).toBe(true);
     });
   });
   ```
2. **Run to verify it fails:** `npx vitest run src/pages/stock-vouchers/utils/line-navigation.test.ts` → FAIL, `Failed to resolve import "./line-navigation"`.
3. **Minimal implementation.** Create `line-navigation.ts`:
   ```ts
   import type { PricingMode } from '@/features/products/types';

   // Grid input fields in column order; cell ids are `stock-line-<field>-<row>`.
   export const LINE_FOCUS_FIELDS = [
     'product-code', 'warehouse', 'sheet-count', 'length', 'width', 'thickness',
     'quantity', 'unit-price', 'discount-rate', 'discount-amount', 'vat-rate', 'note',
   ] as const;

   export type LineFocusField = (typeof LINE_FOCUS_FIELDS)[number];
   // Every field but the product code, which is always the first stop.
   export type EnterStopField = Exclude<LineFocusField, 'product-code'>;

   export const ENTER_STOP_FIELDS = LINE_FOCUS_FIELDS.filter(
     (f): f is EnterStopField => f !== 'product-code',
   );

   export const PRICING_MODES: readonly PricingMode[] = ['PerUnit', 'PerLinearMeter', 'PerSquareMeter', 'PerCubicMeter'];

   // Enter stops after the product code, per pricing mode (brainstorm §2).
   export const DEFAULT_ENTER_STOPS: Record<PricingMode, readonly EnterStopField[]> = {
     PerUnit: ['quantity', 'unit-price'],
     PerLinearMeter: ['sheet-count', 'length', 'unit-price'],
     PerSquareMeter: ['sheet-count', 'length', 'width', 'unit-price'],
     PerCubicMeter: ['sheet-count', 'length', 'width', 'thickness', 'unit-price'],
   };

   const order = (f: LineFocusField) => LINE_FOCUS_FIELDS.indexOf(f);
   ```
   Move `isFieldDisabled` here verbatim from `stock-line-grid.tsx` (with its comment, exported, `field: LineFocusField`). Then add:
   ```ts
   // The cells Enter visits in one row: the product code, then the chosen stops the row's mode enables and
   // whose column is shown, in column order.
   export function enterPath(
     mode: PricingMode,
     stops: readonly EnterStopField[],
     isVisible: (field: LineFocusField) => boolean,
   ): LineFocusField[] {
     return [
       'product-code',
       ...ENTER_STOP_FIELDS.filter((f) => stops.includes(f) && !isFieldDisabled(mode, f) && isVisible(f)),
     ];
   }

   // Next stop after `field` by column order (works for a field reached by Tab); null leaves the row.
   export function nextStop(path: readonly LineFocusField[], field: LineFocusField): LineFocusField | null {
     return path.find((f) => order(f) > order(field)) ?? null;
   }

   export function prevStop(path: readonly LineFocusField[], field: LineFocusField): LineFocusField | null {
     return [...path].reverse().find((f) => order(f) < order(field)) ?? null;
   }

   // ↑/↓ keep the column unless the target row cannot use it.
   export function verticalTarget(
     mode: PricingMode,
     field: LineFocusField,
     isVisible: (field: LineFocusField) => boolean,
   ): LineFocusField {
     return !isFieldDisabled(mode, field) && isVisible(field) ? field : 'product-code';
   }
   ```
   In `stock-line-grid.tsx`, delete the local `LINE_FOCUS_FIELDS`, `LineFocusField` and `isFieldDisabled`, and import them from `@/pages/stock-vouchers/utils/line-navigation`. Grid behavior is unchanged.
4. **Run tests to verify they pass:** `npx vitest run src/pages/stock-vouchers/utils/line-navigation.test.ts src/pages/stock-vouchers/components/stock-line-grid.test.tsx` → PASS.
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/utils/line-navigation.ts \
     frontend/src/pages/stock-vouchers/utils/line-navigation.test.ts \
     frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx
   git commit -m "feat(frontend): per-pricing-mode enter path helpers"
   ```

### Task 2.2 — `use-stock-grid-prefs.ts`

1. **Write the failing test** in `use-stock-grid-prefs.test.ts`:
   ```ts
   import { beforeEach, describe, expect, it, vi } from 'vitest';
   import { act, renderHook } from '@testing-library/react';
   import {
     DEFAULT_STOCK_GRID_PREFS,
     forcedColumns,
     readStockGridPrefs,
     stockGridPrefsKey,
     useStockGridPrefs,
   } from './use-stock-grid-prefs';

   describe('readStockGridPrefs', () => {
     beforeEach(() => localStorage.clear());

     it('returns the defaults when nothing is stored', () => {
       expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);
       expect(DEFAULT_STOCK_GRID_PREFS.hiddenColumns).toEqual(['warehouse', 'discount-amount', 'vat-amount', 'note']);
     });

     it('keys by user and direction', () => {
       expect(stockGridPrefsKey('u1', 'Out')).toBe('qldh:stock-grid-prefs:u1:Out');
       localStorage.setItem(
         stockGridPrefsKey('u1', 'Out'),
         JSON.stringify({ version: 1, hiddenColumns: ['note'], enterStops: {} }),
       );
       expect(readStockGridPrefs('u1', 'Out').hiddenColumns).toEqual(['note']);
       expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);
       expect(readStockGridPrefs('u2', 'Out')).toEqual(DEFAULT_STOCK_GRID_PREFS);
     });

     it('falls back per part on corrupt, old or foreign data', () => {
       localStorage.setItem(stockGridPrefsKey('u1', 'In'), '{not json');
       expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);

       localStorage.setItem(stockGridPrefsKey('u1', 'In'), JSON.stringify({ version: 0, hiddenColumns: [] }));
       expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);

       localStorage.setItem(
         stockGridPrefsKey('u1', 'In'),
         JSON.stringify({
           version: 1,
           hiddenColumns: ['note', 'bogus', 'note'],
           // Unknown field and a field PerUnit cannot use are dropped; order follows the columns.
           enterStops: { PerUnit: ['unit-price', 'bogus', 'width', 'vat-rate'], PerSquareMeter: 'x' },
         }),
       );
       const prefs = readStockGridPrefs('u1', 'In');
       expect(prefs.hiddenColumns).toEqual(['note']);
       expect(prefs.enterStops.PerUnit).toEqual(['unit-price', 'vat-rate']);
       expect(prefs.enterStops.PerSquareMeter).toEqual(DEFAULT_STOCK_GRID_PREFS.enterStops.PerSquareMeter);
     });

     it('puts stored stops in column order and falls back to the defaults for a non-array hiddenColumns', () => {
       localStorage.setItem(
         stockGridPrefsKey('u1', 'In'),
         JSON.stringify({ version: 1, hiddenColumns: 'x', enterStops: { PerSquareMeter: ['unit-price', 'sheet-count'] } }),
       );
       const prefs = readStockGridPrefs('u1', 'In');
       expect(prefs.enterStops.PerSquareMeter).toEqual(['sheet-count', 'unit-price']);
       expect(prefs.hiddenColumns).toEqual(DEFAULT_STOCK_GRID_PREFS.hiddenColumns);
     });

     it('survives a throwing localStorage', () => {
       const spy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
         throw new Error('blocked');
       });
       expect(readStockGridPrefs('u1', 'In')).toEqual(DEFAULT_STOCK_GRID_PREFS);
       spy.mockRestore();
     });
   });

   describe('useStockGridPrefs', () => {
     beforeEach(() => localStorage.clear());

     it('toggles a column and an enter stop, persists them, and resets', () => {
       const { result } = renderHook(() => useStockGridPrefs('u1', 'In'));

       act(() => result.current.toggleColumn('note'));
       expect(result.current.prefs.hiddenColumns).not.toContain('note');
       act(() => result.current.toggleEnterStop('PerUnit', 'vat-rate'));
       expect(result.current.prefs.enterStops.PerUnit).toEqual(['quantity', 'unit-price', 'vat-rate']);
       // A field the mode cannot use is ignored.
       act(() => result.current.toggleEnterStop('PerUnit', 'width'));
       expect(result.current.prefs.enterStops.PerUnit).toEqual(['quantity', 'unit-price', 'vat-rate']);
       expect(readStockGridPrefs('u1', 'In')).toEqual(result.current.prefs);

       act(() => result.current.reset());
       expect(result.current.prefs).toEqual(DEFAULT_STOCK_GRID_PREFS);
       expect(localStorage.getItem(stockGridPrefsKey('u1', 'In'))).toBeNull();
     });

     it('inserts a toggled stop and a re-shown column in column order, not at the end', () => {
       const { result } = renderHook(() => useStockGridPrefs('u1', 'In'));
       act(() => result.current.toggleEnterStop('PerUnit', 'warehouse'));
       expect(result.current.prefs.enterStops.PerUnit).toEqual(['warehouse', 'quantity', 'unit-price']);
       // Show Kho, then hide it again: the stored list keeps the column order of OPTIONAL_COLUMNS.
       act(() => result.current.toggleColumn('warehouse'));
       act(() => result.current.toggleColumn('warehouse'));
       expect(result.current.prefs.hiddenColumns).toEqual(['warehouse', 'discount-amount', 'vat-amount', 'note']);
     });

     it('keeps working when localStorage cannot be written', () => {
       const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
         throw new Error('quota');
       });
       const { result } = renderHook(() => useStockGridPrefs('u1', 'In'));
       expect(() => act(() => result.current.toggleColumn('note'))).not.toThrow();
       // The change still applies for this page.
       expect(result.current.prefs.hiddenColumns).not.toContain('note');
       spy.mockRestore();
     });
   });

   describe('forcedColumns', () => {
     it('shows Kho when a line uses another warehouse, and any column with a line error', () => {
       const lines = [{ warehouseId: 'w1' }, { warehouseId: 'w2' }, { warehouseId: '' }];
       expect([...forcedColumns(lines, 'w1', undefined)]).toEqual(['warehouse']);
       expect([...forcedColumns([{ warehouseId: 'w1' }], 'w1', undefined)]).toEqual([]);
       // A line without a warehouse yet is not "another warehouse".
       expect([...forcedColumns([{ warehouseId: '' }], 'w1', undefined)]).toEqual([]);
       const errors = [undefined, { note: { message: 'x' }, discountAmount: { message: 'y' }, quantity: { message: 'z' } }];
       expect([...forcedColumns([{ warehouseId: 'w1' }], 'w1', errors)].sort()).toEqual(['discount-amount', 'note']);
     });

     it('ignores an array-level error, which react-hook-form gives as an object, not an array', () => {
       // Empty grid submitted: zod `min(1)` sets errors.lines to { root: ... } or { message, type }.
       expect([...forcedColumns([], 'w1', { root: { message: 'x' } })]).toEqual([]);
       expect([...forcedColumns([], 'w1', { message: 'x', type: 'too_small' })]).toEqual([]);
     });
   });
   ```
2. **Run to verify it fails:** `npx vitest run src/pages/stock-vouchers/utils/use-stock-grid-prefs.test.ts` → FAIL, module not found.
3. **Minimal implementation.** Create `use-stock-grid-prefs.ts`:
   ```ts
   import { useCallback, useState } from 'react';
   import type { PricingMode } from '@/features/products/types';
   import type { StockDirection } from '@/features/stock-reasons/types';
   import {
     DEFAULT_ENTER_STOPS,
     ENTER_STOP_FIELDS,
     PRICING_MODES,
     isFieldDisabled,
     type EnterStopField,
   } from './line-navigation';

   // Columns the user may hide (brainstorm §2); the rest are always shown.
   export const OPTIONAL_COLUMNS = [
     'warehouse', 'stock-at', 'amount', 'discount-rate', 'discount-amount', 'vat-rate', 'vat-amount', 'note',
   ] as const;
   export type OptionalColumn = (typeof OPTIONAL_COLUMNS)[number];

   export interface StockGridPrefs {
     hiddenColumns: OptionalColumn[];
     enterStops: Record<PricingMode, EnterStopField[]>;
   }

   const VERSION = 1;

   export const DEFAULT_STOCK_GRID_PREFS: StockGridPrefs = {
     hiddenColumns: ['warehouse', 'discount-amount', 'vat-amount', 'note'],
     enterStops: {
       PerUnit: [...DEFAULT_ENTER_STOPS.PerUnit],
       PerLinearMeter: [...DEFAULT_ENTER_STOPS.PerLinearMeter],
       PerSquareMeter: [...DEFAULT_ENTER_STOPS.PerSquareMeter],
       PerCubicMeter: [...DEFAULT_ENTER_STOPS.PerCubicMeter],
     },
   };

   export function stockGridPrefsKey(userId: string, type: StockDirection): string {
     // Colon style, like `qldh:col-sizes:<table>` in lib/use-column-sizing-persist.ts.
     return `qldh:stock-grid-prefs:${userId}:${type}`;
   }

   function cleanStops(mode: PricingMode, value: unknown): EnterStopField[] {
     if (!Array.isArray(value)) return [...DEFAULT_ENTER_STOPS[mode]];
     return ENTER_STOP_FIELDS.filter((f) => value.includes(f) && !isFieldDisabled(mode, f));
   }

   // Corrupt, old or foreign data falls back to the defaults part by part.
   export function readStockGridPrefs(userId: string, type: StockDirection): StockGridPrefs {
     try {
       const raw = localStorage.getItem(stockGridPrefsKey(userId, type));
       if (!raw) return DEFAULT_STOCK_GRID_PREFS;
       const parsed = JSON.parse(raw) as { version?: unknown; hiddenColumns?: unknown; enterStops?: unknown };
       if (parsed?.version !== VERSION) return DEFAULT_STOCK_GRID_PREFS;
       const hidden = Array.isArray(parsed.hiddenColumns)
         ? OPTIONAL_COLUMNS.filter((c) => (parsed.hiddenColumns as unknown[]).includes(c))
         : DEFAULT_STOCK_GRID_PREFS.hiddenColumns;
       const stops = (parsed.enterStops ?? {}) as Record<string, unknown>;
       return {
         hiddenColumns: [...hidden],
         enterStops: Object.fromEntries(PRICING_MODES.map((m) => [m, cleanStops(m, stops[m])])) as StockGridPrefs['enterStops'],
       };
     } catch {
       return DEFAULT_STOCK_GRID_PREFS;
     }
   }

   function write(userId: string, type: StockDirection, prefs: StockGridPrefs | null) {
     try {
       const key = stockGridPrefsKey(userId, type);
       if (prefs) localStorage.setItem(key, JSON.stringify({ version: VERSION, ...prefs }));
       else localStorage.removeItem(key);
     } catch {
       // Storage blocked: the change still applies for this page.
     }
   }

   export function useStockGridPrefs(userId: string, type: StockDirection) {
     const [prefs, setPrefs] = useState(() => readStockGridPrefs(userId, type));

     const update = useCallback(
       (next: StockGridPrefs) => {
         setPrefs(next);
         write(userId, type, next);
       },
       [userId, type],
     );

     const toggleColumn = (column: OptionalColumn) => {
       const hidden = prefs.hiddenColumns.includes(column)
         ? prefs.hiddenColumns.filter((c) => c !== column)
         : OPTIONAL_COLUMNS.filter((c) => c === column || prefs.hiddenColumns.includes(c));
       update({ ...prefs, hiddenColumns: [...hidden] });
     };

     const toggleEnterStop = (mode: PricingMode, field: EnterStopField) => {
       if (isFieldDisabled(mode, field)) return;
       const current = prefs.enterStops[mode];
       const next = current.includes(field)
         ? current.filter((f) => f !== field)
         : ENTER_STOP_FIELDS.filter((f) => f === field || current.includes(f));
       update({ ...prefs, enterStops: { ...prefs.enterStops, [mode]: next } });
     };

     const reset = () => {
       setPrefs(DEFAULT_STOCK_GRID_PREFS);
       write(userId, type, null);
     };

     return { prefs, toggleColumn, toggleEnterStop, reset };
   }

   // Line form fields whose error forces their optional column into view.
   const ERROR_COLUMNS: Partial<Record<string, OptionalColumn>> = {
     warehouseId: 'warehouse',
     discountRate: 'discount-rate',
     discountAmount: 'discount-amount',
     vatRate: 'vat-rate',
     note: 'note',
   };

   // Hidden columns that must still show: Kho when a line leaves the header warehouse, and any column with an error.
   export function forcedColumns(
     lines: readonly { warehouseId?: string }[],
     headerWarehouseId: string,
     // `form.formState.errors.lines`: an array of per-line errors, but an object when the array itself is
     // rejected (for example no line), so it is checked here instead of trusting the type.
     lineErrors: unknown,
   ): Set<OptionalColumn> {
     const forced = new Set<OptionalColumn>();
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
   `readStockGridPrefs` returns `DEFAULT_STOCK_GRID_PREFS` itself only when nothing valid is stored. The hook never mutates `prefs`; it always builds new arrays. Invalid entries inside a valid list are dropped and the rest is kept (see the test "falls back per part"); a whole part of the wrong type falls back to its default. This refines the brainstorm's "that part uses the defaults".
4. **Run tests to verify they pass:** `npx vitest run src/pages/stock-vouchers/utils/use-stock-grid-prefs.test.ts` → PASS.
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.ts \
     frontend/src/pages/stock-vouchers/utils/use-stock-grid-prefs.test.ts
   git commit -m "feat(frontend): per-user stock grid preferences"
   ```

### Task 2.3 — `debt-after-voucher.ts`

1. **Write the failing test** in `debt-after-voucher.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { debtAfterVoucher, savedImpactOf } from './debt-after-voucher';

   const summary = { receivableBalance: 7_000_000, payableBalance: 500_000, creditLimit: 10_000_000 };

   describe('debtAfterVoucher', () => {
     it('ReceivableIncrease adds the unpaid part to the receivable', () => {
       expect(debtAfterVoucher(summary, { effect: 'ReceivableIncrease', total: 4_000_000, paid: 1_000_000 })).toEqual({
         receivableAfter: 10_000_000,
         payableAfter: 500_000,
         availableAfter: 0,
         receivableChanged: true,
         payableChanged: false,
       });
     });

     it('ReceivableDecrease, PayableIncrease and PayableDecrease move their side', () => {
       expect(debtAfterVoucher(summary, { effect: 'ReceivableDecrease', total: 2_000_000, paid: 0 }).receivableAfter).toBe(5_000_000);
       const buy = debtAfterVoucher(summary, { effect: 'PayableIncrease', total: 3_000_000, paid: 1_000_000 });
       expect(buy).toMatchObject({ receivableAfter: 7_000_000, payableAfter: 2_500_000, receivableChanged: false, payableChanged: true });
       expect(debtAfterVoucher(summary, { effect: 'PayableDecrease', total: 200_000, paid: 0 }).payableAfter).toBe(300_000);
     });

     it('None changes nothing', () => {
       expect(debtAfterVoucher(summary, { effect: 'None', total: 9_000_000, paid: 0 })).toMatchObject({
         receivableAfter: 7_000_000,
         payableAfter: 500_000,
         receivableChanged: false,
         payableChanged: false,
       });
     });

     it('an edited voucher replaces its saved impact, which the balance already holds', () => {
       const saved = { effect: 'ReceivableIncrease' as const, total: 4_000_000, paid: 0 };
       expect(debtAfterVoucher(summary, { effect: 'ReceivableIncrease', total: 5_000_000, paid: 0 }, saved).receivableAfter).toBe(8_000_000);
       // Reason changed to one without debt: the saved impact goes away.
       const none = debtAfterVoucher(summary, { effect: 'None', total: 5_000_000, paid: 0 }, saved);
       expect(none.receivableAfter).toBe(3_000_000);
       expect(none.receivableChanged).toBe(true);
     });

     it('an edited purchase subtracts the saved payable impact, including its paid part', () => {
       const saved = { effect: 'PayableIncrease' as const, total: 3_000_000, paid: 1_000_000 };
       const r = debtAfterVoucher(summary, { effect: 'PayableIncrease', total: 4_000_000, paid: 1_000_000 }, saved);
       // 500.000 + 3.000.000 (new net) - 2.000.000 (saved net)
       expect(r.payableAfter).toBe(1_500_000);
       expect(r.payableChanged).toBe(true);
       // Reason changed to one without debt: the saved payable impact goes away and the side still counts as changed.
       const none = debtAfterVoucher(summary, { effect: 'None', total: 0, paid: 0 }, saved);
       expect(none.payableAfter).toBe(-1_500_000);
       expect(none.payableChanged).toBe(true);
     });

     it('a saved paid amount reduces the saved impact that is taken back', () => {
       const saved = { effect: 'ReceivableIncrease' as const, total: 4_000_000, paid: 1_000_000 };
       // Saved net 3.000.000 is taken out of 7.000.000 when the reason loses its debt effect.
       expect(debtAfterVoucher(summary, { effect: 'None', total: 0, paid: 0 }, saved).receivableAfter).toBe(4_000_000);
     });

     it('no credit limit gives no available amount', () => {
       const r = debtAfterVoucher({ ...summary, creditLimit: null }, { effect: 'ReceivableIncrease', total: 1, paid: 0 });
       expect(r.availableAfter).toBeNull();
     });
   });

   describe('savedImpactOf', () => {
     const voucher = {
       status: 'Active',
       partnerId: 'p1',
       reasonDebtEffect: 'ReceivableIncrease',
       total: 4_000_000,
       paidAmount: 500_000,
     } as const;

     it('gives the stored impact of an Active voucher whose partner is still selected', () => {
       expect(savedImpactOf(voucher, 'p1')).toEqual({ effect: 'ReceivableIncrease', total: 4_000_000, paid: 500_000 });
     });

     it('gives nothing for a new voucher, another partner, a cancelled voucher or no partner', () => {
       expect(savedImpactOf(undefined, 'p1')).toBeUndefined();
       // Partner changed while editing: the other partner's balance never held this voucher.
       expect(savedImpactOf(voucher, 'p2')).toBeUndefined();
       expect(savedImpactOf({ ...voucher, status: 'Cancelled' }, 'p1')).toBeUndefined();
       expect(savedImpactOf(voucher, undefined)).toBeUndefined();
     });
   });
   ```
2. **Run to verify it fails:** `npx vitest run src/pages/stock-vouchers/utils/debt-after-voucher.test.ts` → FAIL, module not found.
3. **Minimal implementation.** Create `debt-after-voucher.ts`:
   ```ts
   import type { DebtEffect } from '@/features/cash-common/types';

   export interface VoucherDebtImpact {
     effect: DebtEffect;
     total: number;
     paid: number;
   }

   export interface DebtAfterVoucher {
     receivableAfter: number;
     payableAfter: number;
     // creditLimit − receivableAfter; null without a limit.
     availableAfter: number | null;
     receivableChanged: boolean;
     payableChanged: boolean;
   }

   interface Balances {
     receivableBalance: number;
     payableBalance: number;
     creditLimit?: number | null;
   }

   // The unpaid part moves the debt; the paid part goes through the automatic cash voucher.
   function delta({ effect, total, paid }: VoucherDebtImpact) {
     const net = total - paid;
     return {
       receivable: effect === 'ReceivableIncrease' ? net : effect === 'ReceivableDecrease' ? -net : 0,
       payable: effect === 'PayableIncrease' ? net : effect === 'PayableDecrease' ? -net : 0,
     };
   }

   const touchesReceivable = (e?: DebtEffect) => e === 'ReceivableIncrease' || e === 'ReceivableDecrease';
   const touchesPayable = (e?: DebtEffect) => e === 'PayableIncrease' || e === 'PayableDecrease';

   // `saved`: the stored impact of the voucher being edited, already inside the server balances.
   export function debtAfterVoucher(
     balances: Balances,
     current: VoucherDebtImpact,
     saved?: VoucherDebtImpact,
   ): DebtAfterVoucher {
     const now = delta(current);
     const before = saved ? delta(saved) : { receivable: 0, payable: 0 };
     const receivableAfter = balances.receivableBalance + now.receivable - before.receivable;
     return {
       receivableAfter,
       payableAfter: balances.payableBalance + now.payable - before.payable,
       availableAfter: balances.creditLimit == null ? null : balances.creditLimit - receivableAfter,
       receivableChanged: touchesReceivable(current.effect) || touchesReceivable(saved?.effect),
       payableChanged: touchesPayable(current.effect) || touchesPayable(saved?.effect),
     };
   }

   interface SavedVoucher {
     status: string;
     partnerId?: string | null;
     reasonDebtEffect: DebtEffect;
     total: number;
     paidAmount: number;
   }

   // The impact the server balance already holds: only an Active voucher counts, and only for its own partner.
   export function savedImpactOf(
     voucher: SavedVoucher | undefined,
     selectedPartnerId: string | undefined,
   ): VoucherDebtImpact | undefined {
     if (!voucher || voucher.status !== 'Active' || !selectedPartnerId || voucher.partnerId !== selectedPartnerId) {
       return undefined;
     }
     return { effect: voucher.reasonDebtEffect, total: voucher.total, paid: voucher.paidAmount };
   }
   ```
   The form page (Phase 03 Task 3.6) calls `savedImpactOf(voucher, selectedPartner?.id)` instead of an inline condition.
4. **Run tests to verify they pass:** same command → PASS.
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/utils/debt-after-voucher.ts \
     frontend/src/pages/stock-vouchers/utils/debt-after-voucher.test.ts
   git commit -m "feat(frontend): partner debt after the voucher"
   ```

### Task 2.4 — `voucher-tabs.ts`

1. **Write the failing test** in `voucher-tabs.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { errorTabs, firstErrorTab, tabOfField } from './voucher-tabs';

   describe('voucher tabs', () => {
     it('maps form and server keys to their tab; main-area fields map to null', () => {
       expect(tabOfField('lines')).toBe('lines');
       expect(tabOfField('lines[3].width')).toBe('lines');
       expect(tabOfField('lines.3.width')).toBe('lines');
       expect(tabOfField('note')).toBe('other');
       expect(tabOfField('partnerTaxCode')).toBe('other');
       expect(tabOfField('handlerName')).toBe('other');
       expect(tabOfField('paidAmount')).toBe('payment');
       expect(tabOfField('fundId')).toBe('payment');
       expect(tabOfField('dueDate')).toBe('payment');
       expect(tabOfField('freight')).toBe('payment');
       expect(tabOfField('orderDiscount')).toBe('payment');
       expect(tabOfField('reasonId')).toBeNull();
       expect(tabOfField('partnerAddress')).toBeNull();
     });

     it('collects the tabs with errors and picks the first in tab order', () => {
       expect([...errorTabs(['fundId', 'note', 'reasonId'])].sort()).toEqual(['other', 'payment']);
       expect(firstErrorTab(['fundId', 'note'])).toBe('other');
       expect(firstErrorTab(['paidAmount', 'lines[0].quantity'])).toBe('lines');
       expect(firstErrorTab(['reasonId'])).toBeNull();
     });
   });
   ```
2. **Run to verify it fails:** `npx vitest run src/pages/stock-vouchers/utils/voucher-tabs.test.ts` → FAIL, module not found.
3. **Minimal implementation.** Create `voucher-tabs.ts`:
   ```ts
   export type StockVoucherTab = 'lines' | 'other' | 'payment' | 'history';
   export type ErrorTab = Exclude<StockVoucherTab, 'history'>;

   const TAB_ORDER: readonly ErrorTab[] = ['lines', 'other', 'payment'];

   const FIELD_TABS: Record<string, ErrorTab> = {
     lines: 'lines',
     partnerTaxCode: 'other',
     handlerName: 'other',
     note: 'other',
     freight: 'payment',
     orderDiscount: 'payment',
     paidAmount: 'payment',
     fundId: 'payment',
     dueDate: 'payment',
   };

   // A form key (`note`), a nested path (`lines.3.width`) or a server key (`lines[3].width`); null = main area.
   export function tabOfField(key: string): ErrorTab | null {
     return FIELD_TABS[key.split(/[.[]/)[0]] ?? null;
   }

   export function errorTabs(keys: Iterable<string>): Set<ErrorTab> {
     const tabs = new Set<ErrorTab>();
     for (const key of keys) {
       const tab = tabOfField(key);
       if (tab) tabs.add(tab);
     }
     return tabs;
   }

   export function firstErrorTab(keys: Iterable<string>): ErrorTab | null {
     const tabs = errorTabs(keys);
     return TAB_ORDER.find((t) => tabs.has(t)) ?? null;
   }
   ```
4. **Run tests to verify they pass:** same command → PASS.
5. **Commit:**
   ```bash
   git add frontend/src/pages/stock-vouchers/utils/voucher-tabs.ts \
     frontend/src/pages/stock-vouchers/utils/voucher-tabs.test.ts
   git commit -m "feat(frontend): stock voucher field to tab mapping"
   ```

### Task 2.5 — Close the phase

1. Run the Verification commands below. All must pass.
2. In this file, change `**Status:** [ ] pending` to `**Status:** [x] complete`. In `SUMMARY.md`, tick `- [x] Phase 02`.
3. Commit:
   ```bash
   git add docs/plans/261013-0622-stock-voucher-form-tabs/phase-02-pure-logic.md \
     docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md
   git commit -m "chore(plan): stock voucher form phase 02 complete"
   ```

## Verification

From `frontend/`:

- `npx vitest run src/pages/stock-vouchers` → PASS
- `npm run typecheck` → PASS
- `npm run lint` → counts ≤ `BASELINE.md`

## Exit Criteria

- The four utils exist with their tests green (including `forcedColumns` with a non-array `lineErrors`, the throwing `setItem`, and `savedImpactOf`).
- `stock-line-grid.tsx` imports `LINE_FOCUS_FIELDS`, `LineFocusField` and `isFieldDisabled` from `line-navigation.ts`, with no behavior change.
- No visible UI change.
- The phase-complete commit exists (Task 2.5).
- A team chat summary is posted.
