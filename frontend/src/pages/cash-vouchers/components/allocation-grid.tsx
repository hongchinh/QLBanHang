import { allocationTotal, suggestAllocations } from '@/features/cash-vouchers/allocation';
import type { CashAllocationFormValues } from '@/features/cash-vouchers/schema';
import type { OpenItem } from '@/features/debt/types';
import { roundAwayFromZero } from '@/lib/round';
import { cn } from '@/lib/utils';
import { CellError, MoneyCell } from './cash-line-grid';
import '@/pages/quotations/components/line-items-grid.css';

const fmt = new Intl.NumberFormat('vi-VN');

// `yyyy-MM-dd` (DateOnly) → `dd/MM/yyyy`, without going through Date (no time-zone shift).
function formatDateOnly(value?: string | null): string {
  if (!value) return '';
  const [y, m, d] = value.split('-');
  return `${d}/${m}/${y}`;
}

function toNum(v: unknown): number {
  const n = typeof v === 'number' ? v : Number(v);
  return Number.isFinite(n) ? n : 0;
}

export interface AllocationGridProps {
  // Open items of the partner and side (`useOpenItems`, with `forSource` on edit so the room this voucher uses is added back).
  items: OpenItem[];
  // The voucher's AtVoucher allocations; rows without an amount are left out.
  value: CashAllocationFormValues[];
  // Voucher total (Σ lines).
  total: number;
  // A user edit (typing or "Bỏ phân bổ").
  onChange: (rows: CashAllocationFormValues[]) => void;
  // "Gợi ý": earliest due first, filling total − otherSettled.
  onSuggest: (rows: CashAllocationFormValues[]) => void;
  readOnly: boolean;
  // The voucher's own Manual/Offset settlements (edit only); they use part of the voucher amount.
  otherSettled: number;
}

export function AllocationGrid({ items, value, total, onChange, onSuggest, readOnly, otherSettled }: AllocationGridProps) {
  const amountOf = (entryId: string): number | undefined => {
    const row = value.find((r) => r.entryId === entryId);
    return row === undefined ? undefined : toNum(row.amount);
  };

  const allocated = allocationTotal(value.map((row) => ({ amount: toNum(row.amount) })));
  const capacity = roundAwayFromZero(total - otherSettled, 2);
  const remaining = roundAwayFromZero(capacity - allocated, 2);
  const overAllocated = remaining < 0;

  const setAmount = (entryId: string, amount: number) => {
    const next = items
      .map((item) => ({
        entryId: item.entryId,
        docCode: item.docCode,
        amount: item.entryId === entryId ? amount : (amountOf(item.entryId) ?? 0),
      }))
      .filter((row) => row.amount !== 0);
    onChange(next);
  };

  const suggest = () => {
    onSuggest(
      suggestAllocations(
        items.map((item) => ({ ...item, dueDate: item.dueDate ?? null })),
        capacity,
      ),
    );
  };

  return (
    <div className="space-y-2">
      <div className="accounting-grid-wrap">
        <table className="accounting-grid" style={{ minWidth: 600 }}>
          <colgroup>
            <col style={{ width: 120 }} />
            <col style={{ width: 100 }} />
            <col style={{ width: 100 }} />
            <col style={{ width: 140 }} />
            <col style={{ width: 160 }} />
          </colgroup>
          <thead>
            <tr>
              <th>Số CT</th>
              <th>Ngày CT</th>
              <th>Hạn TT</th>
              <th className="cell-number">Còn nợ</th>
              <th className="cell-number">Phân bổ</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const amount = amountOf(item.entryId);
              const inputId = `allocation-amount-${item.entryId}`;
              const error = amount !== undefined && amount > item.open ? 'Phân bổ vượt số còn nợ' : undefined;
              return (
                <tr key={item.entryId}>
                  <td>
                    <div className="cell-readonly" title={item.description ?? undefined}>
                      {item.docCode}
                    </div>
                  </td>
                  <td>
                    <div className="cell-readonly">{formatDateOnly(item.docDate)}</div>
                  </td>
                  <td>
                    <div className="cell-readonly">{formatDateOnly(item.dueDate)}</div>
                  </td>
                  <td className="cell-number">
                    <div className="cell-readonly cell-number tabular-nums">{fmt.format(item.open)}</div>
                  </td>
                  <td className="cell-number">
                    {readOnly ? (
                      <div className="cell-readonly cell-number tabular-nums">
                        {amount ? fmt.format(amount) : ''}
                      </div>
                    ) : (
                      <>
                        <MoneyCell
                          id={inputId}
                          label={`Phân bổ ${item.docCode}`}
                          value={amount || undefined}
                          onCommit={(v) => setAmount(item.entryId, v ?? 0)}
                          error={error}
                          errorId={`${inputId}-error`}
                        />
                        <CellError id={`${inputId}-error`} message={error} />
                      </>
                    )}
                  </td>
                </tr>
              );
            })}
            {items.length === 0 && (
              <tr>
                <td colSpan={5} className="empty-placeholder">
                  Không có chứng từ còn nợ.
                </td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={4} className="text-right font-semibold">
                Tổng phân bổ
              </td>
              <td className="cell-number">
                <div data-testid="allocation-total" className="cell-readonly cell-number tabular-nums font-semibold">
                  {fmt.format(allocated)}
                </div>
              </td>
            </tr>
          </tfoot>
        </table>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
        <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <span>
            Chưa phân bổ:{' '}
            <span
              data-testid="allocation-remaining"
              className={cn('font-semibold tabular-nums', overAllocated && 'text-destructive')}
            >
              {fmt.format(remaining)}
            </span>
          </span>
          {otherSettled !== 0 && (
            <span className="text-muted-foreground tabular-nums">
              Đã đối trừ ở màn khác: {fmt.format(otherSettled)}
            </span>
          )}
          {overAllocated && (
            <span role="alert" className="text-destructive">
              Tổng phân bổ vượt số tiền phiếu
            </span>
          )}
        </div>
        {!readOnly && (
          <div className="line-items-toolbar">
            <button type="button" className="lib-btn" onClick={suggest} disabled={items.length === 0}>
              Gợi ý
            </button>
            <button type="button" className="lib-btn" onClick={() => onChange([])} disabled={value.length === 0}>
              Bỏ phân bổ
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
