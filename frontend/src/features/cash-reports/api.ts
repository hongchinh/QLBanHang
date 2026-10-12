import { apiGet } from '@/lib/api-client';
import type {
  CashBookParams,
  CashBookReport,
  DebtAgingParams,
  DebtAgingReport,
  DebtBalanceParams,
  DebtBalanceReport,
  DebtDocumentsParams,
  DebtDocumentsReport,
  DebtLedgerParams,
  DebtLedgerReport,
  DebtOverdueParams,
  DebtOverdueReport,
} from './types';

export const cashReportsApi = {
  debtBalance: (params: DebtBalanceParams) => apiGet<DebtBalanceReport>('/reports/debt-balance', params),
  debtLedger: (params: DebtLedgerParams) => apiGet<DebtLedgerReport>('/reports/debt-ledger', params),
  debtDocuments: (params: DebtDocumentsParams) => apiGet<DebtDocumentsReport>('/reports/debt-documents', params),
  debtAging: (params: DebtAgingParams) => apiGet<DebtAgingReport>('/reports/debt-aging', params),
  debtOverdue: (params: DebtOverdueParams) => apiGet<DebtOverdueReport>('/reports/debt-overdue', params),
  cashBook: (params: CashBookParams) => apiGet<CashBookReport>('/reports/cash-book', params),
};
