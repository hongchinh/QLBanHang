// Enter order of the cash voucher form (section-01 §2.1 header, §2.2 line grid).

/** The ten header fields in Enter and DOM order. */
export const CASH_HEADER_FIELD_IDS: readonly string[] = [
  'cash-voucher-at',
  'cash-fund',
  'cash-partner',
  'cash-partner-name',
  'cash-reason',
  'cash-person-name',
  'cash-partner-address',
  'cash-description',
  'cash-attached-docs',
  'cash-voucher-note',
];

export type CashLineField = 'description' | 'amount' | 'note';

export type CashLineTarget =
  | { kind: 'cell'; field: CashLineField; index: number }
  | { kind: 'header-note' }
  | { kind: 'append' }
  | { kind: 'none' };

const FIELD_ORDER: readonly CashLineField[] = ['description', 'amount', 'note'];

/** Where Enter (or Shift+Enter) goes from a cell of the line grid. */
export function cashLineEnterTarget(
  field: CashLineField,
  index: number,
  lineCount: number,
  shift: boolean,
): CashLineTarget {
  if (index < 0 || index >= lineCount) return { kind: 'none' };
  const at = FIELD_ORDER.indexOf(field);
  if (shift) {
    if (at > 0) return { kind: 'cell', field: FIELD_ORDER[at - 1], index };
    return index === 0 ? { kind: 'header-note' } : { kind: 'cell', field: 'note', index: index - 1 };
  }
  if (at < FIELD_ORDER.length - 1) return { kind: 'cell', field: FIELD_ORDER[at + 1], index };
  return index === lineCount - 1 ? { kind: 'append' } : { kind: 'cell', field: 'description', index: index + 1 };
}
