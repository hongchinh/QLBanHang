import { useState, type KeyboardEvent } from 'react';
import { useFieldArray, useWatch, type UseFormReturn } from 'react-hook-form';
import { Plus, Trash2 } from 'lucide-react';
import { createEmptyStockLine } from '@/features/stock-vouchers/payload';
import type {
  StockLineFormValues,
  StockVoucherFormParsed,
  StockVoucherFormValues,
} from '@/features/stock-vouchers/schema';
import type { PricingMode, ProductSuggestion } from '@/features/products/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import type { Warehouse } from '@/features/warehouses/types';
import { formatStockQuantity } from '@/lib/stock-quantity';
import { ProductTypeaheadCell } from '@/pages/quotations/components/product-typeahead-cell';
import { formatMoneyForDisplay, parseMoneyInput } from '@/pages/quotations/utils/money-input';
import type { StockLineComputed } from '@/pages/stock-vouchers/utils/compute-stock-line';
import '@/pages/quotations/components/line-items-grid.css';

const fmt = new Intl.NumberFormat('vi-VN');

// Stock unit snapshot for metre-based modes (D24).
const METRE_UNITS: Partial<Record<PricingMode, string>> = {
  PerLinearMeter: 'm',
  PerSquareMeter: 'm²',
  PerCubicMeter: 'm³',
};

const LINE_FOCUS_FIELDS = [
  'product-code',
  'warehouse',
  'sheet-count',
  'length',
  'width',
  'thickness',
  'quantity',
  'unit-price',
  'discount-rate',
  'discount-amount',
  'vat-rate',
  'note',
] as const;

type LineFocusField = (typeof LINE_FOCUS_FIELDS)[number];

function cellId(field: LineFocusField | 'stock-at', idx: number): string {
  return `stock-line-${field}-${idx}`;
}

function errorId(field: LineFocusField, idx: number): string {
  return `${cellId(field, idx)}-error`;
}

// Screen readers hear which row a cell belongs to ("Số lượng dòng 3").
function rowLabel(label: string, idx: number): string {
  return `${label} dòng ${idx + 1}`;
}

// Props linking an input to its CellError.
function errorProps(field: LineFocusField, idx: number, error: string | undefined) {
  return {
    'aria-invalid': error ? true : undefined,
    'aria-describedby': error ? errorId(field, idx) : undefined,
    title: error,
  };
}

function parseCellId(id: string): { field: LineFocusField; rowIndex: number } | null {
  const match = /^stock-line-(.+)-(\d+)$/.exec(id);
  if (!match) return null;
  const field = match[1] as LineFocusField;
  if (!LINE_FOCUS_FIELDS.includes(field)) return null;
  return { field, rowIndex: Number(match[2]) };
}

function focusCell(field: LineFocusField, rowIndex: number, afterRender = false): void {
  const focus = () => document.getElementById(cellId(field, rowIndex))?.focus();
  if (afterRender) setTimeout(focus, 0);
  else focus();
}

