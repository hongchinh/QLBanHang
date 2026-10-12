import type { DebtLedgerReport } from '@/features/cash-reports/types';
import { formatDateOnly } from '@/lib/date-only';
import { formatCurrencyVnd } from '@/lib/utils';
import {
  DEBT_SOURCE_TYPE_LABELS,
  NUMERIC_CELL,
  formatAmountCell,
  formatDebitCredit,
  formatSignedBalance,
} from './document-link';
import { DocumentCodeLink } from './document-code-link';

// Sổ chi tiết công nợ of one partner and side: opening row, entries, Cộng phát sinh and closing.
// Shared by the ledger page and the reconciliation page.
export function DebtLedgerTable({ ledger }: { ledger: DebtLedgerReport }) {
  return (
    <table className="w-full min-w-[900px] text-sm">
      <thead>
        <tr className="border-b text-left text-muted-foreground">
          <th className="px-2 py-2">Ngày</th>
          <th className="px-2 py-2">Số CT</th>
          <th className="px-2 py-2">Loại CT</th>
          <th className="px-2 py-2">Diễn giải</th>
          <th className="px-2 py-2 text-right">Tăng</th>
          <th className="px-2 py-2 text-right">Giảm</th>
          <th className="px-2 py-2 text-right">Số dư</th>
        </tr>
      </thead>
      <tbody>
        <tr className="border-b font-medium">
          <td colSpan={6} className="px-2 py-2">
            Số dư đầu kỳ
          </td>
          <td className={NUMERIC_CELL}>{formatDebitCredit(ledger.openingDebit, ledger.openingCredit)}</td>
        </tr>
        {ledger.rows.map((row) => (
          <tr key={row.entryId} className="border-b">
            <td className="whitespace-nowrap px-2 py-2">{formatDateOnly(row.docDate)}</td>
            <td className="px-2 py-2">
              <DocumentCodeLink sourceType={row.sourceType} sourceId={row.sourceId} code={row.docCode} />
            </td>
            <td className="px-2 py-2">{DEBT_SOURCE_TYPE_LABELS[row.sourceType]}</td>
            <td className="px-2 py-2">{row.description}</td>
            <td className={NUMERIC_CELL}>{formatAmountCell(row.increase)}</td>
            <td className={NUMERIC_CELL}>{formatAmountCell(row.decrease)}</td>
            <td className={NUMERIC_CELL}>{formatSignedBalance(ledger.side, row.runningBalance)}</td>
          </tr>
        ))}
        {ledger.rows.length === 0 && (
          <tr className="border-b">
            <td colSpan={7} className="px-2 py-6 text-center text-muted-foreground">
              Không có phát sinh trong khoảng thời gian này
            </td>
          </tr>
        )}
      </tbody>
      <tfoot>
        <tr className="border-b font-semibold">
          <td colSpan={4} className="px-2 py-2">
            Cộng phát sinh
          </td>
          <td className={NUMERIC_CELL}>{formatCurrencyVnd(ledger.totalIncrease)}</td>
          <td className={NUMERIC_CELL}>{formatCurrencyVnd(ledger.totalDecrease)}</td>
          <td />
        </tr>
        <tr className="font-semibold">
          <td colSpan={6} className="px-2 py-2">
            Số dư cuối kỳ
          </td>
          <td className={NUMERIC_CELL}>{formatDebitCredit(ledger.closingDebit, ledger.closingCredit)}</td>
        </tr>
      </tfoot>
    </table>
  );
}
