import { useEffect, useRef, useSyncExternalStore } from 'react';
import { realtimeBus } from '@/lib/realtime-bus';
import type { RealtimeStatus } from '@/lib/realtime-bus';

// Signals of one second are answered with one round of refetches.
const SIGNAL_WINDOW_MS = 1_000;

export interface DashboardSignalHandlers {
  // Domains collected in one 1-second window, for signals of `branchId` (any branch when undefined).
  onDomains: (domains: ReadonlySet<string>) => void;
  // Called at the end of a window that saw at least one QuotationsChanged; omitted = ignore those.
  onQuotations?: () => void;
  // The connection came back after being down: refetch everything.
  onReconnect: () => void;
}

// The listening core of the live dashboards: collects the "dashboard changed" signals of one branch and the
// "quotations changed" signals in 1-second windows and hands them to the page, which decides what to refetch.
// `branchId` is what the page sends to the API: a branch id, or undefined for every branch the user may see.
// `handlers` is read through a ref, so a page can pass an inline object without subscribing again.
export function useDashboardSignal(branchId: string | undefined, handlers: DashboardSignalHandlers): RealtimeStatus {
  const status = useSyncExternalStore(realtimeBus.onStatus, realtimeBus.getStatus);

  // The subscription below outlives a branch change, so what was collected for the second in progress is kept.
  const branchRef = useRef(branchId);
  const handlersRef = useRef(handlers);
  useEffect(() => {
    branchRef.current = branchId;
    handlersRef.current = handlers;
  });

  useEffect(() => {
    const pending = new Set<string>();
    let sawDomains = false;
    let sawQuotations = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const flush = () => {
      timer = undefined;
      const domains = new Set(pending);
      const hadDomains = sawDomains;
      const hadQuotations = sawQuotations;
      pending.clear();
      sawDomains = false;
      sawQuotations = false;
      if (hadDomains) handlersRef.current.onDomains(domains);
      if (hadQuotations) handlersRef.current.onQuotations?.();
    };

    const offSignal = realtimeBus.onDashboardChanged((signal) => {
      if (branchRef.current && signal.branchId !== branchRef.current) return;
      for (const domain of signal.domains) pending.add(domain);
      sawDomains = true;
      // The second starts with its first signal; later ones only add their domains.
      timer ??= setTimeout(flush, SIGNAL_WINDOW_MS);
    });

    const offQuotations = realtimeBus.onQuotationsChanged(() => {
      if (!handlersRef.current.onQuotations) return;
      sawQuotations = true;
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
        handlersRef.current.onReconnect();
      }
    });

    return () => {
      offSignal();
      offQuotations();
      offStatus();
      clearTimeout(timer);
    };
  }, []);

  return status;
}
