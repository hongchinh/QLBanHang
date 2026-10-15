import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useReasonSwitchNotice } from './use-reason-notice';

describe('useReasonSwitchNotice', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('starts empty', () => {
    const { result } = renderHook(() => useReasonSwitchNotice());
    expect(result.current.notice).toBeNull();
    expect(result.current.flash).toBe(false);
  });
  it('show keeps the text and flashes for 1.6 s', () => {
    const { result } = renderHook(() => useReasonSwitchNotice());
    act(() => result.current.show('Đã đổi lý do'));
    expect(result.current.notice).toBe('Đã đổi lý do');
    expect(result.current.flash).toBe(true);
    act(() => { vi.advanceTimersByTime(1599); });
    expect(result.current.flash).toBe(true);
    act(() => { vi.advanceTimersByTime(2); });
    expect(result.current.flash).toBe(false);
    expect(result.current.notice).toBe('Đã đổi lý do');
  });
  it('a second show restarts the flash', () => {
    const { result } = renderHook(() => useReasonSwitchNotice());
    act(() => result.current.show('a'));
    act(() => { vi.advanceTimersByTime(1500); });
    act(() => result.current.show('b'));
    act(() => { vi.advanceTimersByTime(1500); });
    expect(result.current.flash).toBe(true);
    expect(result.current.notice).toBe('b');
  });
  it('clear removes the text and the flash', () => {
    const { result } = renderHook(() => useReasonSwitchNotice());
    act(() => result.current.show('a'));
    act(() => result.current.clear());
    expect(result.current.notice).toBeNull();
    expect(result.current.flash).toBe(false);
  });
});
