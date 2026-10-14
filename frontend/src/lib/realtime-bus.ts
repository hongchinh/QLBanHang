// A small bridge between the one SignalR connection of the app (hooks/useNotificationHub.ts) and the screens
// that follow it: the connection status and the "dashboard changed" signal. The hub hook writes, pages read.

export type RealtimeStatus = 'connecting' | 'connected' | 'disconnected';

// Which domains of which branch changed; it carries no figures. Domain names the client does not know are skipped.
export interface DashboardChangedSignal {
  branchId: string;
  domains: string[];
}

let status: RealtimeStatus = 'connecting';
const statusListeners = new Set<() => void>();
const signalListeners = new Set<(signal: DashboardChangedSignal) => void>();

// No method uses `this`: getStatus and onStatus are handed to useSyncExternalStore as they are.
export const realtimeBus = {
  getStatus(): RealtimeStatus {
    return status;
  },
  setStatus(next: RealtimeStatus): void {
    if (next === status) return;
    status = next;
    statusListeners.forEach((listener) => listener());
  },
  onStatus(listener: () => void): () => void {
    statusListeners.add(listener);
    return () => {
      statusListeners.delete(listener);
    };
  },
  emitDashboardChanged(signal: DashboardChangedSignal): void {
    signalListeners.forEach((listener) => listener(signal));
  },
  onDashboardChanged(listener: (signal: DashboardChangedSignal) => void): () => void {
    signalListeners.add(listener);
    return () => {
      signalListeners.delete(listener);
    };
  },
};
