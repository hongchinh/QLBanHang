import { apiGet } from '@/lib/api-client';
import type {
  CashBookParams,
  CashBookReport,
  DebtBalanceParams,
  DebtBalanceReport,
  DebtLedgerParams,
  DebtLedgerReport,
} from './types';

export const cashReportsApi = {
  debtBalance: (params: DebtBalanceParams) => apiGet<DebtBalanceReport>('/reports/debt-balance', params),
  debtLedger: (params: DebtLedgerParams) => apiGet<DebtLedgerReport>('/reports/debt-ledger', params),
  cashBook: (params: CashBookParams) => apiGet<CashBookReport>('/reports/cash-book', params),
};
