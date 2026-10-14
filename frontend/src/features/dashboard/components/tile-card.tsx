import { useId } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { useCanOpenLink } from '@/hooks/useCanOpenLink';
import { getErrorMessage } from '@/lib/api-client';
import { cn } from '@/lib/utils';

// The part of a query result the tile reads (a TanStack query result fits).
export interface TileQuery {
  isLoading: boolean;
  isError: boolean;
  error?: unknown;
  data?: unknown;
  // The data on screen belongs to the previous query key (keepPreviousData) while the new key loads.
  isPlaceholderData?: boolean;
  refetch: () => unknown;
}

interface TileAction {
  label: string;
  // A link (`to`) or a button (`onClick`).
  to?: string;
  onClick?: () => void;
  title?: string;
}

interface TileCardProps {
  title: string;
  action?: TileAction;
  query?: TileQuery;
  isEmpty?: boolean;
  emptyText?: string;
  className?: string;
  children: ReactNode;
}

const ACTION_CLASS = 'shrink-0 text-xs font-normal text-primary hover:underline';

// A dashboard tile: title, optional action, and the loading / error / empty states of its query.
export function TileCard({ title, action, query, isEmpty, emptyText, className, children }: TileCardProps) {
  const titleId = useId();
  const stale = query?.isPlaceholderData === true;
  return (
    <section
      aria-labelledby={titleId}
      aria-busy={query?.isLoading || stale || undefined}
      className={cn('rounded-xl border border-border bg-card p-4 shadow-sm', className)}
    >
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 id={titleId} className="text-sm font-semibold text-foreground">
          {title}
        </h2>
        {action && <TileActionControl action={action} />}
      </div>
      {/* Always this element, dimmed or not: the children (a toggle, a chart) stay mounted when the key changes. */}
      <div className={cn(stale && 'opacity-60')}>
        <TileBody query={query} isEmpty={isEmpty} emptyText={emptyText}>
          {children}
        </TileBody>
      </div>
    </section>
  );
}

function TileActionControl({ action }: { action: TileAction }) {
  const canOpenLink = useCanOpenLink();
  const { label, to, onClick, title } = action;
  if (to) {
    // Without access the link would land on /403: leave it out.
    if (!canOpenLink(to)) return null;
    return (
      <Link to={to} title={title} className={ACTION_CLASS}>
        {label}
      </Link>
    );
  }
  if (!onClick) return null;
  return (
    <button type="button" title={title} onClick={() => onClick()} className={ACTION_CLASS}>
      {label}
    </button>
  );
}

// The error of a query and its retry button; also for a second query a tile reads.
export function TileError({ query }: { query: Pick<TileQuery, 'error' | 'refetch'> }) {
  return (
    <div role="status" className="text-sm text-rose-700">
      <p>{query.error != null ? getErrorMessage(query.error) : 'Không tải được dữ liệu.'}</p>
      <Button type="button" variant="outline" size="sm" className="mt-2 text-foreground" onClick={() => query.refetch()}>
        Thử lại
      </Button>
    </div>
  );
}

function TileBody({
  query,
  isEmpty,
  emptyText,
  children,
}: Pick<TileCardProps, 'query' | 'isEmpty' | 'emptyText' | 'children'>) {
  // The first load only: a later key shows the previous data (isPlaceholderData) until its own arrives.
  if (query?.isLoading) {
    return <div className="h-24 animate-pulse rounded bg-muted" />;
  }
  // Only a failure with nothing to show: a failed background refetch keeps the last data on screen.
  if (query?.isError && query.data === undefined) {
    return <TileError query={query} />;
  }
  if (isEmpty) {
    return <p className="text-sm text-muted-foreground">{emptyText ?? 'Chưa có dữ liệu.'}</p>;
  }
  return <>{children}</>;
}
