import { format } from 'date-fns';
import { formatNumber } from '@/features/dashboard/format';
import type { RecentDocument, RecentDocumentKind } from '@/features/executive-dashboard/types';
import { cn } from '@/lib/utils';
import { BranchDocumentLink } from './branch-document-link';

interface RecentFeedProps {
  items: RecentDocument[];
  // Only documents of the working branch link to their screen.
  workingBranchCode: string | undefined;
  // The branch code in front of each line (company scope).
  showBranch: boolean;
}

const TAGS: Record<RecentDocumentKind, { label: string; className: string }> = {
  Receipt: { label: 'Thu', className: 'bg-emerald-500/15 text-emerald-700' },
  Payment: { label: 'Chi', className: 'bg-rose-500/15 text-rose-700' },
  StockIn: { label: 'Nhập', className: 'bg-primary/10 text-primary' },
  StockOut: { label: 'Xuất', className: 'bg-primary/10 text-primary' },
};

function amountText(item: RecentDocument): string {
  const amount = formatNumber(Math.abs(item.amount));
  if (item.kind === 'Receipt') return `+${amount}`;
  if (item.kind === 'Payment') return `-${amount}`;
  return amount;
}

// The documents created today, newest first.
export function RecentFeed({ items, workingBranchCode, showBranch }: RecentFeedProps) {
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground">Hôm nay chưa có chứng từ.</p>;
  }
  return (
    <ul className="divide-y divide-border">
      {items.map((item) => {
        const tag = TAGS[item.kind];
        // The browser runs in VN time (lib/vn-datetime.ts).
        const line = [showBranch ? item.branchCode : null, item.partnerName, item.ownerName, format(new Date(item.createdAt), 'HH:mm')]
          .filter((part): part is string => !!part)
          .join(' · ');
        return (
          <li key={`${item.kind}-${item.id}`} className="flex items-start justify-between gap-2 py-2">
            <div className="min-w-0">
              <div className="flex items-center gap-1.5 text-sm">
                <span className={cn('rounded px-1.5 text-[10px] font-medium', tag.className)}>{tag.label}</span>
                <BranchDocumentLink
                  sourceType={item.kind}
                  sourceId={item.id}
                  code={item.code}
                  branchCode={item.branchCode}
                  workingBranchCode={workingBranchCode}
                />
              </div>
              <p className="truncate text-xs text-muted-foreground" title={line}>
                {line}
              </p>
            </div>
            <span
              className={cn(
                'shrink-0 text-sm font-semibold tabular-nums',
                item.kind === 'Receipt' && 'text-emerald-700',
                item.kind === 'Payment' && 'text-rose-700',
              )}
            >
              {amountText(item)}
            </span>
          </li>
        );
      })}
    </ul>
  );
}
