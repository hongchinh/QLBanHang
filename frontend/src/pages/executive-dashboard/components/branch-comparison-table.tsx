import { formatPct } from '@/features/dashboard/format';
import { formatCompactAmount } from '@/features/executive-dashboard/labels';
import type { BranchComparisonRow } from '@/features/executive-dashboard/types';
import { DataTable } from './data-table';
import type { DataColumn } from './data-table';

interface BranchComparisonTableProps {
  rows: BranchComparisonRow[];
  onSelectBranch(branchId: string): void;
}


// The branches side by side; a branch name opens the dashboard of that branch.
export function BranchComparisonTable({ rows, onSelectBranch }: BranchComparisonTableProps) {
  const columns: DataColumn<BranchComparisonRow>[] = [
    {
      key: 'branch',
      header: 'Chi nhánh',
      cell: (row) => (
        <button type="button" onClick={() => onSelectBranch(row.branchId)} className="text-left text-primary hover:underline">
          {row.branchCode} — {row.branchName}
        </button>
      ),
    },
    // A column is null when the user may not see its domain ("—").
    { key: 'revenue', header: 'Doanh thu', align: 'right', cell: (row) => formatCompactAmount(row.revenue) },
    { key: 'margin', header: 'Lãi gộp %', align: 'right', cell: (row) => formatPct(row.marginPct) },
    { key: 'receivable', header: 'Phải thu', align: 'right', cell: (row) => formatCompactAmount(row.receivable) },
    { key: 'stock', header: 'Tồn kho', align: 'right', cell: (row) => formatCompactAmount(row.stockValue) },
  ];
  return <DataTable columns={columns} rows={rows} rowKey={(row) => row.branchId} />;
}
