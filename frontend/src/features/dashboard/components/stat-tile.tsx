import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useCanOpenLink } from '@/hooks/useCanOpenLink';
import { cn } from '@/lib/utils';
import { formatPct } from '../format';

type HintTone = 'default' | 'good' | 'warn' | 'bad';

interface StatTileProps {
  label: string;
  // null: the figure is not available ("—").
  value: string | null;
  // Change against the previous period in percent. undefined: no badge; null: "—".
  delta?: number | null;
  hint?: ReactNode;
  hintTone?: HintTone;
  // A short note next to the label, e.g. "tạm tính".
  badge?: string;
  to?: string;
  // Tooltip of the link.
  title?: string;
  loading?: boolean;
  // The tile's query failed and has no data.
  error?: boolean;
  onRetry?: () => void;
}

const HINT_TONE: Record<HintTone, string> = {
  default: 'text-muted-foreground',
  good: 'text-emerald-700',
  warn: 'text-amber-700',
  bad: 'text-rose-700',
};

const TILE_CLASS = 'block rounded-lg border border-border bg-card px-3 py-2.5 shadow-sm';

// A compact KPI tile: seven of them fit one row at 1366 px (KpiCard, with its sparkline, does not).
export function StatTile({
  label,
  value,
  delta,
  hint,
  hintTone = 'default',
  badge,
  to,
  title,
  loading,
  error,
  onRetry,
}: StatTileProps) {
  const canOpenLink = useCanOpenLink();

  const content = (
    <>
      <div className="flex items-start justify-between gap-2 text-xs text-muted-foreground">
        <span>{label}</span>
        {badge && <span className="shrink-0 text-amber-700">{badge}</span>}
      </div>
      {loading ? (
        <div className="mt-1 h-7 w-20 animate-pulse rounded bg-muted" />
      ) : (
        <div className="mt-1 text-xl font-semibold leading-7 tracking-tight tabular-nums text-foreground">
          {error ? '—' : (value ?? '—')}
        </div>
      )}
      <div className="mt-0.5 flex min-h-4 flex-wrap items-center gap-x-1.5 text-xs">
        {delta !== undefined && <DeltaBadge delta={delta} />}
        {error ? (
          <button type="button" onClick={() => onRetry?.()} className="text-primary hover:underline">
            Thử lại
          </button>
        ) : (
          hint != null && <span className={HINT_TONE[hintTone]}>{hint}</span>
        )}
      </div>
    </>
  );

  // A failed tile is not a link: its retry button must not sit inside one.
  if (to && !error && canOpenLink(to)) {
    return (
      <Link
        to={to}
        title={title}
        aria-busy={loading || undefined}
        className={cn(TILE_CLASS, 'transition-colors hover:bg-accent')}
      >
        {content}
      </Link>
    );
  }
  return (
    <div aria-busy={loading || undefined} className={TILE_CLASS}>
      {content}
    </div>
  );
}

function DeltaBadge({ delta }: { delta: number | null }) {
  if (delta === null) return <span className="text-muted-foreground">—</span>;
  // Zero counts as a rise, as formatDelta does for the quotation dashboards.
  const rise = delta >= 0;
  return (
    <span className={cn('font-medium', rise ? 'text-emerald-700' : 'text-rose-700')}>
      {rise ? '▲' : '▼'} {formatPct(Math.abs(delta))}
    </span>
  );
}
