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
  return (
    <section aria-labelledby={titleId} className={cn('rounded-xl border border-border bg-card p-4 shadow-sm', className)}>
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 id={titleId} className="text-sm font-semibold text-foreground">
          {title}
        </h2>
        {action && <TileActionControl action={action} />}
      </div>
      <TileBody query={query} isEmpty={isEmpty} emptyText={emptyText}>
        {children}
      </TileBody>
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

function TileBody({
  query,
  isEmpty,
  emptyText,
  children,
}: Pick<TileCardProps, 'query' | 'isEmpty' | 'emptyText' | 'children'>) {
  if (query?.isLoading) {
    return <div aria-busy="true" className="h-24 animate-pulse rounded bg-muted" />;
  }
  // Only a failure with nothing to show: a failed background refetch keeps the last data on screen.
  if (query?.isError && query.data === undefined) {
    return (
      <div role="alert" className="text-sm text-rose-700">
        <p>{query.error != null ? getErrorMessage(query.error) : 'Không tải được dữ liệu.'}</p>
        <Button type="button" variant="outline" size="sm" className="mt-2 text-foreground" onClick={() => query.refetch()}>
          Thử lại
        </Button>
      </div>
    );
  }
  if (isEmpty) {
    return <p className="text-sm text-muted-foreground">{emptyText ?? 'Chưa có dữ liệu.'}</p>;
  }
  return <>{children}</>;
}
