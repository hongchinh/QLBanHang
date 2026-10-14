import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MockInstance } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { realtimeBus } from '@/lib/realtime-bus';
import type { RealtimeStatus } from '@/lib/realtime-bus';
import { executiveKeys } from './keys';
import { useExecutiveLive } from './use-executive-live';

function setup(branchId?: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  const view = renderHook(({ id }: { id?: string }) => useExecutiveLive(id), {
    wrapper,
    initialProps: { id: branchId },
  });
  return { queryClient, invalidate, ...view };
}

const signal = (domains: string[], branchId = 'b1') =>
  act(() => realtimeBus.emitDashboardChanged({ branchId, domains }));
const setStatus = (status: RealtimeStatus) => act(() => realtimeBus.setStatus(status));
const advance = (ms: number) =>
  act(() => {
    vi.advanceTimersByTime(ms);
  });
// The argument of every invalidateQueries call, in order.
const calls = (invalidate: MockInstance) => invalidate.mock.calls.map((args) => args[0]);
const CASH = { queryKey: ['executive', 'cash'] };
const DEBT = { queryKey: ['executive', 'debt'] };
const SALES = { queryKey: ['executive', 'sales'] };
const INVENTORY = { queryKey: ['executive', 'inventory'] };
const CROSS = { queryKey: ['executive', 'cross'] };
const ALL = { queryKey: ['executive'] };

