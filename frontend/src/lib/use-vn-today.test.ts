import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useVnToday } from './use-vn-today';

describe('useVnToday', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    // Wednesday 14 October 2026, 22:00 in VN time (the browser runs in VN time, lib/vn-datetime.ts).
    vi.setSystemTime(new Date(2026, 9, 14, 22, 0, 0));
  });
  afterEach(() => vi.useRealTimers());

  it('is the VN date of today', () => {
    const { result } = renderHook(() => useVnToday());

    expect(result.current).toBe('2026-10-14');
  });

  it('changes at VN midnight, not a moment before, and again at the next one', () => {
    const { result } = renderHook(() => useVnToday());

    act(() => vi.advanceTimersByTime(2 * 3_600_000 - 1));
    expect(result.current).toBe('2026-10-14');

    act(() => vi.advanceTimersByTime(1));
    expect(result.current).toBe('2026-10-15');

    act(() => vi.advanceTimersByTime(24 * 3_600_000));
    expect(result.current).toBe('2026-10-16');
  });

  // A computer asleep at midnight may fire a long timer late: the date is checked at least once a minute.
  it('catches up within a minute when the clock jumps past midnight', () => {
    const { result } = renderHook(() => useVnToday());

    vi.setSystemTime(new Date(2026, 9, 15, 7, 0, 0));
    act(() => vi.advanceTimersByTime(60_000));

    expect(result.current).toBe('2026-10-15');
  });

  it('clears its timer when unmounted', () => {
    const { unmount } = renderHook(() => useVnToday());
    expect(vi.getTimerCount()).toBe(1);

    unmount();

    expect(vi.getTimerCount()).toBe(0);
  });
});
