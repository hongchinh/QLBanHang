import { forwardRef, useImperativeHandle, useRef, useState, type KeyboardEvent } from 'react';
import { useFieldArray, useWatch, type UseFormReturn } from 'react-hook-form';
import { AppIcon } from '@/components/icons';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import type {
  QuotationFormParsed,
  QuotationFormValues,
  QuotationLineFormValues,
} from '@/features/quotations/schema';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
import { toOptionalNumber } from '@/lib/to-number';
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
  'length',
  'width',
  'thickness',
  'sheet-count',
  'quantity',
  'unit-price',
  'unit-cost',
] as const;

type LineFocusField = (typeof LINE_FOCUS_FIELDS)[number];

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
  const watched = useWatch({ control: form.control, name: 'lines' }) as QuotationLineFormValues[] | undefined;
  const rows = watched ?? [];
  const wrapRef = useRef<HTMLDivElement>(null);
  const [activeRowIndex, setActiveRowIndex] = useState<number | null>(null);
  const [editingMoneyCellId, setEditingMoneyCellId] = useState<string | null>(null);
  const [editingQuantityCellId, setEditingQuantityCellId] = useState<string | null>(null);
  const [editingQuantityText, setEditingQuantityText] = useState('');
  const [clearAllOpen, setClearAllOpen] = useState(false);
  const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));

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

  function getEnabledFocusFields(rowIndex: number): readonly LineFocusField[] {
    const line = getLineForNavigation(rowIndex);
    if (!line) return LINE_FOCUS_FIELDS;
    return LINE_FOCUS_FIELDS.filter((field) => !isLineFocusFieldDisabled(line, field));
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

  function handleGridKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (e.defaultPrevented) return;

    if (e.key === 'Insert' && !e.ctrlKey && !e.shiftKey && !e.altKey && !e.metaKey) {
      e.preventDefault();
      addLine();
      return;
    }

    if (e.key === 'Delete' && e.ctrlKey) {
      e.preventDefault();
      if (activeRowIndex != null && activeRowIndex < fields.length) {
        remove(activeRowIndex);
        setActiveRowIndex(null);
      }
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
    setActiveRowIndex(null);
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
        <table className="accounting-grid">
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
              const lengthDisabled = isLineFocusFieldDisabled(line, 'length');
              const widthDisabled = isLineFocusFieldDisabled(line, 'width');
              const thicknessDisabled = isLineFocusFieldDisabled(line, 'thickness');
              const sheetCountDisabled = isLineFocusFieldDisabled(line, 'sheet-count');
              return (
                <tr key={line._uiKey ?? field.id} onFocus={() => setActiveRowIndex(idx)}>
                  <td className="row-no">{idx + 1}</td>
                  <td>
                    <ProductTypeaheadCell
                      variant="cell"
                      inputId={getLineCellId('product-code', idx)}
                      nextFocusId={getLineCellId('name', idx)}
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
                  <td className="cell-number">
                    <input
                      id={quantityCellId}
                      className="cell-input cell-number"
                      type="text"
                      inputMode="decimal"
                      aria-label="Số lượng"
                      disabled={isLineFocusFieldDisabled(line, 'quantity')}
                      value={
                        editingQuantityCellId === quantityCellId
                          ? editingQuantityText
                          : formatQuantityForDisplay(quantityValue)
                      }
                      onFocus={(e) => {
                        const el = e.currentTarget;
                        setEditingQuantityCellId(quantityCellId);
                        setEditingQuantityText(formatQuantityForEditing(quantityValue));
                        requestAnimationFrame(() => el.select());
                      }}
                      onBlur={(e) => {
                        const parsed = parseQuantityInput(e.currentTarget.value);
                        if (parsed !== undefined) {
                          setLineField(idx, 'quantity', parsed as never);
                        }
                        setEditingQuantityCellId(null);
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
                      value={editingMoneyCellId === getLineCellId('unit-price', idx) ? (line.unitPrice ?? '') : formatMoneyForDisplay(line.unitPrice)}
                      onFocus={(e) => {
                        const el = e.currentTarget;
                        setEditingMoneyCellId(getLineCellId('unit-price', idx));
                        requestAnimationFrame(() => el.select());
                      }}
                      onBlur={() => setEditingMoneyCellId(null)}
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
                        value={editingMoneyCellId === getLineCellId('unit-cost', idx) ? (line.unitCost ?? '') : formatMoneyForDisplay(line.unitCost)}
                        onFocus={(e) => {
                          const el = e.currentTarget;
                          setEditingMoneyCellId(getLineCellId('unit-cost', idx));
                          requestAnimationFrame(() => el.select());
                        }}
                        onBlur={() => setEditingMoneyCellId(null)}
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
                      onClick={() => {
                        remove(idx);
                        if (activeRowIndex === idx) setActiveRowIndex(null);
                      }}
                    >
                      <AppIcon name="delete" style={{ display: 'inline-block', verticalAlign: 'middle' }} />
                    </button>
                  </td>
                </tr>
              );
            })}
            {fields.length === 0 && (
              <tr>
                <td colSpan={canViewCost ? 12 : 9} className="empty-placeholder">
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
        <button type="button" className="lib-btn" onClick={addLine}>
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
