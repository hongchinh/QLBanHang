import { useState, type KeyboardEvent } from 'react';
import { useFieldArray, useWatch, type UseFormReturn } from 'react-hook-form';
import { Plus, Trash2 } from 'lucide-react';
import { createEmptyCashLine } from '@/features/cash-vouchers/payload';
import type {
  CashLineFormValues,
  CashVoucherFormParsed,
  CashVoucherFormValues,
} from '@/features/cash-vouchers/schema';
import { roundAwayFromZero } from '@/lib/round';
import { formatMoneyForDisplay, parseMoneyInput } from '@/pages/quotations/utils/money-input';
import '@/pages/quotations/components/line-items-grid.css';
import { toNumberOrZero } from '@/lib/to-number';

const fmt = new Intl.NumberFormat('vi-VN');

export interface MoneyCellProps {
  id: string;
  label: string;
  value: number | undefined;
  onCommit: (value: number | undefined) => void;
  disabled?: boolean;
  error?: string;
  errorId?: string;
}

// Formatted when idle; raw text while focused. Every keystroke commits the parsed value.
export function MoneyCell({ id, label, value, onCommit, disabled, error, errorId }: MoneyCellProps) {
  const [editing, setEditing] = useState<string | null>(null);
  return (
    <input
      id={id}
      className="cell-input cell-number tabular-nums"
      type="text"
      inputMode="decimal"
      aria-label={label}
      aria-invalid={error ? true : undefined}
      aria-describedby={error ? errorId : undefined}
      title={error}
      disabled={disabled}
      value={editing ?? formatMoneyForDisplay(value)}
      onFocus={(e) => {
        const el = e.currentTarget;
        setEditing(value === undefined ? '' : String(value));
        requestAnimationFrame(() => el.select());
      }}
      onChange={(e) => {
        setEditing(e.target.value);
        onCommit(parseMoneyInput(e.target.value));
      }}
      onBlur={() => setEditing(null)}
    />
  );
}

export function CellError({ id, message }: { id: string; message?: string }) {
  if (!message) return null;
  return (
    <div id={id} className="px-1 text-[11px] leading-tight text-destructive">
      {message}
    </div>
  );
}

function cellId(field: 'description' | 'amount' | 'note', idx: number): string {
  return `cash-line-${field}-${idx}`;
}

export interface CashLineGridProps {
  form: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed>;
  readOnly: boolean;
}