// Số tấm for non-PerUnit; Dài for metre modes; Rộng for m² / m³; Dày for m³; SL only for PerUnit.
function isFieldDisabled(mode: PricingMode, field: LineFocusField): boolean {
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

function toNum(v: unknown): number | undefined {
  if (v === undefined || v === null || v === '') return undefined;
  const n = typeof v === 'number' ? v : Number(v);
  return Number.isFinite(n) ? n : undefined;
}

function parseQuantityInput(text: string): number | undefined {
  const trimmed = text.trim();
  if (trimmed === '' || !/^-?\d*(?:\.\d*)?$/.test(trimmed)) return undefined;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : undefined;
}

interface NumberCellProps {
  field: LineFocusField;
  rowIndex: number;
  label: string;
  value: number | undefined;
  format: (value: number | undefined) => string;
  parse: (text: string) => number | undefined;
  onCommit: (value: number | undefined) => void;
  disabled: boolean;
  error?: string;
}

// Formatted when idle; raw text while focused. Every keystroke commits the parsed value.
function NumberCell({ field, rowIndex, label, value, format, parse, onCommit, disabled, error }: NumberCellProps) {
  const [editing, setEditing] = useState<string | null>(null);
  return (
    <input
      id={cellId(field, rowIndex)}
      className="cell-input cell-number tabular-nums"
      type="text"
      inputMode="decimal"
      aria-label={rowLabel(label, rowIndex)}
      {...errorProps(field, rowIndex, error)}
      disabled={disabled}
      value={editing ?? format(value)}
      onFocus={(e) => {
        const el = e.currentTarget;
        setEditing(value === undefined ? '' : String(value));
        requestAnimationFrame(() => el.select());
      }}
      onChange={(e) => {
        setEditing(e.target.value);
        onCommit(parse(e.target.value));
      }}
      onBlur={() => setEditing(null)}
    />
  );
}

function CellError({ id, message }: { id: string; message?: string }) {
  if (!message) return null;
  return (
    <div id={id} className="px-1 text-[11px] leading-tight text-destructive">
      {message}
    </div>
  );
}

// %CK / %VAT: an emptied cell stays empty while typing and counts as 0 (written back on blur).
function PercentCell({
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
  value: number | undefined;
  onCommit: (value: number) => void;
  disabled: boolean;
  error?: string;
}) {
  const [editing, setEditing] = useState<string | null>(null);
  return (
    <input
      id={cellId(field, rowIndex)}
      className="cell-input cell-number tabular-nums"
      type="number"
      step="any"
      min={0}
      max={100}
      aria-label={rowLabel(label, rowIndex)}
      {...errorProps(field, rowIndex, error)}
      disabled={disabled}
      value={editing ?? value ?? ''}
      onChange={(e) => {
        setEditing(e.target.value);
        onCommit(toNum(e.target.value) ?? 0);
      }}
      onBlur={() => setEditing(null)}
    />
  );
}

export interface StockLineGridProps {
  form: UseFormReturn<StockVoucherFormValues, unknown, StockVoucherFormParsed>;
  type: StockDirection;
  warehouses: Warehouse[];
  computed: StockLineComputed[];
  // Keyed `${productId}:${warehouseId}`.
  stockAt: Record<string, number>;
  readOnly: boolean;
}

export function StockLineGrid({ form, type, warehouses, computed, stockAt, readOnly }: StockLineGridProps) {
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'lines' });
  const watched = useWatch({ control: form.control, name: 'lines' }) as StockLineFormValues[] | undefined;
  const rows = watched ?? [];
  const lineErrors = form.formState.errors.lines;

  const errorOf = (idx: number, field: keyof StockLineFormValues): string | undefined => {
    const lineError = lineErrors?.[idx] as Record<string, { message?: string } | undefined> | undefined;
    return lineError?.[field]?.message;
  };

  const setLineField = <K extends keyof StockLineFormValues>(
    idx: number,
    field: K,
    value: StockLineFormValues[K],
  ) => {
    form.setValue(`lines.${idx}.${field}` as const, value as never, { shouldDirty: true });
  };

  // One autofill rule for the typeahead and the catalog dialog (section-02 #9).
  const applyProduct = (idx: number, s: ProductSuggestion) => {
    const current = form.getValues(`lines.${idx}`);
    form.setValue(
      `lines.${idx}`,
      {
        ...current,
        productId: s.id,
        productCode: s.code,
        productName: s.name,
        pricingMode: s.pricingMode,
        trackInventory: s.trackInventory,
        priceIncludesVat: s.priceIncludesVat,
        unitName: METRE_UNITS[s.pricingMode] ?? s.unitName ?? '',
        unitPrice: (type === 'In' ? s.costPrice : s.defaultPrice) ?? 0,
        discountRate: type === 'In' ? s.purchaseDiscountRate : s.salesDiscountRate,
        vatRate: s.defaultTaxRate ?? 0,
        length: s.length ?? '',
        width: s.width ?? '',
        thickness: s.thickness ?? '',
        sheetCount: '',
        quantity: 0,
        discountAmount: '',
        discountManual: false,
        warehouseId: current.warehouseId || form.getValues('warehouseId'),
      },
      { shouldDirty: true },
    );
  };

  const addLine = () => {
    append(createEmptyStockLine(fields.length, form.getValues('warehouseId')));
    focusCell('product-code', fields.length, true);
  };

  function enabledFields(rowIndex: number): readonly LineFocusField[] {
    const mode = rows[rowIndex]?.pricingMode ?? 'PerUnit';
    return LINE_FOCUS_FIELDS.filter((field) => !isFieldDisabled(mode, field));
  }

  function moveFocus(current: { field: LineFocusField; rowIndex: number }, direction: 1 | -1) {
    const fieldsInRow = enabledFields(current.rowIndex);
    const position = fieldsInRow.indexOf(current.field);
    if (position === -1) return;

    if (direction === 1) {
      if (position < fieldsInRow.length - 1) return focusCell(fieldsInRow[position + 1], current.rowIndex);
      if (current.rowIndex < fields.length - 1) return focusCell('product-code', current.rowIndex + 1);
      if (!readOnly) addLine();
      return;
    }
    if (position > 0) return focusCell(fieldsInRow[position - 1], current.rowIndex);
    if (current.rowIndex > 0) {
      const previous = enabledFields(current.rowIndex - 1);
      focusCell(previous[previous.length - 1], current.rowIndex - 1);
    }
  }

  function handleGridKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (e.defaultPrevented) return;
    if (e.key === 'Insert' && !readOnly && !e.ctrlKey && !e.shiftKey && !e.altKey && !e.metaKey) {
      e.preventDefault();
      addLine();
      return;
    }
    if (e.key !== 'Enter' || e.ctrlKey || e.altKey || e.metaKey) return;
    const current = parseCellId((e.target as HTMLElement).id);
    if (!current) return;
    e.preventDefault();
    moveFocus(current, e.shiftKey ? -1 : 1);
  }

  const numberInput = (idx: number, field: 'sheetCount' | 'length' | 'width' | 'thickness', focusField: LineFocusField, label: string) => {
    const line = rows[idx];
    const disabled = readOnly || isFieldDisabled(line?.pricingMode ?? 'PerUnit', focusField);
    const error = errorOf(idx, field);
    return (
      <td className="cell-number">
        <input
          id={cellId(focusField, idx)}
          className="cell-input cell-number tabular-nums"
          type="number"
          step="any"
          aria-label={rowLabel(label, idx)}
          {...errorProps(focusField, idx, error)}
          disabled={disabled}
          value={(line?.[field] ?? '') as number | string}
          onChange={(e) => setLineField(idx, field, toNum(e.target.value) ?? '')}
        />
        <CellError id={errorId(focusField, idx)} message={error} />
      </td>
    );
  };

  const percentInput = (idx: number, field: 'discountRate' | 'vatRate', focusField: LineFocusField, label: string) => {
    const error = errorOf(idx, field);
    return (
      <td className="cell-number">
        <PercentCell
          field={focusField}
          rowIndex={idx}
          label={label}
          value={toNum(rows[idx]?.[field])}
          disabled={readOnly}
          error={error}
          onCommit={(value) => {
            if (field === 'discountRate') {
              form.setValue(`lines.${idx}.discountManual`, false, { shouldDirty: true });
            }
            setLineField(idx, field, value);
          }}
        />
        <CellError id={errorId(focusField, idx)} message={error} />
      </td>
    );
  };

  return (
    <div className="space-y-2">
      {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions -- Grid wrapper owns keyboard shortcuts for editable child inputs. */}
      <div className="accounting-grid-wrap" tabIndex={-1} onKeyDown={handleGridKeyDown}>
        <table className="accounting-grid" style={{ minWidth: 2000 }}>
          <colgroup>
            <col style={{ width: 42 }} />
            <col style={{ width: 120 }} />
            <col style={{ minWidth: 200 }} />
            <col style={{ width: 56 }} />
            <col style={{ width: 130 }} />
            <col style={{ width: 70 }} />
            <col style={{ width: 76 }} />
            <col style={{ width: 76 }} />
            <col style={{ width: 64 }} />
            <col style={{ width: 96 }} />
            <col style={{ width: 96 }} />
            <col style={{ width: 110 }} />
            <col style={{ width: 120 }} />
            <col style={{ width: 60 }} />
            <col style={{ width: 104 }} />
            <col style={{ width: 60 }} />
            <col style={{ width: 104 }} />
            <col style={{ width: 120 }} />
            <col style={{ width: 160 }} />
            {!readOnly && <col style={{ width: 42 }} />}
          </colgroup>
          <thead>
            <tr>
              <th className="row-no">STT</th>
              <th>Mã hàng</th>
              <th>Tên hàng</th>
              <th>ĐVT</th>
              <th>Kho</th>
              <th className="cell-number">Số tấm</th>
              <th className="cell-number">Dài</th>
              <th className="cell-number">Rộng</th>
              <th className="cell-number">Dày</th>
              <th className="cell-number">SL</th>
              <th className="cell-number">SL tồn</th>
              <th className="cell-number">Đơn giá</th>
              <th className="cell-number">Số tiền</th>
              <th className="cell-number">%CK</th>
              <th className="cell-number">Tiền CK</th>
              <th className="cell-number">%VAT</th>
              <th className="cell-number">Tiền VAT</th>
              <th className="cell-number">Số còn lại</th>
              <th>Ghi chú</th>
              {!readOnly && <th></th>}
            </tr>
          </thead>
          <tbody>
            {fields.map((field, idx) => {
              const line = rows[idx] ?? (field as unknown as StockLineFormValues);
              const result = computed[idx];
              const mode = line.pricingMode;
              const stockKey = `${line.productId}:${line.warehouseId}`;
              const productError = errorOf(idx, 'productId');
              const warehouseError = errorOf(idx, 'warehouseId');
              const quantityValue = mode === 'PerUnit' ? toNum(line.quantity) : result?.quantity;
              return (
                <tr key={line._uiKey ?? field.id}>
                  <td className="row-no">{idx + 1}</td>
                  <td>
                    {readOnly ? (
                      <input
                        id={cellId('product-code', idx)}
                        className="cell-input"
                        aria-label={rowLabel('Mã hàng', idx)}
                        value={line.productCode ?? ''}
                        disabled
                      />
                    ) : (
                      <ProductTypeaheadCell
                        variant="cell"
                        inputId={cellId('product-code', idx)}
                        nextFocusId={cellId('warehouse', idx)}
                        value={line.productCode ?? ''}
                        onChange={(v) => setLineField(idx, 'productCode', v)}
                        onSelect={(s) => applyProduct(idx, s)}
                      />
                    )}
                    <CellError id={errorId('product-code', idx)} message={productError} />
                  </td>
                  <td>
                    <div className="cell-readonly" title={line.productName}>
                      {line.productName}
                    </div>
                  </td>
                  <td>
                    <div className="cell-readonly">{line.unitName}</div>
                  </td>
                  <td>
                    <select
                      id={cellId('warehouse', idx)}
                      className="cell-input"
                      aria-label={rowLabel('Kho', idx)}
                      {...errorProps('warehouse', idx, warehouseError)}
                      disabled={readOnly}
                      value={line.warehouseId ?? ''}
                      onChange={(e) => setLineField(idx, 'warehouseId', e.target.value)}
                    >
                      {!line.warehouseId && <option value="">-- Chọn kho --</option>}
                      {warehouses.map((w) => (
                        <option key={w.id} value={w.id}>
                          {w.name}
                        </option>
                      ))}
                    </select>
                    <CellError id={errorId('warehouse', idx)} message={warehouseError} />
                  </td>
                  {numberInput(idx, 'sheetCount', 'sheet-count', 'Số tấm')}
                  {numberInput(idx, 'length', 'length', 'Dài')}
                  {numberInput(idx, 'width', 'width', 'Rộng')}
                  {numberInput(idx, 'thickness', 'thickness', 'Dày')}
                  <td className="cell-number">
                    <NumberCell
                      field="quantity"
                      rowIndex={idx}
                      label="Số lượng"
                      value={quantityValue}
                      format={formatStockQuantity}
                      parse={parseQuantityInput}
                      onCommit={(v) => setLineField(idx, 'quantity', v ?? 0)}
                      disabled={readOnly || isFieldDisabled(mode, 'quantity')}
                      error={errorOf(idx, 'quantity')}
                    />
                    <CellError id={errorId('quantity', idx)} message={errorOf(idx, 'quantity')} />
                  </td>
                  <td className="cell-number">
                    <div id={cellId('stock-at', idx)} className="cell-readonly cell-number tabular-nums">
                      {!line.productId
                        ? ''
                        : line.trackInventory
                          ? formatStockQuantity(stockAt[stockKey])
                          : '—'}
                    </div>
                  </td>
                  <td className="cell-number">
                    <NumberCell
                      field="unit-price"
                      rowIndex={idx}
                      label="Đơn giá"
                      value={toNum(line.unitPrice)}
                      format={formatMoneyForDisplay}
                      parse={parseMoneyInput}
                      onCommit={(v) => setLineField(idx, 'unitPrice', v ?? 0)}
                      disabled={readOnly}
                      error={errorOf(idx, 'unitPrice')}
                    />
                    <CellError id={errorId('unit-price', idx)} message={errorOf(idx, 'unitPrice')} />
                  </td>
                  <td className="cell-number">
                    <div className="cell-readonly cell-number tabular-nums">{fmt.format(result?.amount ?? 0)}</div>
                  </td>
                  {percentInput(idx, 'discountRate', 'discount-rate', '%CK')}
                  <td className="cell-number">
                    <NumberCell
                      field="discount-amount"
                      rowIndex={idx}
                      label="Tiền CK"
                      value={line.discountManual ? toNum(line.discountAmount) : result?.discountAmount}
                      format={formatMoneyForDisplay}
                      parse={parseMoneyInput}
                      onCommit={(v) => {
                        setLineField(idx, 'discountManual', true);
                        setLineField(idx, 'discountAmount', v ?? 0);
                      }}
                      disabled={readOnly}
                      error={errorOf(idx, 'discountAmount')}
                    />
                    <CellError id={errorId('discount-amount', idx)} message={errorOf(idx, 'discountAmount')} />
                  </td>
                  {percentInput(idx, 'vatRate', 'vat-rate', '%VAT')}
                  <td className="cell-number">
                    <div className="cell-readonly cell-number tabular-nums">{fmt.format(result?.vatAmount ?? 0)}</div>
                  </td>
                  <td className="cell-number">
                    <div className="cell-readonly cell-number tabular-nums">{fmt.format(result?.netAmount ?? 0)}</div>
                  </td>
                  <td>
                    <input
                      id={cellId('note', idx)}
                      className="cell-input"
                      aria-label={rowLabel('Ghi chú', idx)}
                      disabled={readOnly}
                      value={line.note ?? ''}
                      onChange={(e) => setLineField(idx, 'note', e.target.value)}
                    />
                  </td>
                  {!readOnly && (
                    <td className="cell-action">
                      <button type="button" aria-label={rowLabel('Xóa', idx)} onClick={() => remove(idx)}>
                        <Trash2
                          className="h-4 w-4 text-red-600"
                          style={{ display: 'inline-block', verticalAlign: 'middle' }}
                        />
                      </button>
                    </td>
                  )}
                </tr>
              );
            })}
            {fields.length === 0 && (
              <tr>
                <td colSpan={readOnly ? 19 : 20} className="empty-placeholder">
                  Chưa có dòng nào.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {!readOnly && (
        <>
          <div className="line-items-footer">
            <div className="keyboard-guide">
              <span><span className="kbd">Enter</span> Tiếp</span>
              <span><span className="kbd">Shift</span>+<span className="kbd">Enter</span> Lùi</span>
              <span><span className="kbd">Ctrl</span>+<span className="kbd">S</span> Lưu</span>
              <span><span className="kbd">Insert</span> Thêm dòng</span>
            </div>
          </div>
          <div className="line-items-toolbar">
            <button type="button" className="lib-btn" onClick={addLine}>
              <Plus className="h-4 w-4 text-cyan-600" />
              Thêm dòng
            </button>
          </div>
        </>
      )}
    </div>
  );
}
