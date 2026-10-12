import type { DebtSide, DebtSourceType } from '@/features/cash-common/types';
import type { CashBookSourceType } from '@/features/cash-reports/types';
import { formatCurrencyVnd } from '@/lib/utils';

// Screen of a document shown in the debt ledger or the cash book.
export function documentLink(sourceType: DebtSourceType | CashBookSourceType, sourceId: string): string {
  switch (sourceType) {
    case 'StockIn':
      return `/stock-in/${sourceId}`;
    case 'StockOut':
      return `/stock-out/${sourceId}`;
    case 'Receipt':
      return `/receipts/${sourceId}`;
    case 'Payment':
      return `/payments/${sourceId}`;
    case 'Offset':
      return `/debt/offsets/${sourceId}`;
    case 'Opening':
      return '/debt/opening';
    case 'TransferIn':
    case 'TransferOut':
      return `/fund-transfers/${sourceId}`;
  }
}

export const DEBT_SOURCE_TYPE_LABELS: Record<DebtSourceType, string> = {
  Opening: 'Nợ đầu kỳ',
  StockIn: 'Phiếu nhập',
  StockOut: 'Phiếu xuất',
  Receipt: 'Phiếu thu',
  Payment: 'Phiếu chi',
  Offset: 'Bù trừ công nợ',
};

// Empty cell for a zero movement, so the columns stay readable.
export function formatAmountCell(value: number): string {
  return value ? formatCurrencyVnd(value) : '';
}

// Balance already split by the server into Dư Nợ / Dư Có (P24).
export function formatDebitCredit(debit: number, credit: number): string {
  if (debit) return `Nợ ${formatCurrencyVnd(debit)}`;
  if (credit) return `Có ${formatCurrencyVnd(credit)}`;
  return '0';
}

// A running balance (Σ increase − Σ decrease) as Dư Nợ / Dư Có (P24, mirrors DebtBalanceSign.Split):
// Receivable +x is Nợ and −x Có; Payable +x is Có and −x Nợ.
export function formatSignedBalance(side: DebtSide, balance: number): string {
  const positive = Math.max(balance, 0);
  const negative = Math.max(-balance, 0);
  return side === 'Receivable' ? formatDebitCredit(positive, negative) : formatDebitCredit(negative, positive);
}

export const NUMERIC_CELL = 'px-2 py-2 text-right tabular-nums';

// Side from a search param; anything else falls back to receivable.
export function parseDebtSide(value: string | null): DebtSide {
  return value === 'Payable' ? 'Payable' : 'Receivable';
}