export function CashLineGrid({ form, readOnly }: CashLineGridProps) {
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'lines' });
  const watched = useWatch({ control: form.control, name: 'lines' }) as CashLineFormValues[] | undefined;
  const rows = watched ?? [];
  const lineErrors = form.formState.errors.lines;
  // Display only: the server computes Total = Σ lines.
  const total = roundAwayFromZero(
    rows.reduce((sum, row) => sum + toNumberOrZero(row?.amount), 0),
    2,
  );

  const errorOf = (idx: number, field: keyof CashLineFormValues): string | undefined => {
    const lineError = lineErrors?.[idx] as Record<string, { message?: string } | undefined> | undefined;
    return lineError?.[field]?.message;
  };

  const setLineField = <K extends keyof CashLineFormValues>(idx: number, field: K, value: CashLineFormValues[K]) => {
    form.setValue(`lines.${idx}.${field}` as const, value as never, { shouldDirty: true });
  };

  const addLine = () => {
    append(createEmptyCashLine());
    const next = fields.length;
    setTimeout(() => document.getElementById(cellId('description', next))?.focus(), 0);
  };

  function handleGridKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (e.defaultPrevented || readOnly) return;
    if (e.key === 'Insert' && !e.ctrlKey && !e.shiftKey && !e.altKey && !e.metaKey) {
      e.preventDefault();
      addLine();
    }
  }

  // "Phiếu phải có ít nhất 1 dòng" sits on the array itself.
  const arrayError = lineErrors?.root?.message ?? lineErrors?.message;

  return (
    <div className="space-y-2">
      {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions -- Grid wrapper owns keyboard shortcuts for editable child inputs. */}
      <div className="accounting-grid-wrap" tabIndex={-1} onKeyDown={handleGridKeyDown}>
        <table className="accounting-grid" style={{ minWidth: 640 }}>
          <colgroup>
            <col style={{ width: 42 }} />
            <col style={{ minWidth: 260 }} />
            <col style={{ width: 150 }} />
            <col style={{ minWidth: 180 }} />
            {!readOnly && <col style={{ width: 42 }} />}
          </colgroup>
          <thead>
            <tr>
              <th className="row-no">#</th>
              <th>Diễn giải</th>
              <th className="cell-number">Số tiền</th>
              <th>Ghi chú</th>
              {!readOnly && <th></th>}
            </tr>
          </thead>
          <tbody>
            {fields.map((field, idx) => {
              const line = rows[idx] ?? (field as unknown as CashLineFormValues);
              const descriptionError = errorOf(idx, 'description');
              const amountError = errorOf(idx, 'amount');
              const noteError = errorOf(idx, 'note');
              return (
                <tr key={line._uiKey ?? field.id}>
                  <td className="row-no">{idx + 1}</td>
                  <td>
                    <input
                      id={cellId('description', idx)}
                      className="cell-input"
                      aria-label={`Diễn giải dòng ${idx + 1}`}
                      aria-invalid={descriptionError ? true : undefined}
                      aria-describedby={descriptionError ? `${cellId('description', idx)}-error` : undefined}
                      title={descriptionError}
                      disabled={readOnly}
                      value={line.description ?? ''}
                      onChange={(e) => setLineField(idx, 'description', e.target.value)}
                    />
                    <CellError id={`${cellId('description', idx)}-error`} message={descriptionError} />
                  </td>
                  <td className="cell-number">
                    <MoneyCell
                      id={cellId('amount', idx)}
                      label={`Số tiền dòng ${idx + 1}`}
                      value={line.amount === undefined ? undefined : toNumberOrZero(line.amount)}
                      onCommit={(v) => setLineField(idx, 'amount', v ?? 0)}
                      disabled={readOnly}
                      error={amountError}
                      errorId={`${cellId('amount', idx)}-error`}
                    />
                    <CellError id={`${cellId('amount', idx)}-error`} message={amountError} />
                  </td>
                  <td>
                    <input
                      id={cellId('note', idx)}
                      className="cell-input"
                      aria-label={`Ghi chú dòng ${idx + 1}`}
                      aria-invalid={noteError ? true : undefined}
                      title={noteError}
                      disabled={readOnly}
                      value={line.note ?? ''}
                      onChange={(e) => setLineField(idx, 'note', e.target.value)}
                    />
                    <CellError id={`${cellId('note', idx)}-error`} message={noteError} />
                  </td>
                  {!readOnly && (
                    <td className="cell-action">
                      <button type="button" aria-label={`Xóa dòng ${idx + 1}`} onClick={() => remove(idx)}>
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
                <td colSpan={readOnly ? 4 : 5} className="empty-placeholder">
                  Chưa có dòng nào.
                </td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={2} className="text-right font-semibold">
                Tổng
              </td>
              <td className="cell-number">
                <div data-testid="cash-lines-total" className="cell-readonly cell-number tabular-nums font-semibold">
                  {fmt.format(total)}
                </div>
              </td>
              <td colSpan={readOnly ? 1 : 2}></td>
            </tr>
          </tfoot>
        </table>
      </div>

      {arrayError && <p className="text-sm text-destructive">{arrayError}</p>}

      {!readOnly && (
        <div className="line-items-toolbar">
          <button type="button" className="lib-btn" onClick={addLine}>
            <Plus className="h-4 w-4 text-cyan-600" />
            Thêm dòng
          </button>
        </div>
      )}
    </div>
  );
}
