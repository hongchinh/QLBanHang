import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { formatVnd } from '@/features/dashboard/format';
import { executiveLinks } from '@/features/executive-dashboard/links';
import type { Attention, AttentionItem } from '@/features/executive-dashboard/types';
import { useCanOpenLink } from '@/hooks/useCanOpenLink';
import { cn } from '@/lib/utils';

interface AttentionListProps {
  attention: Attention | undefined;
  today: string;
  from: string;
  to: string;
  // The title of the report links (which branch the report opens for).
  linkTitle?: string;
}

interface Row {
  key: keyof Omit<Attention, 'at'>;
  text: string;
  sub: (item: AttentionItem) => string | null;
  to: string;
  // The dot of an item that needs attention.
  dot: string;
}

const money = (item: AttentionItem) => (item.amount == null ? null : formatVnd(item.amount));

// What needs attention, one row per item the user may see. A row with something to look at opens its report.
export function AttentionList({ attention, today, from, to, linkTitle }: AttentionListProps) {
  const canOpenLink = useCanOpenLink();
  if (!attention) return null;

  const rows: Row[] = [
    {
      key: 'overdueReceivables',
      text: 'Khách nợ quá hạn',
      sub: money,
      to: executiveLinks.debtOverdue('Receivable', today),
      dot: 'bg-rose-500',
    },
    {
      key: 'overCreditLimit',
      text: 'Khách vượt hạn mức nợ',
      sub: (item) => (item.amount == null ? null : `vượt ${formatVnd(item.amount)}`),
      to: executiveLinks.creditLimits(),
      dot: 'bg-rose-500',
    },
    { key: 'negativeStock', text: 'Mã hàng tồn âm', sub: () => null, to: executiveLinks.stockOnHand(), dot: 'bg-amber-500' },
    {
      key: 'payablesDueSoon',
      text: 'Khoản phải trả đến hạn 7 ngày',
      sub: money,
      to: executiveLinks.debtBalance('Payable', from, to),
      dot: 'bg-amber-500',
    },
    { key: 'negativeFunds', text: 'Quỹ âm', sub: money, to: executiveLinks.cashBook(), dot: 'bg-rose-500' },
  ];
  const visible = rows.flatMap((row) => {
    const item = attention[row.key];
    return item ? [{ row, item }] : [];
  });

  if (visible.length === 0) {
    return <p className="text-sm text-muted-foreground">Không có mục nào trong quyền xem của bạn.</p>;
  }

  return (
    <ul className="divide-y divide-border">
      {visible.map(({ row, item }) => {
        const needsAttention = item.count > 0;
        const sub = needsAttention ? row.sub(item) : 'Không có';
        const content: ReactNode = (
          <>
            <span
              aria-hidden="true"
              data-dot
              className={cn('h-2 w-2 shrink-0 rounded-full', needsAttention ? row.dot : 'bg-emerald-500')}
            />
            <b className="min-w-7 text-right text-base font-semibold tabular-nums">{item.count}</b>
            <span className="min-w-0">
              <span className="block text-sm text-foreground">{row.text}</span>
              {sub && <small className="block text-xs text-muted-foreground">{sub}</small>}
            </span>
          </>
        );
        return (
          <li key={row.key}>
            {needsAttention && canOpenLink(row.to) ? (
              <Link to={row.to} title={linkTitle} className="flex items-center gap-2.5 py-2 hover:bg-accent">
                {content}
              </Link>
            ) : (
              <div className="flex items-center gap-2.5 py-2">{content}</div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
