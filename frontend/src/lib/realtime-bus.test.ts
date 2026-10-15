import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { realtimeBus } from './realtime-bus';
import type { DashboardChangedSignal, RealtimeStatus } from './realtime-bus';

describe('realtimeBus', () => {
  const cleanups: (() => void)[] = [];
  const onStatus = (listener: () => void) => cleanups.push(realtimeBus.onStatus(listener));
  const onSignal = (listener: (signal: DashboardChangedSignal) => void) =>
    cleanups.push(realtimeBus.onDashboardChanged(listener));

  beforeEach(() => realtimeBus.setStatus('connecting'));
  afterEach(() => {
    while (cleanups.length > 0) cleanups.pop()!();
  });

  it('starts as connecting', async () => {
    vi.resetModules();
    const fresh = (await import('./realtime-bus')).realtimeBus;

    expect(fresh.getStatus()).toBe('connecting');
  });

  it('setStatus notifies the status listeners only when the status changes', () => {
    const first = vi.fn();
    const second = vi.fn();
    onStatus(first);
    onStatus(second);

    realtimeBus.setStatus('connected');
    expect(realtimeBus.getStatus()).toBe('connected');
    expect(first).toHaveBeenCalledTimes(1);
    expect(second).toHaveBeenCalledTimes(1);

    realtimeBus.setStatus('connected');
    expect(first).toHaveBeenCalledTimes(1);
    expect(second).toHaveBeenCalledTimes(1);

    realtimeBus.setStatus('disconnected');
    expect(first).toHaveBeenCalledTimes(2);
  });

  it('a listener reads the new status when it is notified', () => {
    const seen: RealtimeStatus[] = [];
    onStatus(() => seen.push(realtimeBus.getStatus()));

    realtimeBus.setStatus('disconnected');
    realtimeBus.setStatus('connected');

    expect(seen).toEqual(['disconnected', 'connected']);
  });

  it('an unsubscribed status listener is not called', () => {
    const gone = vi.fn();
    const kept = vi.fn();
    const off = realtimeBus.onStatus(gone);
    onStatus(kept);

    off();
    realtimeBus.setStatus('connected');

    expect(gone).not.toHaveBeenCalled();
    expect(kept).toHaveBeenCalledTimes(1);
  });

  it('emitDashboardChanged reaches every signal listener with the signal', () => {
    const first = vi.fn();
    const second = vi.fn();
    onSignal(first);
    onSignal(second);
    const signal: DashboardChangedSignal = { branchId: 'b1', domains: ['cash', 'debt'] };

    realtimeBus.emitDashboardChanged(signal);

    expect(first).toHaveBeenCalledTimes(1);
    expect(first.mock.calls[0][0]).toBe(signal);
    expect(second).toHaveBeenCalledTimes(1);
    expect(second.mock.calls[0][0]).toBe(signal);
  });

  it('an unsubscribed signal listener is not called', () => {
    const gone = vi.fn();
    const kept = vi.fn();
    const off = realtimeBus.onDashboardChanged(gone);
    onSignal(kept);

    off();
    realtimeBus.emitDashboardChanged({ branchId: 'b1', domains: ['sales'] });

    expect(gone).not.toHaveBeenCalled();
    expect(kept).toHaveBeenCalledTimes(1);
  });

  it('emitQuotationsChanged reaches the quotation listeners, and an unsubscribed one is not called', () => {
    const gone = vi.fn();
    const kept = vi.fn();
    const signal = vi.fn();
    const off = realtimeBus.onQuotationsChanged(gone);
    cleanups.push(realtimeBus.onQuotationsChanged(kept));
    onSignal(signal);

    realtimeBus.emitQuotationsChanged();
    expect(gone).toHaveBeenCalledTimes(1);
    expect(kept).toHaveBeenCalledTimes(1);
    expect(signal).not.toHaveBeenCalled();

    off();
    realtimeBus.emitQuotationsChanged();
    expect(gone).toHaveBeenCalledTimes(1);
    expect(kept).toHaveBeenCalledTimes(2);
  });

  it('keeps the two channels apart', () => {
    const status = vi.fn();
    const signal = vi.fn();
    onStatus(status);
    onSignal(signal);

    realtimeBus.emitDashboardChanged({ branchId: 'b1', domains: ['sales'] });
    expect(status).not.toHaveBeenCalled();

    realtimeBus.setStatus('connected');
    expect(signal).toHaveBeenCalledTimes(1);
  });

  it('a listener that unsubscribes while it is notified does not hide the next one', () => {
    const next = vi.fn();
    const off = realtimeBus.onStatus(() => off());
    onStatus(next);

    realtimeBus.setStatus('connected');

    expect(next).toHaveBeenCalledTimes(1);
  });

  it('getStatus and onStatus work without the object (useSyncExternalStore passes them bare)', () => {
    const { getStatus, onStatus: subscribe, setStatus } = realtimeBus;
    const listener = vi.fn();
    cleanups.push(subscribe(listener));

    setStatus('disconnected');

    expect(getStatus()).toBe('disconnected');
    expect(listener).toHaveBeenCalledTimes(1);
  });
});
