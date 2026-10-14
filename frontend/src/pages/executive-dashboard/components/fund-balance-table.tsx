import { TableCell } from '@/components/ui/table';
import { formatNumber } from '@/features/dashboard/format';
import type { FundBalance } from '@/features/executive-dashboard/types';
import { cn } from '@/lib/utils';
import { DataTable } from './data-table';
import type { DataColumn } from './data-table';

interface FundBalanceTableProps {
  funds: FundBalance[];
  // The branch code in front of each fund (company scope).
  showBranch: boolean;
  // Keep only this many funds: those with the largest balance in absolute value.
  limit?: number;
  // A "Tổng cộng" row with this amount.
  total?: number;
}

const NEGATIVE = 'text-rose-700';

function largest(funds: FundBalance[], limit: number | undefined): FundBalance[] {
  if (limit === undefined) return funds;
  return [...funds].sort((a, b) => Math.abs(b.balance) - Math.abs(a.balance)).slice(0, limit);
}

// The balance of each fund, full numbers as on the cash book.
export function FundBalanceTable({ funds, showBranch, limit, total }: FundBalanceTableProps) {
  const columns: DataColumn<FundBalance>[] = [
    {
      key: 'fund',
      header: 'Quỹ',
      cell: (fund) => `${showBranch ? `${fund.branchCode} · ` : ''}${fund.code ? `${fund.code} – ` : ''}${fund.name}`,
    },
    {
      key: 'balance',
      header: 'Số dư',
      align: 'right',
      cell: (fund) => <span className={cn(fund.balance < 0 && NEGATIVE)}>{formatNumber(fund.balance)}</span>,
    },
  ];
  return (
    <DataTable
      columns={columns}
      rows={largest(funds, limit)}
      rowKey={(fund) => fund.fundId}
      footer={
        total === undefined ? undefined : (
          <>
            <TableCell>Tổng cộng</TableCell>
            <TableCell className={cn('text-right tabular-nums', total < 0 && NEGATIVE)}>{formatNumber(total)}</TableCell>
          </>
        )
      }
    />
  );
}
