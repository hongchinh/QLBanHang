import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MockInstance } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { realtimeBus } from '@/lib/realtime-bus';
import type { RealtimeStatus } from '@/lib/realtime-bus';
import { useOverviewLive } from './use-overview-live';

function setup(branchId = 'a') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(['dashboard', 'summary', {}], { x: 1 });
  queryClient.setQueryData(['dashboard', 'recent-activity', {}], []);
  queryClient.setQueryData(['dashboard', 'top-products', {}], []);
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  const view = renderHook(() => useOverviewLive(branchId), { wrapper });
  return { queryClient, invalidate, ...view };
}

const signal = (domains: string[], branchId = 'a') => act(() => realtimeBus.emitDashboardChanged({ branchId, domains }));
const setStatus = (status: RealtimeStatus) => act(() => realtimeBus.setStatus(status));
const advance = (ms: number) =>
  act(() => {
    vi.advanceTimersByTime(ms);
  });

// The second key part of every prefix refetched through a predicate, in call order.
function prefixesOf(invalidate: MockInstance): string[] {
  return invalidate.mock.calls.flatMap(([filters]) => {
    const f = filters as { queryKey?: unknown[]; predicate?: (q: { queryKey: unknown[] }) => boolean };
    if (f.queryKey && f.queryKey.length > 1) return [String(f.queryKey[1])];
    return [];
  });
}

describe('useOverviewLive', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    realtimeBus.setStatus('connecting');
  });
  afterEach(() => vi.useRealTimers());

  it('a sales signal of the branch invalidates the five revenue prefixes and not recent-activity', () => {
    const { invalidate } = setup();

    signal(['sales']);
    advance(1000);

    expect(prefixesOf(invalidate).sort()).toEqual(
      ['leaderboard', 'revenue-series', 'summary', 'top-customers', 'top-products'].sort(),
    );
    expect(prefixesOf(invalidate)).not.toContain('recent-activity');
  });

  it('a signal of another domain or another branch invalidates nothing', () => {
    const { invalidate } = setup();

    signal(['cash']);
    signal(['sales'], 'b');
    advance(1000);

    expect(invalidate).not.toHaveBeenCalled();
  });

  it('QuotationsChanged invalidates summary and recent-activity only', () => {
    const { invalidate } = setup();

    act(() => realtimeBus.emitQuotationsChanged());
    advance(1000);

    expect(prefixesOf(invalidate).sort()).toEqual(['recent-activity', 'summary']);
  });

  it('a reconnect invalidates every dashboard query', () => {
    const { invalidate } = setup();

    setStatus('connected');
    setStatus('disconnected');
    setStatus('connected');

    expect(invalidate).toHaveBeenCalledTimes(1);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['dashboard'] });
  });

  it('updatedAt is the newest dataUpdatedAt of the dashboard queries', () => {
    const { result, queryClient } = setup();
    expect(result.current.updatedAt).not.toBeNull();

    const times = queryClient
      .getQueryCache()
      .findAll({ queryKey: ['dashboard'] })
      .map((q) => q.state.dataUpdatedAt);
    expect(result.current.updatedAt).toBe(Math.max(...times));

    advance(5000);
    const writtenAt = Date.now();
    act(() => {
      queryClient.setQueryData(['dashboard', 'top-products', {}], [{ a: 1 }]);
    });
    // The cache notifies through the notify manager, which batches on a timer.
    advance(1);
    expect(result.current.updatedAt).toBe(writtenAt);
  });
});