describe('useExecutiveLive', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    realtimeBus.setStatus('connecting');
  });
  afterEach(() => vi.useRealTimers());

  describe('signals', () => {
    it('collects the signals of one second, then invalidates each named domain once and the cross queries', () => {
      const { invalidate } = setup();

      signal(['cash']);
      advance(600);
      signal(['cash', 'debt']);
      advance(399);
      expect(invalidate).not.toHaveBeenCalled();

      // The second runs from the first signal, not from the last one.
      advance(1);
      expect(calls(invalidate)).toEqual([CASH, DEBT, CROSS]);
    });

    it('starts a new second with the next signal and does not repeat the earlier domains', () => {
      const { invalidate } = setup();
      signal(['sales']);
      advance(1_000);
      expect(calls(invalidate)).toEqual([SALES, CROSS]);
      invalidate.mockClear();

      advance(10_000);
      expect(invalidate).not.toHaveBeenCalled();

      signal(['inventory']);
      advance(999);
      expect(invalidate).not.toHaveBeenCalled();
      advance(1);
      expect(calls(invalidate)).toEqual([INVENTORY, CROSS]);
    });

    it('with a branch, ignores the signals of another branch', () => {
      const { invalidate } = setup('b1');

      signal(['cash'], 'b2');
      advance(5_000);
      expect(invalidate).not.toHaveBeenCalled();

      signal(['cash'], 'b1');
      advance(1_000);
      expect(calls(invalidate)).toEqual([CASH, CROSS]);
    });

    it('a signal of another branch neither adds its domains nor starts the second', () => {
      const { invalidate } = setup('b1');

      signal(['debt'], 'b2');
      advance(900);
      signal(['cash'], 'b1');
      advance(100);
      expect(invalidate).not.toHaveBeenCalled();

      advance(900);
      expect(calls(invalidate)).toEqual([CASH, CROSS]);
    });

    it('without a branch, the signal of any branch counts', () => {
      const { invalidate } = setup();

      signal(['cash'], 'b1');
      signal(['debt'], 'b2');
      advance(1_000);

      expect(calls(invalidate)).toEqual([CASH, DEBT, CROSS]);
    });

    it('skips a domain name it does not know', () => {
      const { invalidate } = setup();

      signal(['payroll', 'cross', 'Cash', 'toString']);
      advance(1_000);

      expect(calls(invalidate)).toEqual([CROSS]);
    });

    it('follows the branch it is given later, and keeps what it collected before', () => {
      const { invalidate, rerender } = setup('b1');
      signal(['cash'], 'b1');
      advance(500);

      rerender({ id: 'b2' });
      signal(['debt'], 'b1');
      signal(['sales'], 'b2');
      advance(500);

      expect(calls(invalidate)).toEqual([SALES, CASH, CROSS]);
    });
  });

  describe('connection', () => {
    it('status follows the bus', () => {
      const { result } = setup();
      expect(result.current.status).toBe('connecting');

      setStatus('connected');
      expect(result.current.status).toBe('connected');

      setStatus('disconnected');
      expect(result.current.status).toBe('disconnected');
    });

    it('the first connection refetches nothing', () => {
      const { invalidate } = setup();

      setStatus('connected');

      expect(invalidate).not.toHaveBeenCalled();
    });

    it('refetches everything once when the connection comes back', () => {
      const { invalidate } = setup();
      setStatus('connected');

      setStatus('disconnected');
      expect(invalidate).not.toHaveBeenCalled();

      setStatus('connected');
      expect(calls(invalidate)).toEqual([ALL]);
    });

    it('also when a new connection had to be started in between', () => {
      const { invalidate } = setup();
      setStatus('connected');

      setStatus('disconnected');
      setStatus('connecting');
      expect(invalidate).not.toHaveBeenCalled();
      setStatus('connected');
      expect(calls(invalidate)).toEqual([ALL]);

      // Only once per outage.
      setStatus('connecting');
      setStatus('connected');
      expect(calls(invalidate)).toEqual([ALL]);
    });

    it('opened while the connection is down: refetches when it is back', () => {
      realtimeBus.setStatus('disconnected');
      const { invalidate, result } = setup();
      expect(result.current.status).toBe('disconnected');

      setStatus('connected');

      expect(calls(invalidate)).toEqual([ALL]);
    });
  });

  describe('updatedAt', () => {
    const at = (h: number, m: number, s: number) => new Date(2026, 9, 13, h, m, s).getTime();
    const seed = (queryClient: QueryClient, key: readonly unknown[], data: unknown) =>
      act(() => {
        queryClient.setQueryData(key, data);
        vi.advanceTimersByTime(0);
      });

    it('is null while no executive query has data', () => {
      const { result, queryClient } = setup();
      expect(result.current.updatedAt).toBeNull();

      vi.setSystemTime(at(14, 30, 0));
      seed(queryClient, ['cash', 'reports', 'cash-book'], { rows: [] });
      act(() => {
        // A query that exists but has not loaded yet.
        queryClient.getQueryCache().build(queryClient, { queryKey: executiveKeys.debtSummary({}) });
        vi.advanceTimersByTime(0);
      });

      expect(result.current.updatedAt).toBeNull();
    });

    it('is the newest data time of the executive queries', () => {
      const { result, queryClient } = setup();

      vi.setSystemTime(at(14, 32, 5));
      seed(queryClient, executiveKeys.attention({}), { at: '2026-10-13' });
      expect(queryClient.getQueryState(executiveKeys.attention({}))?.dataUpdatedAt).toBe(at(14, 32, 5));
      expect(result.current.updatedAt).toBe(at(14, 32, 5));

      vi.setSystemTime(at(14, 33, 0));
      seed(queryClient, executiveKeys.cashSummary({ from: '2026-10-01', to: '2026-10-13' }), { balance: 1 });
      expect(result.current.updatedAt).toBe(at(14, 33, 0));

      // Data of another feature does not count.
      vi.setSystemTime(at(14, 34, 0));
      seed(queryClient, ['notifications'], []);
      expect(result.current.updatedAt).toBe(at(14, 33, 0));
    });
  });

  describe('unmount', () => {
    it('drops the second in progress: its timer does not fire', () => {
      const { invalidate, unmount } = setup();
      signal(['cash']);

      unmount();
      advance(5_000);

      expect(invalidate).not.toHaveBeenCalled();
    });

    it('a later signal reaches nothing', () => {
      const { invalidate, unmount } = setup();

      unmount();
      signal(['debt']);
      advance(5_000);

      expect(invalidate).not.toHaveBeenCalled();
    });

    it('a later reconnect reaches nothing', () => {
      const { invalidate, unmount } = setup();

      unmount();
      setStatus('disconnected');
      setStatus('connected');

      expect(invalidate).not.toHaveBeenCalled();
    });
  });
});
