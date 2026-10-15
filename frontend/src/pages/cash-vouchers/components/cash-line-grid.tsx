import type { KeyboardEvent } from 'react';
import { useFieldArray, useWatch, type UseFormReturn } from 'react-hook-form';
import { AppIcon } from '@/components/icons';
import { CellError, MoneyCell } from '@/components/money-cell/money-cell';
import { createEmptyCashLine } from '@/features/cash-vouchers/payload';
import type {
  CashLineFormValues,
  CashVoucherFormParsed,
  CashVoucherFormValues,
} from '@/features/cash-vouchers/schema';
import { focusById } from '@/lib/enter-focus';
import { roundAwayFromZero } from '@/lib/round';
import { toNumberOrZero } from '@/lib/to-number';
import { cashLineCellId, cashLineEnterTarget, type CashLineField } from '@/pages/cash-vouchers/utils/cash-navigation';
import '@/pages/quotations/components/line-items-grid.css';

const fmt = new Intl.NumberFormat('vi-VN');

const CELL_ID = /^cash-line-(description|amount|note)-(\d+)$/;

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

  // A new row starts with the header description (section-01 §2.2).
  const addLine = () => {
    append(createEmptyCashLine(form.getValues('description') ?? ''));
    const next = fields.length;
    setTimeout(() => focusById(cashLineCellId('description', next), 'select'), 0);
  };

  function handleGridKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (e.defaultPrevented || readOnly) return;
    if (e.key === 'Insert' && !e.ctrlKey && !e.shiftKey && !e.altKey && !e.metaKey) {
      e.preventDefault();
      addLine();
      return;
    }
    if (e.key !== 'Enter' || e.ctrlKey || e.altKey || e.metaKey || e.nativeEvent.isComposing) return;
    const match = CELL_ID.exec((e.target as HTMLElement).id);
    if (!match) return;
    e.preventDefault();
    const target = cashLineEnterTarget(match[1] as CashLineField, Number(match[2]), rows.length, e.shiftKey);
    if (target.kind === 'cell') focusById(cashLineCellId(target.field, target.index), 'select');
    else if (target.kind === 'append') addLine();
    else if (target.kind === 'header-note') focusById('cash-voucher-note');
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
                      id={cashLineCellId('description', idx)}
                      className="cell-input"
                      aria-label={`Diễn giải dòng ${idx + 1}`}
                      aria-invalid={descriptionError ? true : undefined}
                      aria-describedby={descriptionError ? `${cashLineCellId('description', idx)}-error` : undefined}
                      title={descriptionError}
                      disabled={readOnly}
                      value={line.description ?? ''}
                      onChange={(e) => setLineField(idx, 'description', e.target.value)}
                    />
                    <CellError id={`${cashLineCellId('description', idx)}-error`} message={descriptionError} />
                  </td>
                  <td className="cell-number">
                    <MoneyCell
                      id={cashLineCellId('amount', idx)}
                      label={`Số tiền dòng ${idx + 1}`}
                      value={line.amount === undefined ? undefined : toNumberOrZero(line.amount)}
                      onCommit={(v) => setLineField(idx, 'amount', v ?? 0)}
                      disabled={readOnly}
                      error={amountError}
                      errorId={`${cashLineCellId('amount', idx)}-error`}
                    />
                    <CellError id={`${cashLineCellId('amount', idx)}-error`} message={amountError} />
                  </td>
                  <td>
                    <input
                      id={cashLineCellId('note', idx)}
                      className="cell-input"
                      aria-label={`Ghi chú dòng ${idx + 1}`}
                      aria-invalid={noteError ? true : undefined}
                      title={noteError}
                      disabled={readOnly}
                      value={line.note ?? ''}
                      onChange={(e) => setLineField(idx, 'note', e.target.value)}
                    />
                    <CellError id={`${cashLineCellId('note', idx)}-error`} message={noteError} />
                  </td>
                  {!readOnly && (
                    <td className="cell-action">
                      <button type="button" aria-label={`Xóa dòng ${idx + 1}`} onClick={() => remove(idx)}>
                        <AppIcon
                          name="delete"
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
            <AppIcon name="add" />
            Thêm dòng
          </button>
        </div>
      )}
    </div>
  );
}
