import { formatNumber } from '@/features/dashboard/format';
import { bucketLabel } from '@/features/cash-reports/aging';
import type { AgingBucket } from '@/features/cash-reports/types';
import type { DebtSideSummary } from '@/features/executive-dashboard/types';
import { cn } from '@/lib/utils';
import { DataTable } from './data-table';
import type { DataColumn } from './data-table';

interface DebtTermBarProps {
  side: DebtSideSummary;
  // The overdue buckets of the Nợ quá hạn report; side.overdueAmounts has one amount per bucket.
  buckets: AgingBucket[];
}

interface Term {
  label: string;
  amount: number;
  colour: string;
}

// "Trong hạn", then the overdue buckets from the newest to the oldest.
const COLOURS = ['bg-primary', 'bg-amber-400', 'bg-orange-500', 'bg-rose-500', 'bg-rose-700'];

const COLUMNS: DataColumn<Term & { share: number }>[] = [
  {
    key: 'term',
    header: 'Nhóm',
    // The bar is hidden from assistive technology; the swatch ties a label to its segment on screen.
    cell: (term) => (
      <>
        <span aria-hidden="true" data-swatch className={cn('mr-1.5 inline-block h-2.5 w-2.5 rounded-sm align-middle', term.colour)} />
        {term.label}
      </>
    ),
  },
  { key: 'amount', header: 'Số tiền', align: 'right', cell: (term) => formatNumber(term.amount) },
  { key: 'share', header: '%', align: 'right', cell: (term) => `${term.share}%` },
];

// The open amount of one side split into "not due yet" and the overdue buckets: one bar, then a table.
export function DebtTermBar({ side, buckets }: DebtTermBarProps) {
  const note =
    side.unallocated > 0 ? (
      <p className="text-xs text-muted-foreground">Chưa đối trừ: {formatNumber(side.unallocated)}</p>
    ) : null;

  if (side.totalOpen === 0) {
    return (
      <div className="space-y-2">
        <p className="text-sm text-muted-foreground">Không có công nợ</p>
        {note}
      </div>
    );
  }

  const terms: Term[] = [
    { label: 'Trong hạn', amount: side.notDue, colour: COLOURS[0] },
    ...buckets.map((bucket, index) => ({
      label: `Quá hạn ${bucketLabel(bucket)} ngày`,
      amount: side.overdueAmounts[index] ?? 0,
      // More buckets than colours (custom boundaries) keep the darkest one.
      colour: COLOURS[Math.min(index + 1, COLOURS.length - 1)],
    })),
  ];
  const share = (amount: number) => (amount / side.totalOpen) * 100;

  return (
    <div className="space-y-2">
      <div aria-hidden="true" className="flex h-4 overflow-hidden rounded bg-muted">
        {terms
          .filter((term) => term.amount > 0)
          .map((term) => (
            <div
              key={term.label}
              data-segment={term.label}
              title={term.label}
              className={cn('h-full', term.colour)}
              style={{ width: `${share(term.amount)}%` }}
            />
          ))}
      </div>
      <DataTable
        columns={COLUMNS}
        rows={terms.map((term) => ({ ...term, share: Math.round(share(term.amount)) }))}
        rowKey={(term) => term.label}
      />
      {note}
    </div>
  );
}
