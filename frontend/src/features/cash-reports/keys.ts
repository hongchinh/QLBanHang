import { cashKeys } from '@/features/cash-common/keys';
import type {
  CashBookParams,
  DebtAgingParams,
  DebtBalanceParams,
  DebtBySalespersonParams,
  DebtDocumentsParams,
  DebtLedgerParams,
  DebtOverdueParams,
  DebtReconciliationParams,
} from './types';

// Under the cash root (P28), so cash, debt and stock voucher saves refresh every report.
export const cashReportKeys = {
  all: [...cashKeys.all, 'reports'] as const,
  debtBalance: (params: DebtBalanceParams) => [...cashReportKeys.all, 'debt-balance', params] as const,
  debtLedger: (params: DebtLedgerParams) => [...cashReportKeys.all, 'debt-ledger', params] as const,
  debtDocuments: (params: DebtDocumentsParams) => [...cashReportKeys.all, 'debt-documents', params] as const,
  debtAging: (params: DebtAgingParams) => [...cashReportKeys.all, 'debt-aging', params] as const,
  debtOverdue: (params: DebtOverdueParams) => [...cashReportKeys.all, 'debt-overdue', params] as const,
  debtBySalesperson: (params: DebtBySalespersonParams) =>
    [...cashReportKeys.all, 'debt-by-salesperson', params] as const,
  debtReconciliation: (params: DebtReconciliationParams) =>
    [...cashReportKeys.all, 'debt-reconciliation', params] as const,
  cashBook: (params: CashBookParams) => [...cashReportKeys.all, 'cash-book', params] as const,
};
