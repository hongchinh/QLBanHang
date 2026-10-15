import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useMediaQuery } from './useMediaQuery';

const original = Object.getOwnPropertyDescriptor(window, 'matchMedia');

function setMatchMedia(value: unknown) {
  Object.defineProperty(window, 'matchMedia', { configurable: true, writable: true, value });
}

afterEach(() => {
  if (original) Object.defineProperty(window, 'matchMedia', original);
  else delete (window as { matchMedia?: unknown }).matchMedia;
});

describe('useMediaQuery', () => {
  it.each([true, false])('returns the fallback %s without matchMedia', (fallback) => {
    setMatchMedia(undefined);
    const { result } = renderHook(() => useMediaQuery('(min-width: 1024px)', fallback));
    expect(result.current).toBe(fallback);
  });

  it('defaults the fallback to false', () => {
    setMatchMedia(undefined);
    const { result } = renderHook(() => useMediaQuery('(min-width: 1024px)'));
    expect(result.current).toBe(false);
  });

  it('does not throw on a stub without change listeners', () => {
    setMatchMedia(vi.fn().mockReturnValue({ matches: true }));
    const { result, unmount } = renderHook(() => useMediaQuery('(min-width: 1024px)'));
    expect(result.current).toBe(true);
    expect(() => unmount()).not.toThrow();
  });

  it('falls back to addListener on older matchMedia objects', () => {
    const mql = { matches: false, addListener: vi.fn(), removeListener: vi.fn() };
    setMatchMedia(vi.fn().mockReturnValue(mql));
    const { unmount } = renderHook(() => useMediaQuery('(min-width: 1024px)'));
    expect(mql.addListener).toHaveBeenCalledWith(expect.any(Function));
    unmount();
    expect(mql.removeListener).toHaveBeenCalledWith(mql.addListener.mock.calls[0][0]);
  });

  it('follows the media query and removes its listener on unmount', () => {
    const mql = { matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() };
    setMatchMedia(vi.fn().mockReturnValue(mql));
    const { result, unmount } = renderHook(() => useMediaQuery('(min-width: 1024px)'));
    expect(result.current).toBe(true);
    expect(mql.addEventListener).toHaveBeenCalledWith('change', expect.any(Function));

    const listener = mql.addEventListener.mock.calls[0][1] as () => void;
    mql.matches = false;
    act(() => listener());
    expect(result.current).toBe(false);

    unmount();
    expect(mql.removeEventListener).toHaveBeenCalledWith('change', listener);
  });
});
