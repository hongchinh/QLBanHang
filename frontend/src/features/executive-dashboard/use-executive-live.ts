import { useCallback, useSyncExternalStore } from 'react';
import { notifyManager, useQueryClient } from '@tanstack/react-query';
import { useDashboardSignal } from '@/hooks/use-dashboard-signal';
import type { RealtimeStatus } from '@/lib/realtime-bus';
import { executiveKeys } from './keys';
import { EXECUTIVE_DOMAINS } from './types';

// Keeps the executive queries of an open dashboard in step with the "dashboard changed" signal.
// `branchId` is what the page sends to the API: a branch id, or undefined for every branch the user may see.
export function useExecutiveLive(branchId: string | undefined): { status: RealtimeStatus; updatedAt: number | null } {
  const queryClient = useQueryClient();
  // The newest data time of the executive queries; the cache is the store.
  const subscribeToCache = useCallback(
    (onChange: () => void) => queryClient.getQueryCache().subscribe(notifyManager.batchCalls(onChange)),
    [queryClient],
  );
  const getUpdatedAt = useCallback(() => {
    let latest = 0;
    for (const query of queryClient.getQueryCache().findAll({ queryKey: executiveKeys.all })) {
      latest = Math.max(latest, query.state.dataUpdatedAt);
    }
    return latest > 0 ? latest : null;
  }, [queryClient]);
  const updatedAt = useSyncExternalStore(subscribeToCache, getUpdatedAt);

  const status = useDashboardSignal(branchId, {
    onDomains: (domains) => {
      // Names the client does not know are left out; the cross queries read several domains.
      for (const domain of EXECUTIVE_DOMAINS) {
        if (domains.has(domain)) void queryClient.invalidateQueries({ queryKey: executiveKeys.domain(domain) });
      }
      void queryClient.invalidateQueries({ queryKey: executiveKeys.domain('cross') });
    },
    onReconnect: () => void queryClient.invalidateQueries({ queryKey: executiveKeys.all }),
  });

  return { status, updatedAt };
}
