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

// Số tấm for non-PerUnit; Dài for metre modes; Rộng for m² / m³; Dày for m³; SL only for PerUnit.
export function isFieldDisabled(mode: PricingMode, field: LineFocusField): boolean {
  switch (field) {
    case 'sheet-count':
    case 'length':
      return mode === 'PerUnit';
    case 'width':
      return mode !== 'PerSquareMeter' && mode !== 'PerCubicMeter';
    case 'thickness':
      return mode !== 'PerCubicMeter';
    case 'quantity':
      return mode !== 'PerUnit';
    default:
      return false;
  }
}

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
