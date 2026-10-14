import { Link } from 'react-router-dom';
import type { SalesLeaderboardItem } from '../types';
import { DeltaArrow } from './delta-arrow';
import { formatDelta, formatNumber, formatVnd } from '../format';
import { cn } from '@/lib/utils';

interface SalesLeaderboardProps {
  items: SalesLeaderboardItem[];
  loading?: boolean;
  empty?: string;
  drillHref?: (userId: string) => string;
}

export function SalesLeaderboard({ items, loading, empty, drillHref }: SalesLeaderboardProps) {
  if (loading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-12 animate-pulse rounded bg-muted" />
        ))}
      </div>
    );
  }
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground">{empty ?? 'Chưa có dữ liệu xếp hạng.'}</p>;
  }

  const top = items[0]?.revenue ?? 0;

  return (
    <ol className="space-y-2">
      {items.map((it, idx) => {
        const pct = top > 0 ? Math.max(4, Math.round((it.revenue / top) * 100)) : 0;
        const delta = formatDelta(it.deltaPct);
        const row = (
          <div className="flex items-center gap-3">
            <span className="w-6 text-center text-sm font-semibold text-muted-foreground">{idx + 1}</span>
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-medium text-muted-foreground">
              {(it.fullName || '?').trim().charAt(0).toUpperCase()}
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-baseline justify-between gap-2">
                <span className="truncate text-sm font-medium text-foreground">{it.fullName}</span>
                <span className="shrink-0 text-sm font-semibold tabular-nums text-foreground">
                  {formatVnd(it.revenue)}
                </span>
              </div>
              <div className="mt-1 h-1.5 w-full overflow-hidden rounded bg-muted">
                <div className="h-full bg-foreground/70" style={{ width: `${pct}%` }} />
              </div>
              <div className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-muted-foreground">
                <span>{formatNumber(it.confirmedCount)} BG</span>
                {it.conversionRate != null && <span>CR {it.conversionRate.toFixed(1)}%</span>}
                <span
                  className={cn(
                    'inline-flex items-center gap-0.5',
                    delta.tone === 'positive' && 'text-emerald-600',
                    delta.tone === 'negative' && 'text-rose-600',
                  )}
                >
                  {delta.direction && <DeltaArrow direction={delta.direction} />}
                  {delta.text}
                </span>
              </div>
            </div>
          </div>
        );
        return (
          <li key={it.userId}>
            {drillHref ? (
              <Link to={drillHref(it.userId)} className="block rounded-md p-2 hover:bg-accent">
                {row}
              </Link>
            ) : (
              <div className="p-2">{row}</div>
            )}
          </li>
        );
      })}
    </ol>
  );
}
