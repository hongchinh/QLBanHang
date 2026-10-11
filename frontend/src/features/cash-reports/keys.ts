import { cashKeys } from '@/features/cash-common/keys';
import type { CashBookParams, DebtBalanceParams, DebtLedgerParams } from './types';

// Under the cash root (P28), so cash, debt and stock voucher saves refresh every report.
export const cashReportKeys = {
  all: [...cashKeys.all, 'reports'] as const,
  debtBalance: (params: DebtBalanceParams) => [...cashReportKeys.all, 'debt-balance', params] as const,
  debtLedger: (params: DebtLedgerParams) => [...cashReportKeys.all, 'debt-ledger', params] as const,
  cashBook: (params: CashBookParams) => [...cashReportKeys.all, 'cash-book', params] as const,
};
