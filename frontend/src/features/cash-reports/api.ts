import { apiGet } from '@/lib/api-client';
import type {
  CashBookParams,
  CashBookReport,
  DebtAgingParams,
  DebtAgingReport,
  DebtBalanceParams,
  DebtBalanceReport,
  DebtBySalespersonParams,
  DebtBySalespersonReport,
  DebtDocumentsParams,
  DebtDocumentsReport,
  DebtLedgerParams,
  DebtLedgerReport,
  DebtOverdueParams,
  DebtOverdueReport,
  DebtReconciliationParams,
  DebtReconciliationReport,
} from './types';

export const cashReportsApi = {
  debtBalance: (params: DebtBalanceParams) => apiGet<DebtBalanceReport>('/reports/debt-balance', params),
  debtLedger: (params: DebtLedgerParams) => apiGet<DebtLedgerReport>('/reports/debt-ledger', params),
  debtDocuments: (params: DebtDocumentsParams) => apiGet<DebtDocumentsReport>('/reports/debt-documents', params),
  debtAging: (params: DebtAgingParams) => apiGet<DebtAgingReport>('/reports/debt-aging', params),
  debtOverdue: (params: DebtOverdueParams) => apiGet<DebtOverdueReport>('/reports/debt-overdue', params),
  debtBySalesperson: (params: DebtBySalespersonParams) =>
    apiGet<DebtBySalespersonReport>('/reports/debt-by-salesperson', params),
  debtReconciliation: (params: DebtReconciliationParams) =>
    apiGet<DebtReconciliationReport>('/reports/debt-reconciliation', params),
  cashBook: (params: CashBookParams) => apiGet<CashBookReport>('/reports/cash-book', params),
};
