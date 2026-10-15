import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { realtimeBus } from '@/lib/realtime-bus';
import type { RealtimeStatus } from '@/lib/realtime-bus';
import { useDashboardSignal } from './use-dashboard-signal';

function setup(branchId: string | undefined, withQuotations = true) {
  const onDomains = vi.fn();
  const onQuotations = vi.fn();
  const onReconnect = vi.fn();
  const view = renderHook(
    ({ id }: { id: string | undefined }) =>
      useDashboardSignal(id, { onDomains, onReconnect, ...(withQuotations ? { onQuotations } : {}) }),
    { initialProps: { id: branchId } },
  );
  return { onDomains, onQuotations, onReconnect, ...view };
}

const signal = (domains: string[], branchId = 'a') => act(() => realtimeBus.emitDashboardChanged({ branchId, domains }));
const quotations = () => act(() => realtimeBus.emitQuotationsChanged());
const setStatus = (status: RealtimeStatus) => act(() => realtimeBus.setStatus(status));
const advance = (ms: number) =>
  act(() => {
    vi.advanceTimersByTime(ms);
  });

describe('useDashboardSignal', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    realtimeBus.setStatus('connecting');
  });
  afterEach(() => vi.useRealTimers());

  it('answers the signals of one second with one onDomains call holding the union of the domains', () => {
    const { onDomains } = setup('a');

    signal(['cash']);
    advance(600);
    signal(['sales', 'cash']);
    advance(399);
    expect(onDomains).not.toHaveBeenCalled();

    advance(1);
    expect(onDomains).toHaveBeenCalledTimes(1);
    expect([...onDomains.mock.calls[0][0]].sort()).toEqual(['cash', 'sales']);
  });

  it('ignores a signal of another branch, and accepts every branch when branchId is undefined', () => {
    const own = setup('a');
    signal(['sales'], 'b');
    advance(1000);
    expect(own.onDomains).not.toHaveBeenCalled();
    own.unmount();

    const any = setup(undefined);
    signal(['sales'], 'b');
    advance(1000);
    expect(any.onDomains).toHaveBeenCalledTimes(1);
  });

  it('QuotationsChanged alone gives one onQuotations call and no onDomains call', () => {
    const { onDomains, onQuotations } = setup('a');

    quotations();
    quotations();
    advance(999);
    expect(onQuotations).not.toHaveBeenCalled();

    advance(1);
    expect(onQuotations).toHaveBeenCalledTimes(1);
    expect(onDomains).not.toHaveBeenCalled();
  });

  it('ignores QuotationsChanged when there is no onQuotations handler', () => {
    const { onDomains } = setup('a', false);

    quotations();
    advance(2000);

    expect(onDomains).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('calls onReconnect once when the connection comes back after being down', () => {
    const { onReconnect } = setup('a');

    setStatus('connected');
    expect(onReconnect).not.toHaveBeenCalled();
    setStatus('disconnected');
    setStatus('connecting');
    setStatus('connected');
    expect(onReconnect).toHaveBeenCalledTimes(1);
    setStatus('connected');
    expect(onReconnect).toHaveBeenCalledTimes(1);
  });

  it('returns the connection status', () => {
    const { result } = setup('a');
    expect(result.current).toBe('connecting');
    setStatus('connected');
    expect(result.current).toBe('connected');
  });

  it('clears the pending timer on unmount', () => {
    const { onDomains, onQuotations, unmount } = setup('a');
    signal(['sales']);
    quotations();
    expect(vi.getTimerCount()).toBe(1);

    unmount();
    expect(vi.getTimerCount()).toBe(0);
    advance(2000);
    expect(onDomains).not.toHaveBeenCalled();
    expect(onQuotations).not.toHaveBeenCalled();
  });
});
