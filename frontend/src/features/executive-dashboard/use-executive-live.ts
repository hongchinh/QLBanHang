import { useCallback, useEffect, useRef, useSyncExternalStore } from 'react';
import { notifyManager, useQueryClient } from '@tanstack/react-query';
import { realtimeBus } from '@/lib/realtime-bus';
import type { RealtimeStatus } from '@/lib/realtime-bus';
import { executiveKeys } from './keys';
import { EXECUTIVE_DOMAINS } from './types';

// Signals of one second are answered with one round of refetches.
const SIGNAL_WINDOW_MS = 1_000;

// Keeps the executive queries of an open dashboard in step with the "dashboard changed" signal.
// `branchId` is what the page sends to the API: a branch id, or undefined for every branch the user may see.
export function useExecutiveLive(branchId: string | undefined): { status: RealtimeStatus; updatedAt: number | null } {
  const queryClient = useQueryClient();
  const status = useSyncExternalStore(realtimeBus.onStatus, realtimeBus.getStatus);

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

  // The subscription below outlives a branch change, so what was collected for the second in progress is kept.
  const branchRef = useRef(branchId);
  useEffect(() => {
    branchRef.current = branchId;
  }, [branchId]);

  useEffect(() => {
    const pending = new Set<string>();
    let timer: ReturnType<typeof setTimeout> | undefined;

    const flush = () => {
      timer = undefined;
      // Names the client does not know are left out; the cross queries read several domains.
      for (const domain of EXECUTIVE_DOMAINS) {
        if (pending.has(domain)) void queryClient.invalidateQueries({ queryKey: executiveKeys.domain(domain) });
      }
      pending.clear();
      void queryClient.invalidateQueries({ queryKey: executiveKeys.domain('cross') });
    };

    const offSignal = realtimeBus.onDashboardChanged((signal) => {
      if (branchRef.current && signal.branchId !== branchRef.current) return;
      for (const domain of signal.domains) pending.add(domain);
      // The second starts with its first signal; later ones only add their domains.
      timer ??= setTimeout(flush, SIGNAL_WINDOW_MS);
    });

    // Signals sent while the connection was down are lost: refetch everything once it is back,
    // also when a new connection had to be started in between ('connecting').
    let wasDown = realtimeBus.getStatus() === 'disconnected';
    const offStatus = realtimeBus.onStatus(() => {
      const next = realtimeBus.getStatus();
      if (next === 'disconnected') {
        wasDown = true;
      } else if (next === 'connected' && wasDown) {
        wasDown = false;
        void queryClient.invalidateQueries({ queryKey: executiveKeys.all });
      }
    });

    return () => {
      offSignal();
      offStatus();
      clearTimeout(timer);
    };
  }, [queryClient]);

  return { status, updatedAt };
}
