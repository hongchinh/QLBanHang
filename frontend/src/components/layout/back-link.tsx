import { Link, useLocation } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { AppIcon } from '@/components/icons';

export function BackLink({ to, label }: { to: string; label: string }) {
  const text = `Quay lại ${label}`;
  return (
    <Button variant="ghost" size="icon" asChild aria-label={text} title={text}>
      <Link to={to}>
        <AppIcon name="back" />
      </Link>
    </Button>
  );
}

/** Parent URL passed by a drill-down through `state.from`, else the fallback. */
export function useBackTarget(fallback?: string): string | undefined {
  const from = (useLocation().state as { from?: unknown } | null)?.from;
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : fallback;
}

/** State a drill-down parent passes so the child can link back with the same filters. */
export function useFromHere(): { from: string } {
  const { pathname, search } = useLocation();
  return { from: pathname + search };
}
