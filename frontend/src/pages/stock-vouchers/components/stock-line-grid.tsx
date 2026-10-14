import { createPortal } from 'react-dom';
import { useLayoutEffect, useRef, useState, type CSSProperties, type FocusEvent, type KeyboardEvent } from 'react';
import { useFieldArray, useWatch, type UseFormReturn } from 'react-hook-form';
import { Keyboard, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { CellError } from '@/components/money-cell/money-cell';
import { createEmptyStockLine } from '@/features/stock-vouchers/payload';
import type {
  StockLineFormValues,
  StockVoucherFormParsed,
  StockVoucherFormValues,
} from '@/features/stock-vouchers/schema';
import type { PricingMode, ProductSuggestion } from '@/features/products/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import type { Warehouse } from '@/features/warehouses/types';
import { cn } from '@/lib/utils';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
import { formatStockQuantity } from '@/lib/stock-quantity';
import { toOptionalNumber } from '@/lib/to-number';
import { useAuthStore } from '@/stores/auth-store';
import { ProductTypeaheadCell } from '@/pages/quotations/components/product-typeahead-cell';
import type { StockLineComputed } from '@/pages/stock-vouchers/utils/compute-stock-line';
import { StockGridSettingsPopover } from './stock-grid-settings-popover';
import {
  enterPath,
  isFieldDisabled,
  LINE_FOCUS_FIELDS,
  nextStop,
  prevStop,
  verticalTarget,
  type LineFocusField,
} from '@/pages/stock-vouchers/utils/line-navigation';
import {
  forcedColumns,
  OPTIONAL_COLUMNS,
  useStockGridPrefs,
  type OptionalColumn,
} from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';
import '@/pages/quotations/components/line-items-grid.css';
import './stock-line-grid.css';

const fmt = new Intl.NumberFormat('vi-VN');

// Stock unit snapshot for metre-based modes (D24).
const METRE_UNITS: Partial<Record<PricingMode, string>> = {
  PerLinearMeter: 'm',
  PerSquareMeter: 'm²',
  PerCubicMeter: 'm³',
};

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

function parseQuantityInput(text: string): number | undefined {
  const trimmed = text.trim();
  if (trimmed === '' || !/^-?\d*(?:\.\d*)?$/.test(trimmed)) return undefined;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : undefined;
}

/**
 * Tells a keyboard focus from a mouse one. A mouse press on the cell comes right before its mouse focus; every other
 * focus is keyboard navigation (Enter / arrows / Ctrl+Delete through focusCell, or Tab). After keyboard navigation a
 * numeric cell selects its whole value at once, so a digit typed right away replaces the value instead of being
 * appended. What a mouse focus does is up to the cell: a type=number cell selects nothing, so the caret stays where
 * the browser put it; a NumberCell selects its whole text one frame later, as it always did. Returns the props to
 * spread on the <input>.
 */
function useFocusOrigin(onFocus: (el: HTMLInputElement, byKeyboard: boolean) => void, onBlur?: () => void) {
  const pressed = useRef(false);
  return {
    onMouseDown: () => {
      pressed.current = true;
    },
    onFocus: (e: FocusEvent<HTMLInputElement>) => {
      const byKeyboard = !pressed.current;
      pressed.current = false;
      onFocus(e.currentTarget, byKeyboard);
    },
    onBlur: () => {
      // A press on the cell that already had the focus is followed by no focus event: forget it here.
      pressed.current = false;
      onBlur?.();
    },
  };
}

// A type=number cell shows the same text focused or not, so its value is selected right in the focus event.
function selectOnKeyboardFocus(el: HTMLInputElement, byKeyboard: boolean) {
  if (byKeyboard) el.select();
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
  const ref = useRef<HTMLInputElement>(null);
  const selectAfterRender = useRef(false);
  // Keyboard focus: the raw text replaces the formatted one on this render, so select it as soon as
  // that render commits (still inside the event that moved the focus, not a frame later).
  useLayoutEffect(() => {
    if (!selectAfterRender.current) return;
    selectAfterRender.current = false;
    if (document.activeElement === ref.current) ref.current?.select();
  });
  const focusProps = useFocusOrigin(
    (el, byKeyboard) => {
      setEditing(value === undefined ? '' : String(value));
      if (byKeyboard) {
        selectAfterRender.current = true;
      } else {
        // Mouse focus, as before: the browser sets the caret right after the focus event, so the whole text is
        // selected one frame later. The first click on a cell therefore selects the whole number; only a click in
        // the cell that already has the focus (no focus event, so no select) leaves the caret where the user clicked.
        requestAnimationFrame(() => {
          if (document.activeElement === el) el.select();
        });
      }
    },
    () => setEditing(null),
  );
  return (
    <input
      ref={ref}
      id={cellId(field, rowIndex)}
      className="cell-input cell-number tabular-nums"
      type="text"
      inputMode="decimal"
      aria-label={rowLabel(label, rowIndex)}
      {...errorProps(field, rowIndex, error)}
      disabled={disabled}
      value={editing ?? format(value)}
      {...focusProps}
      onChange={(e) => {
        setEditing(e.target.value);
        onCommit(parse(e.target.value));
      }}
    />
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
  const focusProps = useFocusOrigin(selectOnKeyboardFocus, () => setEditing(null));
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
      {...focusProps}
      onChange={(e) => {
        setEditing(e.target.value);
        onCommit(toOptionalNumber(e.target.value) ?? 0);
      }}
    />
  );
}

// Số tấm / Dài / Rộng / Dày: an emptied cell means "no value".
function DimensionCell({
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
  value: number | string;
  onCommit: (value: number | '') => void;
  disabled: boolean;
  error?: string;
}) {
  const focusProps = useFocusOrigin(selectOnKeyboardFocus);
  return (
    <input
      id={cellId(field, rowIndex)}
      className="cell-input cell-number tabular-nums"
      type="number"
      step="any"
      aria-label={rowLabel(label, rowIndex)}
      {...errorProps(field, rowIndex, error)}
      disabled={disabled}
      value={value}
      {...focusProps}
      onChange={(e) => onCommit(toOptionalNumber(e.target.value) ?? '')}
    />
  );
}

function KeyboardGuideItems() {
  return (
    <>
    <span><span className="kbd">Enter</span> Tiếp</span>
    <span><span className="kbd">Shift</span>+<span className="kbd">Enter</span> Lùi</span>
    <span><span className="kbd">↑</span><span className="kbd">↓</span> Dòng trên/dưới</span>
    <span><span className="kbd">Insert</span> Thêm dòng</span>
    <span><span className="kbd">Ctrl</span>+<span className="kbd">Del</span> Xóa dòng</span>
    <span><span className="kbd">Ctrl</span>+<span className="kbd">S</span> Lưu</span>
    <span><span className="kbd">Alt</span>+<span className="kbd">1…4</span> Chuyển tab</span>
    </>
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
  // Element on the tab row to host the toolbar (null while it is not mounted yet). Omitted: the toolbar renders
  // above the grid.
  toolbarSlot?: HTMLElement | null;
}

export function StockLineGrid({ form, type, warehouses, computed, stockAt, readOnly, toolbarSlot }: StockLineGridProps) {
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'lines' });
  const watched = useWatch({ control: form.control, name: 'lines' }) as StockLineFormValues[] | undefined;
  const rows = watched ?? [];

  const lineErrors = form.formState.errors.lines;

  const userId = useAuthStore((s) => s.user?.id ?? '');
  const { prefs, toggleColumn, toggleEnterStop, reset } = useStockGridPrefs(userId, type);
  const headerWarehouseId = useWatch({ control: form.control, name: 'warehouseId' }) ?? '';
  const forced = forcedColumns(rows, headerWarehouseId, lineErrors);
  const isShown = (key: ColumnKey) =>
    !isOptional(key) || !prefs.hiddenColumns.includes(key) || forced.has(key);
  // Remove a row without a focus gap: the focused cell lives in the row being removed, so move the focus to a
  // surviving row BEFORE removing. Its key keeps that row mounted, so the focus stays there and nothing has to
  // re-focus later (a later re-focus would undo a key the user pressed in between). With no line left the focus
  // goes to "Thêm dòng", so Ctrl+S and Alt+n still work and Enter adds a line.
  const removeLine = (rowIndex: number, field: LineFocusField) => {
    const survivor = rowIndex + 1 < fields.length ? rowIndex + 1 : rowIndex - 1;
    if (survivor >= 0) focusCell(verticalTarget(rows[survivor]?.pricingMode ?? 'PerUnit', field, isShown), survivor);
    else document.getElementById('stock-line-add')?.focus();
    remove(rowIndex);
  };
  const columns = COLUMNS.filter((c) => isShown(c.key) && !(readOnly && c.key === 'action'));
  const minWidth = columns.reduce((sum, c) => sum + c.width, 0);

  const stickyOffset = new Map<ColumnKey, CSSProperties>();
  let left = 0;
  for (const c of columns) if (c.sticky === 'left') { stickyOffset.set(c.key, { left }); left += c.width; }
  let right = 0;
  for (const c of [...columns].reverse()) if (c.sticky === 'right') { stickyOffset.set(c.key, { right }); right += c.width; }
  const stickyRight = right; // total width of the right-pinned columns
  const cellClass = (c: GridColumn, extra?: string) =>
    cn(
      c.numeric && 'cell-number',
      c.key === 'row-no' && 'row-no',
      c.key === 'action' && 'cell-action',
      c.sticky === 'left' && 'sticky-left',
      c.sticky === 'right' && 'sticky-right',
      extra,
    );

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

  function handleGridKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (e.defaultPrevented) return;
    if (e.key === 'Insert' && !readOnly && !e.ctrlKey && !e.shiftKey && !e.altKey && !e.metaKey) {
      e.preventDefault();
      addLine();
      return;
    }
    const target = e.target as HTMLElement;
    const noMods = !e.ctrlKey && !e.altKey && !e.shiftKey && !e.metaKey;

    if ((e.key === 'ArrowUp' || e.key === 'ArrowDown') && noMods && target.tagName !== 'SELECT') {
      const current = parseCellId(target.id);
      if (!current) return;
      // Swallow the key even at the first/last row: a number cell would otherwise step its value.
      e.preventDefault();
      const to = current.rowIndex + (e.key === 'ArrowDown' ? 1 : -1);
      if (to < 0 || to >= fields.length) return;
      focusCell(verticalTarget(rows[to]?.pricingMode ?? 'PerUnit', current.field, isVisible), to);
      return;
    }

    if (e.key === 'Delete' && e.ctrlKey && !e.altKey && !e.shiftKey && !e.metaKey && !readOnly) {
      const current = parseCellId(target.id);
      if (!current) return;
      e.preventDefault();
      removeLine(current.rowIndex, current.field);
      return;
    }

    if (e.key !== 'Enter' || e.ctrlKey || e.altKey || e.metaKey) return;
    const current = parseCellId(target.id);
    if (!current) return;
    e.preventDefault();
    moveFocus(current, e.shiftKey ? -1 : 1);
  }


  const numberInput = (idx: number, field: 'sheetCount' | 'length' | 'width' | 'thickness', focusField: LineFocusField, label: string) => {
    const line = rows[idx];
    const disabled = readOnly || isFieldDisabled(line?.pricingMode ?? 'PerUnit', focusField);
    const error = errorOf(idx, field);
    return (
      <>
        <DimensionCell
          field={focusField}
          rowIndex={idx}
          label={label}
          value={(line?.[field] ?? '') as number | string}
          disabled={disabled}
          error={error}
          onCommit={(value) => setLineField(idx, field, value)}
        />
        <CellError id={errorId(focusField, idx)} message={error} />
      </>
    );
  };

  const percentInput = (idx: number, field: 'discountRate' | 'vatRate', focusField: LineFocusField, label: string) => {
    const error = errorOf(idx, field);
    return (
      <>
        <PercentCell
          field={focusField}
          rowIndex={idx}
          label={label}
          value={toOptionalNumber(rows[idx]?.[field])}
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
      </>
    );
  };

  // Content of one cell; the column loop owns the surrounding td.
  const renderCell = (key: ColumnKey, idx: number, line: StockLineFormValues, result: StockLineComputed | undefined) => {
    const mode = line.pricingMode;
    switch (key) {
      case 'row-no':
        return idx + 1;
      case 'product-code':
        return (
          <>
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
                value={line.productCode ?? ''}
                onChange={(v) => setLineField(idx, 'productCode', v)}
                openOnFocus={false}
                onSelect={(s) => {
                  applyProduct(idx, s);
                  const next = nextStop(enterPath(s.pricingMode, prefs.enterStops[s.pricingMode], isVisible), 'product-code');
                  if (next) focusCell(next, idx, true);
                  else setTimeout(() => goNextRow(idx), 0);
                }}
              />
            )}
            <CellError id={errorId('product-code', idx)} message={errorOf(idx, 'productId')} />
          </>
        );
      case 'product-name':
        return (
          <div className="cell-readonly" title={line.productName}>
            {line.productName}
          </div>
        );
      case 'unit':
        return <div className="cell-readonly">{line.unitName}</div>;
      case 'warehouse': {
        const warehouseError = errorOf(idx, 'warehouseId');
        return (
          <>
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
          </>
        );
      }
      case 'sheet-count':
        return numberInput(idx, 'sheetCount', 'sheet-count', 'Số tấm');
      case 'length':
        return numberInput(idx, 'length', 'length', 'Dài');
      case 'width':
        return numberInput(idx, 'width', 'width', 'Rộng');
      case 'thickness':
        return numberInput(idx, 'thickness', 'thickness', 'Dày');
      case 'quantity':
        return (
          <>
            <NumberCell
              field="quantity"
              rowIndex={idx}
              label="Số lượng"
              value={mode === 'PerUnit' ? toOptionalNumber(line.quantity) : result?.quantity}
              format={formatStockQuantity}
              parse={parseQuantityInput}
              onCommit={(v) => setLineField(idx, 'quantity', v ?? 0)}
              disabled={readOnly || isFieldDisabled(mode, 'quantity')}
              error={errorOf(idx, 'quantity')}
            />
            <CellError id={errorId('quantity', idx)} message={errorOf(idx, 'quantity')} />
          </>
        );
      case 'stock-at':
        return (
          <div id={cellId('stock-at', idx)} className="cell-readonly cell-number tabular-nums">
            {!line.productId
              ? ''
              : line.trackInventory
                ? formatStockQuantity(stockAt[`${line.productId}:${line.warehouseId}`])
                : '—'}
          </div>
        );
      case 'unit-price':
        return (
          <>
            <NumberCell
              field="unit-price"
              rowIndex={idx}
              label="Đơn giá"
              value={toOptionalNumber(line.unitPrice)}
              format={formatMoneyForDisplay}
              parse={parseMoneyInput}
              onCommit={(v) => setLineField(idx, 'unitPrice', v ?? 0)}
              disabled={readOnly}
              error={errorOf(idx, 'unitPrice')}
            />
            <CellError id={errorId('unit-price', idx)} message={errorOf(idx, 'unitPrice')} />
          </>
        );
      case 'amount':
        return <div className="cell-readonly cell-number tabular-nums">{fmt.format(result?.amount ?? 0)}</div>;
      case 'discount-rate':
        return percentInput(idx, 'discountRate', 'discount-rate', '%CK');
      case 'discount-amount':
        return (
          <>
            <NumberCell
              field="discount-amount"
              rowIndex={idx}
              label="Tiền CK"
              value={line.discountManual ? toOptionalNumber(line.discountAmount) : result?.discountAmount}
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
          </>
        );
      case 'vat-rate':
        return percentInput(idx, 'vatRate', 'vat-rate', '%VAT');
      case 'vat-amount':
        return <div className="cell-readonly cell-number tabular-nums">{fmt.format(result?.vatAmount ?? 0)}</div>;
      case 'note':
        return (
          <input
            id={cellId('note', idx)}
            className="cell-input"
            aria-label={rowLabel('Ghi chú', idx)}
            disabled={readOnly}
            value={line.note ?? ''}
            onChange={(e) => setLineField(idx, 'note', e.target.value)}
          />
        );
      case 'net-amount':
        return <div className="cell-readonly cell-number tabular-nums">{fmt.format(result?.netAmount ?? 0)}</div>;
      case 'action':
        return (
          <button type="button" aria-label={rowLabel('Xóa', idx)} onClick={() => {
              // The button disappears with its row: move the focus to the line that takes its place.
              removeLine(idx, 'product-code');
            }}>
            <Trash2 className="h-4 w-4 text-red-600" style={{ display: 'inline-block', verticalAlign: 'middle' }} />
          </button>
        );
    }
  };

  // On the tab row (slot) the toolbar has no border or padding of its own; above the grid it is a row of its own.
  const toolbar = (inSlot: boolean) => (
    <div
      className={cn(
        'stock-grid-toolbar flex items-center gap-2',
        inSlot ? 'justify-end' : 'shrink-0 flex-wrap justify-between border-b px-2 py-1.5',
      )}
    >
        <div className="flex items-center gap-2">
          {!readOnly && (
            <button type="button" id="stock-line-add" className="lib-btn" onClick={addLine}>
              <Plus className="h-4 w-4 text-cyan-600" />
              Thêm dòng
            </button>
          )}
          <StockGridSettingsPopover prefs={prefs} onToggleColumn={toggleColumn} onToggleEnterStop={toggleEnterStop} onReset={reset} />
        </div>
        {/* Full guide on wide screens; a compact "Phím tắt" popover below 1860px so the toolbar stays on one line. */}
        <div className="keyboard-guide keyboard-guide-inline">
          <KeyboardGuideItems />
        </div>
        <Popover>
          <PopoverTrigger asChild>
            <Button type="button" variant="outline" size="sm" className="keyboard-guide-trigger h-7">
              <Keyboard className="mr-1.5 h-4 w-4" />
              Phím tắt
            </Button>
          </PopoverTrigger>
          <PopoverContent align="end" className="keyboard-guide keyboard-guide-popover w-auto">
            <KeyboardGuideItems />
          </PopoverContent>
        </Popover>
    </div>
  );

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {toolbarSlot === undefined && toolbar(false)}
      {toolbarSlot && createPortal(toolbar(true), toolbarSlot)}
      {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions -- Grid wrapper owns keyboard shortcuts for editable child inputs. */}
      <div
        className="accounting-grid-wrap stock-grid-wrap min-h-0 flex-1"
        tabIndex={-1}
        onKeyDown={handleGridKeyDown}
        style={{ '--stock-grid-left': `${left}px`, '--stock-grid-right': `${stickyRight}px` } as CSSProperties}
      >
        <table className="accounting-grid stock-grid" style={{ width: minWidth, minWidth }}>
          <colgroup>
            {columns.map((c) => (
              <col key={c.key} style={{ width: c.width }} />
            ))}
          </colgroup>
          <thead>
            <tr>
              {columns.map((c) => (
                <th key={c.key} className={cellClass(c)} style={stickyOffset.get(c.key)}>
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {/* Keyed from the field-array item, not the watched row: right after remove() the watched rows still
                hold the removed one, and keying from them would unmount the last <tr> instead. field.id is
                regenerated on every nested setValue, so the item's own _uiKey is the stable key. */}
            {fields.map((field, idx) => {
              const line = rows[idx] ?? (field as unknown as StockLineFormValues);
              const result = computed[idx];
              return (
                <tr key={(field as { _uiKey?: string })._uiKey ?? field.id}>
                  {columns.map((c) => (
                    <td key={c.key} className={cellClass(c)} style={stickyOffset.get(c.key)}>
                      {renderCell(c.key, idx, line, result)}
                    </td>
                  ))}
                </tr>
              );
            })}
            {fields.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="empty-placeholder">
                  Chưa có dòng nào.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
