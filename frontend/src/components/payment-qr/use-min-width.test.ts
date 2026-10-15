import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useMinWidth } from './use-min-width';

function stubMatchMedia(matches: boolean) {
  let listener: (() => void) | undefined;
  const list = {
    matches,
    addEventListener: vi.fn((_type: string, cb: () => void) => {
      listener = cb;
    }),
    removeEventListener: vi.fn(),
  };
  const matchMedia = vi.fn(() => list);
  vi.stubGlobal('matchMedia', matchMedia);
  return {
    list,
    matchMedia,
    change(next: boolean) {
      list.matches = next;
      listener?.();
    },
  };
}

describe('useMinWidth', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('is true when the query matches', () => {
    const stub = stubMatchMedia(true);
    const { result } = renderHook(() => useMinWidth(640));
    expect(result.current).toBe(true);
    expect(stub.matchMedia).toHaveBeenCalledWith('(min-width: 640px)');
  });

  it('is false when the query does not match', () => {
    stubMatchMedia(false);
    const { result } = renderHook(() => useMinWidth(640));
    expect(result.current).toBe(false);
  });

  it('follows change events', () => {
    const stub = stubMatchMedia(true);
    const { result } = renderHook(() => useMinWidth(640));
    act(() => stub.change(false));
    expect(result.current).toBe(false);
  });

  it('stops listening when it unmounts', () => {
    const stub = stubMatchMedia(true);
    const { unmount } = renderHook(() => useMinWidth(640));
    const added = stub.list.addEventListener.mock.calls[0][1];
    unmount();
    expect(stub.list.removeEventListener).toHaveBeenCalledWith('change', added);
  });

  it('assumes desktop without matchMedia', () => {
    vi.stubGlobal('matchMedia', undefined);
    const { result } = renderHook(() => useMinWidth(640));
    expect(result.current).toBe(true);
  });
});
