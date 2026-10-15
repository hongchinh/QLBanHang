import { format, parseISO } from 'date-fns';

// The line under the title of the overview dashboards: what the revenue is, whose, and for which period.
export function revenueSubtitle(a: { branchName: string | null; ownOnly: boolean; from?: string; to?: string }): string {
  const what = a.ownOnly ? 'Doanh thu xuất bán (phiếu của tôi)' : 'Doanh thu xuất bán';
  const branch = a.branchName ? ` của chi nhánh ${a.branchName}` : '';
  const period =
    a.from && a.to ? `, từ ${format(parseISO(a.from), 'dd/MM/yyyy')} đến ${format(parseISO(a.to), 'dd/MM/yyyy')}` : '';
  return `${what}${branch}${period}.`;
}
