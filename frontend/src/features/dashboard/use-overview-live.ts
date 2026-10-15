import { useCallback, useSyncExternalStore } from 'react';
import { notifyManager, useQueryClient } from '@tanstack/react-query';
import { useDashboardSignal } from '@/hooks/use-dashboard-signal';
import type { RealtimeStatus } from '@/lib/realtime-bus';
import { dashboardKeys, QUOTATION_PREFIXES, REVENUE_PREFIXES } from './keys';

// Keeps the queries of an open overview dashboard (`/` and `/admin/dashboard`) in step with the signals:
// revenue follows the `sales` domain of the working branch, quotation counts follow QuotationsChanged.
// `branchId` is the working branch the page sends to the API.
export function useOverviewLive(branchId: string | undefined): { status: RealtimeStatus; updatedAt: number | null } {
  const queryClient = useQueryClient();

  // The newest data time of the dashboard queries; the cache is the store.
  const subscribeToCache = useCallback(
    (onChange: () => void) => queryClient.getQueryCache().subscribe(notifyManager.batchCalls(onChange)),
    [queryClient],
  );
  const getUpdatedAt = useCallback(() => {
    let latest = 0;
    // The legacy quotation-stats query is not on these pages.
    const onOverview = (key: readonly unknown[]) => key[1] !== 'quotation-stats';
    for (const query of queryClient.getQueryCache().findAll({
      queryKey: dashboardKeys.all,
      predicate: (q) => onOverview(q.queryKey),
    })) {
      latest = Math.max(latest, query.state.dataUpdatedAt);
    }
    return latest > 0 ? latest : null;
  }, [queryClient]);
  const updatedAt = useSyncExternalStore(subscribeToCache, getUpdatedAt);

  const invalidatePrefixes = (prefixes: string[]) => {
    for (const prefix of prefixes) void queryClient.invalidateQueries({ queryKey: [...dashboardKeys.all, prefix] });
  };

  const status = useDashboardSignal(branchId, {
    onDomains: (domains) => {
      if (domains.has('sales')) invalidatePrefixes(REVENUE_PREFIXES);
    },
    onQuotations: () => invalidatePrefixes(QUOTATION_PREFIXES),
    onReconnect: () => void queryClient.invalidateQueries({ queryKey: dashboardKeys.all }),
  });

  return { status, updatedAt };
}
