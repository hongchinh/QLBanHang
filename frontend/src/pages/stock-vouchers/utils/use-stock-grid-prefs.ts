import { useCallback, useState } from 'react';
import type { PricingMode } from '@/features/products/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import { PRICING_MODES, type DimensionField } from '@/lib/dimension-columns';
import {
  DEFAULT_ENTER_STOPS,
  ENTER_STOP_FIELDS,
  isFieldDisabled,
  type EnterStopField,
} from './line-navigation';

// Columns the user may hide (brainstorm §2). Số tấm/Dài/Rộng/Dày follow the pricing-mode rule (`shownDimensions`);
// the rest are always shown.
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

// The line form fields that have an error on any line. `form.formState.errors.lines` is an array of per-line errors,
// but an object when the array itself is rejected (for example no line), so it is checked here instead of trusting
// the type.
function lineErrorKeys(lineErrors: unknown): string[] {
  if (!Array.isArray(lineErrors)) return [];
  return (lineErrors as (Record<string, unknown> | undefined)[]).flatMap((lineError) => Object.keys(lineError ?? {}));
}

// Hidden columns that must still show: Kho when a line leaves the header warehouse, and any column with an error.
export function forcedColumns(
  lines: readonly { warehouseId?: string }[],
  headerWarehouseId: string,
  // `form.formState.errors.lines`; `lineErrorKeys` handles the array-level error.
  lineErrors: unknown,
): Set<OptionalColumn> {
  const forced = new Set<OptionalColumn>();
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
