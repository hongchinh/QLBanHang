import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useCanOpenLink } from '@/hooks/useCanOpenLink';
import { cn } from '@/lib/utils';
import { formatPct } from '../format';
import { DeltaArrow } from './delta-arrow';

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
  // The figures belong to the previous period or scope while the new ones load: dimmed.
  stale?: boolean;
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
const STALE_CLASS = 'opacity-60';

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
  stale,
  error,
  onRetry,
}: StatTileProps) {
  const canOpenLink = useCanOpenLink();
  const busy = loading || stale || undefined;

  // Seven tiles share a row at 1366 px. A label that does not fit is cut on its line (the title carries the
  // full text): wrapping would push this tile's value below the others. The hint sits under the value, so it
  // wraps instead and no figure in it is hidden; the tiles of a row stretch to the tallest.
  const content = (
    <>
      <div className="flex items-start justify-between gap-2 text-xs text-muted-foreground">
        <span className="min-w-0 truncate" title={label}>
          {label}
        </span>
        {badge && <span className="shrink-0 text-amber-700">{badge}</span>}
      </div>
      {loading ? (
        <div className="mt-1 h-7 w-20 animate-pulse rounded bg-muted" />
      ) : (
        <div
          className={cn(
            'mt-1 text-xl font-semibold leading-7 tracking-tight tabular-nums text-foreground',
            stale && STALE_CLASS,
          )}
        >
          {error ? '—' : (value ?? '—')}
        </div>
      )}
      {/* Flowing text: the hint goes on next to the delta and wraps under it. */}
      <div className={cn('mt-0.5 min-h-4 text-xs leading-4', stale && STALE_CLASS)}>
        {delta !== undefined && <DeltaBadge delta={delta} />}
        {error ? (
          <button type="button" onClick={() => onRetry?.()} className="text-primary hover:underline">
            Thử lại
          </button>
        ) : (
          hint != null && (
            <span className={HINT_TONE[hintTone]} title={typeof hint === 'string' ? hint : undefined}>
              {hint}
            </span>
          )
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
        aria-busy={busy}
        className={cn(TILE_CLASS, 'transition-colors hover:bg-accent')}
      >
        {content}
      </Link>
    );
  }
  return (
    // A group named by its label, so the tile can be found and announced as one.
    <div role="group" aria-label={label} aria-busy={busy} className={TILE_CLASS}>
      {content}
    </div>
  );
}

function DeltaBadge({ delta }: { delta: number | null }) {
  // In one piece, with a gap before the hint.
  if (delta === null) return <span className="mr-1.5 whitespace-nowrap text-muted-foreground">—</span>;
  // Zero counts as a rise, as formatDelta does for the quotation dashboards.
  const rise = delta >= 0;
  return (
    <span
      className={cn('mr-1.5 inline-flex items-center gap-0.5 whitespace-nowrap font-medium', rise ? 'text-emerald-700' : 'text-rose-700')}
    >
      <DeltaArrow direction={rise ? 'up' : 'down'} />
      {formatPct(Math.abs(delta))}
    </span>
  );
}
