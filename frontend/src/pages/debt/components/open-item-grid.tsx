import { useId } from 'react';
import { Link } from 'react-router-dom';
import { CellError, MoneyCell } from '@/components/money-cell/money-cell';
import type { DebtSourceType } from '@/features/cash-common/types';
import type { MatchRow } from '@/features/debt/matching';
import type { OpenItem } from '@/features/debt/types';
import { formatDateOnly } from '@/lib/date-only';
import { roundAwayFromZero } from '@/lib/round';
import '@/pages/quotations/components/line-items-grid.css';

const fmt = new Intl.NumberFormat('vi-VN');

const OVER_OPEN_MESSAGE = 'Vượt số còn lại';
const NEGATIVE_MESSAGE = 'Số tiền không được âm';

// Error of one row's amount; the caller blocks the save while one exists.
function amountError(amount: number | undefined, open: number): string | undefined {
  if (amount === undefined) return undefined;
  if (amount < 0) return NEGATIVE_MESSAGE;
  return amount > open ? OVER_OPEN_MESSAGE : undefined;
}

export interface OpenItemGridProps {
  // Region name and caption.
  title: string;
  // Open items of one partner, side and direction (`useOpenItems`).
  items: OpenItem[];
  // Rows with an amount; the others are left out.
  value: MatchRow[];
  onChange: (rows: MatchRow[]) => void;
  isLoading?: boolean;
  // Screen of the document behind a row; without it the code is plain text.
  docHref?: (sourceType: DebtSourceType, sourceId: string) => string;
}

// One side of a matching: the open documents with a money input per row. A negative amount or one above the
// open amount is shown as an error; the caller blocks the save while one exists.
export function OpenItemGrid({ title, items, value, onChange, isLoading = false, docHref }: OpenItemGridProps) {
  const titleId = useId();
  const amountOf = (entryId: string): number | undefined => value.find((r) => r.entryId === entryId)?.amount;
  const total = roundAwayFromZero(
    value.reduce((sum, row) => sum + row.amount, 0),
    2,
  );

  // Rows keep the order of the items, so pairing follows the grid.
  const setAmount = (entryId: string, amount: number) => {
    onChange(
      items
        .map((item) => ({ entryId: item.entryId, amount: item.entryId === entryId ? amount : (amountOf(item.entryId) ?? 0) }))
        .filter((row) => row.amount !== 0),
    );
  };

  return (
    <section aria-labelledby={titleId} className="min-w-0 space-y-2">
      <h2 id={titleId} className="text-sm font-semibold">
        {title}
      </h2>
      <div className="accounting-grid-wrap">
        <table className="accounting-grid" style={{ minWidth: 560 }}>
          <colgroup>
            <col style={{ width: 120 }} />
            <col style={{ width: 96 }} />
            <col style={{ width: 96 }} />
            <col style={{ width: 120 }} />
            <col style={{ width: 140 }} />
          </colgroup>
          <thead>
            <tr>
              <th>Số CT</th>
              <th>Ngày</th>
              <th>Hạn TT</th>
              <th className="cell-number">Còn lại</th>
              <th className="cell-number">Đối trừ</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const amount = amountOf(item.entryId);
              const inputId = `${titleId}-amount-${item.entryId}`;
              const error = amountError(amount, item.open);
              return (
                <tr key={item.entryId}>
                  <td>
                    <div className="cell-readonly truncate" title={item.description ?? undefined}>
                      {docHref ? (
                        <Link to={docHref(item.sourceType, item.sourceId)} className="text-blue-700 hover:underline">
                          {item.docCode}
                        </Link>
                      ) : (
                        item.docCode
                      )}
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
                    <MoneyCell
                      id={inputId}
                      label={`Đối trừ ${item.docCode}`}
                      value={amount || undefined}
                      onCommit={(v) => setAmount(item.entryId, v ?? 0)}
                      error={error}
                      errorId={`${inputId}-error`}
                    />
                    <CellError id={`${inputId}-error`} message={error} />
                  </td>
                </tr>
              );
            })}
            {items.length === 0 && (
              <tr>
                <td colSpan={5} className="empty-placeholder">
                  {isLoading ? 'Đang tải...' : 'Không có chứng từ.'}
                </td>
              </tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={4} className="text-right font-semibold">
                Tổng đối trừ
              </td>
              <td className="cell-number">
                <div data-testid="open-item-total" className="cell-readonly cell-number tabular-nums font-semibold">
                  {fmt.format(total)}
                </div>
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </section>
  );
}
