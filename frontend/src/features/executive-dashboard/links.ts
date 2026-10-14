import type { DebtSide } from '@/features/cash-common/types';
import type { ExecutiveScopeMode } from './types';

// Links from the dashboard to the report pages, with the parameters those pages read from the URL.
// The cash book and stock on hand keep their filters in component state, so they open without parameters.
export const executiveLinks = {
  debtOverdue: (side: DebtSide, at: string, partnerId?: string) =>
    `/reports/debt-overdue?side=${side}&at=${at}${partnerId ? `&partnerId=${partnerId}` : ''}`,
  debtAging: (side: DebtSide, at: string) => `/reports/debt-aging?side=${side}&at=${at}`,
  debtBalance: (side: DebtSide, from: string, to: string) => `/reports/debt-balance?side=${side}&from=${from}&to=${to}`,
  debtBySalesperson: (from: string, to: string) => `/reports/debt-by-salesperson?side=Receivable&from=${from}&to=${to}`,
  cashBook: () => '/reports/cash-book',
  stockOnHand: () => '/inventory/stock-on-hand',
  creditLimits: () => '/credit-limits',
};

// The hint of a link when the dashboard shows more than the page it opens will: the reports, stock on hand and
// the credit limits follow the working branch, the dashboard may show the whole company. Undefined when both
// show the same.
export function reportLinkTitle(
  mode: ExecutiveScopeMode,
  branchCount: number,
  workingBranchCode?: string,
): string | undefined {
  if (mode !== 'company' || branchCount <= 1) return undefined;
  const hint = 'Báo cáo mở theo chi nhánh đang làm việc';
  return workingBranchCode ? `${hint} (${workingBranchCode})` : hint;
}
