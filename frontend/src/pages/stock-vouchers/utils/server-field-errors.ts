import { toOptionalNumber } from '@/lib/to-number';

// Header fields that show their own server message.
export const HEADER_ERROR_FIELDS: ReadonlySet<string> = new Set([
  'fundId',
  'dueDate',
  'paidAmount',
  'partnerId',
  'paymentMethodId',
]);

// Line fields that take a server message, on their grid cell.
export const LINE_ERROR_FIELDS: ReadonlySet<string> = new Set([
  'productId',
  'warehouseId',
  'sheetCount',
  'length',
  'width',
  'thickness',
  'quantity',
  'unitPrice',
  'discountRate',
  'discountAmount',
  'vatRate',
  'note',
]);

// One line of the form, read by field name.
type LineValues = Readonly<Record<string, unknown>>;

// The form values the rules below read, as they are after the change.
export interface ServerErrorValues {
  paidAmount?: unknown;
  lines: readonly LineValues[];
}

// Header fields whose server error also goes away when a listed field changes (it is what drives them).
const HEADER_DRIVERS: Record<string, readonly string[]> = {
  fundId: ['paymentMethodId'],
  paidAmount: ['paymentMethodId'],
  dueDate: ['voucherAt', 'reasonId'],
  partnerId: ['reasonId'],
};

// The same inside one line, for a cell whose value is computed from other cells of that line.
// SL of a line priced by dimensions (read-only there) comes from Số tấm and the dimensions, in column order.
export const QUANTITY_SOURCES: readonly string[] = ['sheetCount', 'length', 'width', 'thickness'];
// Until it is typed by hand, Tiền CK is %CK of Số tiền (SL × Đơn giá).
const COMPUTED_DISCOUNT_SOURCES: readonly string[] = ['discountRate', 'unitPrice', 'quantity', ...QUANTITY_SOURCES];

// Fields of a line whose value is no longer the one the server saw once `field` of that line changed.
function staleLineFields(field: string, line: LineValues | undefined): string[] {
  const stale = [field];
  if (QUANTITY_SOURCES.includes(field)) stale.push('quantity');
  // The cell switches between the typed and the computed amount, or the computed amount itself changes.
  if (field === 'discountManual' || (!line?.discountManual && COMPUTED_DISCOUNT_SOURCES.includes(field))) {
    stale.push('discountAmount');
  }
  return stale;
}

// Fields whose value differs between two versions of a line.
function changedFields(before: LineValues | undefined, after: LineValues | undefined): string[] {
  const fields = new Set([...Object.keys(before ?? {}), ...Object.keys(after ?? {})]);
  return [...fields].filter((field) => before?.[field] !== after?.[field]);
}

function lineKeys(row: string, fields: readonly string[]): string[] {
  return [...new Set(fields)].filter((field) => LINE_ERROR_FIELDS.has(field)).map((field) => `lines.${row}.${field}`);
}

/**
 * Form keys whose server error is stale once the form key `changed` has a new value. A server error describes the
 * value that was sent, so it goes with that value, or with what the value is computed from.
 * `previousLines` are the lines as they were before the change: a product pick replaces a whole line (`lines.3`), and
 * only the cells it really changed lose their error.
 */
export function staleServerErrorKeys(
  changed: string,
  values: ServerErrorValues,
  previousLines: readonly LineValues[],
): string[] {
  const cell = /^lines\.(\d+)\.(\w+)$/.exec(changed);
  if (cell) return lineKeys(cell[1], staleLineFields(cell[2], values.lines[Number(cell[1])]));

  const row = /^lines\.(\d+)$/.exec(changed);
  if (row) {
    const line = values.lines[Number(row[1])];
    const fields = changedFields(previousLines[Number(row[1])], line);
    return lineKeys(row[1], fields.flatMap((field) => staleLineFields(field, line)));
  }

  const stale = Object.keys(HEADER_DRIVERS).filter((field) => HEADER_DRIVERS[field].includes(changed));
  if (HEADER_ERROR_FIELDS.has(changed)) stale.unshift(changed);
  // Nothing is paid any more: the fund is neither shown nor sent. While an amount is paid the fund is still needed,
  // and that amount follows the total until it is typed, so it changes with any line.
  if (changed === 'paidAmount' && (toOptionalNumber(values.paidAmount) ?? 0) <= 0) stale.push('fundId');
  return stale;
}
