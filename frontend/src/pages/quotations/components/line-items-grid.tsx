import { forwardRef, useImperativeHandle, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react';
import { useFieldArray, useWatch, type UseFormReturn } from 'react-hook-form';
import { AppIcon } from '@/components/icons';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import type {
  QuotationFormParsed,
  QuotationFormValues,
  QuotationLineFormValues,
} from '@/features/quotations/schema';
import {
  isDimensionField,
  normalizePricingMode,
  shownDimensions,
  usesDimension,
  type DimensionField,
} from '@/lib/dimension-columns';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
import { toOptionalNumber } from '@/lib/to-number';
import { useAppearedKeys } from '@/lib/use-appeared-keys';
import { cn } from '@/lib/utils';
import { ProductTypeaheadCell } from './product-typeahead-cell';
import { computeLineCost, computeLineQuantity, computeLineTotal } from '@/pages/quotations/utils/compute-line';
import { useAuthStore } from '@/stores/auth-store';
import './line-items-grid.css';

const fmt = new Intl.NumberFormat('vi-VN');
const vnd = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });
const quantityIntegerFmt = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const quantityDecimalFmt = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const LINE_FOCUS_FIELDS = [
  'product-code',
  'name',
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

// Tên hàng has no fixed width; the table's min-width keeps this much for it.
const NAME_MIN_WIDTH = 200;

const DIMENSION_INPUTS: Record<DimensionField, { name: 'sheetCount' | 'length' | 'width' | 'thickness'; label: string }> = {
  'sheet-count': { name: 'sheetCount', label: 'Số tấm' },
  length: { name: 'length', label: 'Dài' },
  width: { name: 'width', label: 'Rộng' },
  thickness: { name: 'thickness', label: 'Dày' },
};

function getLineCellId(field: LineFocusField, idx: number): string {
  return `line-${field}-${idx}`;
}

function parseLineCellId(id: string): { field: LineFocusField; rowIndex: number } | null {
  const match = /^line-(.+)-(\d+)$/.exec(id);
  if (!match) return null;
  const field = match[1] as LineFocusField;
  if (!LINE_FOCUS_FIELDS.includes(field)) return null;
  const rowIndex = Number(match[2]);
  return Number.isInteger(rowIndex) ? { field, rowIndex } : null;
}

function focusLineCell(field: LineFocusField, rowIndex: number): void {
  document.getElementById(getLineCellId(field, rowIndex))?.focus();
}

function focusLineCellAfterRender(field: LineFocusField, rowIndex: number): void {
  setTimeout(() => focusLineCell(field, rowIndex), 0);
}

export function createEmptyLine(sortOrder: number): QuotationLineFormValues {
  return {
    _uiKey: crypto.randomUUID(),
    sortOrder,
    productName: '',
    unitName: '',
    pricingMode: 'PerUnit',
    quantity: 0,
    unitPrice: 0,
  } as QuotationLineFormValues;
}

// A line's mode opens the dimensions it uses, and SL only on a unit line. A missing or unknown mode (an old draft)
// counts as PerUnit.
function isLineFocusFieldDisabled(line: QuotationLineFormValues, field: LineFocusField): boolean {
  if (isDimensionField(field)) return !usesDimension(line.pricingMode, field);
  if (field === 'quantity') return normalizePricingMode(line.pricingMode) !== 'PerUnit';
  return false;
}

export interface LineItemsGridHandle {
  ensureFirstLineAndFocusProductCode: () => void;
}

interface Props {
  form: UseFormReturn<QuotationFormValues, unknown, QuotationFormParsed>;
}

export const LineItemsGrid = forwardRef<LineItemsGridHandle, Props>(function LineItemsGrid({ form }, ref) {
  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: 'lines',
  });
  // The watch renders the grid on every line change, but the rows are read from the form itself, which always matches
  // the field array. react-hook-form hands remove() to its watchers one render late: read from the watched copy, the
  // rows would key the row that moves up with the deleted line, and that row would remount and drop the focus.
  useWatch({ control: form.control, name: 'lines' });
  const rows = (form.getValues('lines') ?? []) as QuotationLineFormValues[];
  const wrapRef = useRef<HTMLDivElement>(null);
  // The cell being edited, as `${rowKey}:${field}`: a cell id would change when a row above it goes.
  const [editingMoneyKey, setEditingMoneyKey] = useState<string | null>(null);
  const [editingQuantityKey, setEditingQuantityKey] = useState<string | null>(null);
  const [editingQuantityText, setEditingQuantityText] = useState('');
  const [clearAllOpen, setClearAllOpen] = useState(false);
  const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));
  // Số tấm, Dài, Rộng, Dày follow the highest pricing mode of the lines: one pass over the rows on every render.
  const dims = shownDimensions(rows);
  // Columns that appear flash for a moment (class col-appeared in line-items-grid.css), never on the first render.
  const appeared = useAppearedKeys(dims);
  const columns = COLUMNS.filter((c) => (isDimensionField(c.key) ? dims.includes(c.key) : !c.cost || canViewCost));
  const minWidth = columns.reduce((sum, c) => sum + (c.width ?? NAME_MIN_WIDTH), 0);
  // Showing or hiding a dimension column shifts the columns to its right: keep the focused cell in view. Compares the
  // joined list (the array is new on every render) with the last commit's; the first render only records it. No
  // dependency list, as in useAppearedKeys: the rows come from the form, and React may throw away a render that read
  // new rows while keeping its deps.
  const dimsKey = dims.join(',');
  const dimsKeyRef = useRef<string | null>(null);
  useLayoutEffect(() => {
    const previous = dimsKeyRef.current;
    dimsKeyRef.current = dimsKey;
    if (previous === null || previous === dimsKey) return;
    const active = document.activeElement;
    if (active && wrapRef.current?.contains(active)) active.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  });

  useImperativeHandle(ref, () => ({
    ensureFirstLineAndFocusProductCode() {
      if (fields.length === 0) {
        append(createEmptyLine(0));
        setTimeout(() => document.getElementById(getLineCellId('product-code', 0))?.focus(), 0);
      } else {
        document.getElementById(getLineCellId('product-code', 0))?.focus();
      }
    },
  }));

  const setLineField = <K extends keyof QuotationLineFormValues>(
    idx: number,
    field: K,
    value: QuotationLineFormValues[K],
  ) => {
    form.setValue(`lines.${idx}.${field}` as const, value as never, { shouldDirty: true });
  };

  const addLine = () => {
    append(createEmptyLine(fields.length));
    focusLineCellAfterRender('product-code', fields.length);
  };

  function getLineForNavigation(rowIndex: number): QuotationLineFormValues | undefined {
    return (rows[rowIndex] ?? fields[rowIndex]) as unknown as QuotationLineFormValues | undefined;
  }

  // Đơn giá nhập without quotations.view_cost and a hidden dimension column have no cell to focus.
  const isRendered = (field: LineFocusField) =>
    (field !== 'unit-cost' || canViewCost) && (!isDimensionField(field) || dims.includes(field));

  function getEnabledFocusFields(rowIndex: number): readonly LineFocusField[] {
    const rendered = LINE_FOCUS_FIELDS.filter(isRendered);
    const line = getLineForNavigation(rowIndex);
    if (!line) return rendered;
    return rendered.filter((field) => !isLineFocusFieldDisabled(line, field));
  }

  function moveLineFocus(current: { field: LineFocusField; rowIndex: number }, direction: 1 | -1) {
    if (current.rowIndex < 0 || current.rowIndex >= fields.length) return;
    const enabledFields = getEnabledFocusFields(current.rowIndex);
    const fieldIndex = enabledFields.indexOf(current.field);
    if (fieldIndex === -1) return;

    if (direction === 1) {
      if (fieldIndex < enabledFields.length - 1) {
        focusLineCell(enabledFields[fieldIndex + 1], current.rowIndex);
        return;
      }
      if (current.rowIndex < fields.length - 1) {
        focusLineCell(getEnabledFocusFields(current.rowIndex + 1)[0], current.rowIndex + 1);
        return;
      }
      append(createEmptyLine(fields.length));
      focusLineCellAfterRender('product-code', fields.length);
      return;
    }

    if (fieldIndex > 0) {
      focusLineCell(enabledFields[fieldIndex - 1], current.rowIndex);
      return;
    }
    if (current.rowIndex > 0) {
      const previousRowFields = getEnabledFocusFields(current.rowIndex - 1);
      focusLineCell(previousRowFields[previousRowFields.length - 1], current.rowIndex - 1);
    }
  }

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
    if (e.defaultPrevented) return;

    if (e.key === 'Insert' && !e.ctrlKey && !e.shiftKey && !e.altKey && !e.metaKey) {
      e.preventDefault();
      addLine();
      return;
    }

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

    // `isComposing`: the Enter that commits a Telex/VNI composition must not jump to the next field.
    if (e.key !== 'Enter' || e.ctrlKey || e.altKey || e.metaKey || e.nativeEvent.isComposing) return;
    const target = e.target as HTMLElement;
    const current = parseLineCellId(target.id);
    if (!current) return;
    if (current.field === 'name') return;
    e.preventDefault();
    moveLineFocus(current, e.shiftKey ? -1 : 1);
  }

  const totals = rows.reduce(
    (acc, line) => {
      const lineLike = toLineLike(line);
      const lineTotal = computeLineTotal(lineLike);
      const lineCost = computeLineCost(lineLike);
      acc.sales += lineTotal;
      if (lineCost != null) {
        acc.cost += lineCost;
        acc.profit += lineTotal - lineCost;
      }
      return acc;
    },
    { sales: 0, cost: 0, profit: 0 },
  );

  function clearAllLines() {
    for (let i = fields.length - 1; i >= 0; i--) remove(i);
    setClearAllOpen(false);
  }

  function handleClearAll() {
    if (fields.length === 0) return;
    setClearAllOpen(true);
  }

  return (
    <div className="space-y-2">
      <div className="line-items-heading">
        <h3>Chi tiết hàng hóa</h3>
        <div className="line-items-totals" aria-label="Tổng chi tiết hàng hóa">
          <strong>Tổng thành tiền bán: {vnd.format(totals.sales)}</strong>
          {canViewCost && <strong>Tổng thành tiền nhập: {vnd.format(totals.cost)}</strong>}
          {canViewCost && <strong>Tổng lợi nhuận: {vnd.format(totals.profit)}</strong>}
        </div>
      </div>

      {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions -- Grid wrapper owns keyboard shortcuts for editable child inputs. */}
      <div ref={wrapRef} className="accounting-grid-wrap" tabIndex={-1} onKeyDown={handleGridKeyDown}>
        <table className="accounting-grid" style={{ minWidth }}>
          <colgroup>
            {columns.map((c) => (
              <col key={c.key} style={c.width ? { width: c.width } : undefined} />
            ))}
          </colgroup>
          <thead>
            <tr>
              {columns.map((c) => (
                <th
                  key={c.key}
                  className={cn(c.key === 'row-no' && 'row-no', appeared.has(c.key) && 'col-appeared') || undefined}
                >
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {fields.map((field, idx) => {
              const line = rows[idx] ?? (field as unknown as QuotationLineFormValues);
              const lineLike = toLineLike(line);
              const effectiveQuantity = computeLineQuantity(lineLike);
              const quantityCellId = getLineCellId('quantity', idx);
              const quantityValue = line.pricingMode === 'PerUnit' ? line.quantity : effectiveQuantity;
              const lineTotal = computeLineTotal(lineLike);
              const lineCost = computeLineCost(lineLike);
              const lineProfit = lineCost != null ? lineTotal - lineCost : undefined;
              const rowKey = line._uiKey ?? field.id;
              return (
                <tr key={rowKey} data-row-index={idx}>
                  <td className="row-no">{idx + 1}</td>
                  <td>
                    <ProductTypeaheadCell
                      variant="cell"
                      inputId={getLineCellId('product-code', idx)}
                      nextFocusId={getLineCellId('name', idx)}
                      // Closed until the user types: Enter, Shift+Enter or a row delete into a filled cell must
                      // not re-pick its product with the next Enter (decision 11).
                      openOnFocus={false}
                      allowQuickAdd
                      value={(line.productCode ?? '') as string}
                      onChange={(v) => setLineField(idx, 'productCode', v)}
                      onSelect={(s) => {
                        setLineField(idx, 'quantity', 0 as never);
                        setLineField(idx, 'productId', s.id);
                        setLineField(idx, 'productCode', s.code);
                        setLineField(idx, 'productName', s.name);
                        setLineField(idx, 'specification', (s.specification ?? '') as never);
                        setLineField(idx, 'unitName', (s.unitName ?? line.unitName) as never);
                        setLineField(idx, 'pricingMode', s.pricingMode);
                        setLineField(idx, 'unitPrice', (s.defaultPrice ?? 0) as never);
                        setLineField(idx, 'unitCost', s.costPrice as never);
                      }}
                    />
                  </td>
                  <td>
                    <textarea
                      id={getLineCellId('name', idx)}
                      className="cell-input cell-input-multiline"
                      aria-label="Tên hàng"
                      value={(line.productName ?? '') as string}
                      onChange={(e) => setLineField(idx, 'productName', e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Tab') {
                          e.preventDefault();
                          e.stopPropagation();
                          moveLineFocus({ field: 'name', rowIndex: idx }, e.shiftKey ? -1 : 1);
                          return;
                        }
                        if (e.key === 'Enter' && !e.ctrlKey) {
                          e.preventDefault();
                          e.stopPropagation();
                          moveLineFocus({ field: 'name', rowIndex: idx }, e.shiftKey ? -1 : 1);
                          return;
                        }
                        if (e.key === 'Enter' && e.ctrlKey && !e.shiftKey && !e.altKey && !e.metaKey) {
                          const textarea = e.currentTarget;
                          const start = textarea.selectionStart;
                          const end = textarea.selectionEnd;
                          const value = textarea.value;
                          const newValue = value.substring(0, start) + '\n' + value.substring(end);
                          setLineField(idx, 'productName', newValue as never);
                          setTimeout(() => {
                            textarea.selectionStart = textarea.selectionEnd = start + 1;
                            textarea.focus();
                          }, 0);
                          e.preventDefault();
                          e.stopPropagation();
                        }
                      }}
                    />
                  </td>
                  <td>
                    <input
                      id={getLineCellId('unit', idx)}
                      className="cell-input"
                      aria-label="Đơn vị tính"
                      value={(line.unitName ?? '') as string}
                      onChange={(e) => setLineField(idx, 'unitName', e.target.value)}
                    />
                  </td>
                  {dims.map((f) => {
                    const { name, label } = DIMENSION_INPUTS[f];
                    // A cell the row's mode does not use shows "—"; its stored value stays in the form, so it is
                    // still saved and printed.
                    const unused = isLineFocusFieldDisabled(line, f);
                    return (
                      <td key={f} className={cn('cell-number', appeared.has(f) && 'col-appeared')}>
                        <input
                          id={getLineCellId(f, idx)}
                          className="cell-input cell-number tabular-nums"
                          type="number"
                          step="any"
                          aria-label={label}
                          disabled={unused}
                          placeholder={unused ? '—' : undefined}
                          value={unused ? '' : numInput(line[name])}
                          onChange={(e) => setLineField(idx, name, parseNum(e.target.value) as never)}
                        />
                      </td>
                    );
                  })}
                  <td className="cell-number">
                    <input
                      id={quantityCellId}
                      className="cell-input cell-number"
                      type="text"
                      inputMode="decimal"
                      aria-label="Số lượng"
                      disabled={isLineFocusFieldDisabled(line, 'quantity')}
                      value={
                        editingQuantityKey === `${rowKey}:quantity`
                          ? editingQuantityText
                          : formatQuantityForDisplay(quantityValue)
                      }
                      onFocus={(e) => {
                        const el = e.currentTarget;
                        setEditingQuantityKey(`${rowKey}:quantity`);
                        setEditingQuantityText(formatQuantityForEditing(quantityValue));
                        requestAnimationFrame(() => el.select());
                      }}
                      onBlur={(e) => {
                        const parsed = parseQuantityInput(e.currentTarget.value);
                        if (parsed !== undefined) {
                          setLineField(idx, 'quantity', parsed as never);
                        }
                        setEditingQuantityKey(null);
                        setEditingQuantityText('');
                      }}
                      onChange={(e) => {
                        setEditingQuantityText(e.target.value);
                      }}
                    />
                  </td>
                  <td className="cell-number">
                    <input
                      id={getLineCellId('unit-price', idx)}
                      className="cell-input cell-number"
                      type="text"
                      inputMode="decimal"
                      aria-label="Đơn giá bán"
                      value={editingMoneyKey === `${rowKey}:unit-price` ? (line.unitPrice ?? '') : formatMoneyForDisplay(line.unitPrice)}
                      onFocus={(e) => {
                        const el = e.currentTarget;
                        setEditingMoneyKey(`${rowKey}:unit-price`);
                        requestAnimationFrame(() => el.select());
                      }}
                      onBlur={() => setEditingMoneyKey(null)}
                      onChange={(e) => setLineField(idx, 'unitPrice', (parseMoneyInput(e.target.value) ?? 0) as never)}
                    />
                  </td>
                  <td className="cell-number">
                    <div className="cell-readonly cell-number tabular-nums">{fmt.format(lineTotal)}</div>
                  </td>
                  {canViewCost && (
                    <td className="cell-number">
                      <input
                        id={getLineCellId('unit-cost', idx)}
                        className="cell-input cell-number"
                        type="text"
                        inputMode="decimal"
                        aria-label="Đơn giá nhập"
                        value={editingMoneyKey === `${rowKey}:unit-cost` ? (line.unitCost ?? '') : formatMoneyForDisplay(line.unitCost)}
                        onFocus={(e) => {
                          const el = e.currentTarget;
                          setEditingMoneyKey(`${rowKey}:unit-cost`);
                          requestAnimationFrame(() => el.select());
                        }}
                        onBlur={() => setEditingMoneyKey(null)}
                        onChange={(e) => setLineField(idx, 'unitCost', parseMoneyInput(e.target.value) as never)}
                      />
                    </td>
                  )}
                  {canViewCost && (
                    <td className="cell-number">
                      <div className="cell-readonly cell-number tabular-nums">
                        {lineCost == null ? '' : fmt.format(lineCost)}
                      </div>
                    </td>
                  )}
                  {canViewCost && (
                    <td className="cell-number">
                      <div className="cell-readonly cell-number tabular-nums">
                        {lineProfit == null ? '' : fmt.format(lineProfit)}
                      </div>
                    </td>
                  )}
                  <td className="cell-action">
                    <button
                      type="button"
                      aria-label="Xóa dòng"
                      // The button goes with its row: the focus moves to the row that takes its place.
                      onClick={() => removeLine(idx, 'product-code')}
                    >
                      <AppIcon name="delete" style={{ display: 'inline-block', verticalAlign: 'middle' }} />
                    </button>
                  </td>
                </tr>
              );
            })}
            {fields.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="empty-placeholder">
                  Chưa có dòng nào.{' '}
                  <button type="button" className="empty-placeholder-link" onClick={addLine}>
                    Bấm để thêm dòng đầu tiên
                  </button>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="line-items-footer">
        <div className="keyboard-guide">
          <span><span className="kbd">Enter</span> Tiếp (Tên hàng: <span className="kbd">Ctrl</span>+<span className="kbd">Enter</span> để xuống dòng)</span>
          <span><span className="kbd">Shift</span>+<span className="kbd">Enter</span> Lùi</span>
          <span><span className="kbd">Ctrl</span>+<span className="kbd">S</span> Lưu</span>
          <span><span className="kbd">Insert</span> Thêm dòng</span>
          <span><span className="kbd">Ctrl</span>+<span className="kbd">Delete</span> Xóa dòng</span>
        </div>
      </div>

      <div className="line-items-toolbar">
        <button type="button" id="line-add" className="lib-btn" onClick={addLine}>
          <AppIcon name="add" />
          Thêm dòng
        </button>
        <button type="button" className="lib-btn lib-btn-danger" onClick={handleClearAll} disabled={fields.length === 0}>
          <AppIcon name="delete" />
          Xóa tất cả dòng
        </button>
      </div>

      <ConfirmDialog
        open={clearAllOpen}
        onOpenChange={setClearAllOpen}
        title="Xóa tất cả dòng?"
        description={`Xóa toàn bộ ${fields.length} dòng?`}
        confirmLabel="Xóa"
        cancelLabel="Hủy"
        destructive
        onConfirm={clearAllLines}
      />
    </div>
  );

});

function toLineLike(line: QuotationLineFormValues) {
  return {
    pricingMode: line.pricingMode,
    length: toOptionalNumber(line.length),
    width: toOptionalNumber(line.width),
    thickness: toOptionalNumber(line.thickness),
    density: toOptionalNumber(line.density),
    sheetCount: toOptionalNumber(line.sheetCount),
    quantity: toOptionalNumber(line.quantity) ?? 0,
    unitPrice: toOptionalNumber(line.unitPrice) ?? 0,
    unitCost: toOptionalNumber(line.unitCost),
  };
}

function parseNum(v: string): number | undefined {
  if (v === '') return undefined;
  const n = Number(v);
  return Number.isFinite(n) ? n : undefined;
}

function parseQuantityInput(text: string): number | undefined {
  const trimmed = text.trim();
  if (trimmed === '') return undefined;
  if (!/^-?\d*(?:\.\d*)?$/.test(trimmed)) return undefined;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : undefined;
}

function formatQuantityForEditing(value: unknown): string {
  if (value === undefined || value === null || value === '') return '';
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? String(n) : '';
}

function formatQuantityForDisplay(value: unknown): string {
  if (value === undefined || value === null || value === '') return '';
  const n = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(n)) return '';
  return Number.isInteger(n) ? quantityIntegerFmt.format(n) : quantityDecimalFmt.format(n);
}

function numInput(v: unknown): string | number {
  if (v === undefined || v === null) return '';
  return v as number | string;
}
