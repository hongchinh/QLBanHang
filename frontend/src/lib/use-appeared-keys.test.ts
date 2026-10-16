import { StrictMode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useAppearedKeys } from './use-appeared-keys';

function renderKeys(keys: string[], durationMs?: number) {
  // A new array on every render, as the grids pass shownDimensions(...) without a memo.
  return renderHook(({ keys }) => useAppearedKeys([...keys], durationMs), { initialProps: { keys } });
}

describe('useAppearedKeys', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('marks nothing on mount', () => {
    const { result } = renderKeys(['sheet-count', 'length']);
    expect(result.current.size).toBe(0);
  });

  it('marks the keys that were not there before', () => {
    const { result, rerender } = renderKeys(['sheet-count']);
    rerender({ keys: ['sheet-count', 'length', 'width'] });
    expect([...result.current]).toEqual(['length', 'width']);
  });

  it('keeps the marks and adds none when a new array holds the same keys', () => {
    const { result, rerender } = renderKeys([]);
    rerender({ keys: ['sheet-count', 'length'] });
    act(() => {
      vi.advanceTimersByTime(600);
    });
    rerender({ keys: ['sheet-count', 'length'] });
    expect([...result.current]).toEqual(['sheet-count', 'length']);
    // The re-render did not restart the timer: the marks go 1200 ms after the keys appeared.
    act(() => {
      vi.advanceTimersByTime(600);
    });
    expect(result.current.size).toBe(0);
  });

  it('clears a mark durationMs after its key appeared', () => {
    const { result, rerender } = renderKeys([]);
    rerender({ keys: ['width'] });
    act(() => {
      vi.advanceTimersByTime(1199);
    });
    expect(result.current.has('width')).toBe(true);
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current.has('width')).toBe(false);
  });

  it('clears a key added while another is marked durationMs after its own appearance', () => {
    const { result, rerender } = renderKeys([], 1000);
    rerender({ keys: ['length'] });
    act(() => {
      vi.advanceTimersByTime(400);
    });
    rerender({ keys: ['length', 'width'] });
    expect([...result.current]).toEqual(['length', 'width']);
    act(() => {
      vi.advanceTimersByTime(600);
    });
    expect([...result.current]).toEqual(['width']);
    act(() => {
      vi.advanceTimersByTime(399);
    });
    expect(result.current.has('width')).toBe(true);
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current.size).toBe(0);
  });

  it('drops a removed key at once, with its timer, and never marks it', () => {
    const { result, rerender } = renderKeys(['sheet-count', 'length']);
    rerender({ keys: ['sheet-count'] });
    expect(result.current.size).toBe(0);
    rerender({ keys: ['sheet-count', 'width'] });
    expect([...result.current]).toEqual(['width']);
    rerender({ keys: ['sheet-count'] });
    expect(result.current.size).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('marks a key that hides and comes back for durationMs after its second appearance', () => {
    const { result, rerender } = renderKeys(['sheet-count']);
    rerender({ keys: ['sheet-count', 'width'] });
    act(() => {
      vi.advanceTimersByTime(500);
    });
    rerender({ keys: ['sheet-count'] });
    rerender({ keys: ['sheet-count', 'width'] });
    // The first appearance's timer would have fired at 1200 ms; the second appearance keeps the mark until 1700 ms.
    act(() => {
      vi.advanceTimersByTime(1199);
    });
    expect([...result.current]).toEqual(['width']);
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current.size).toBe(0);
  });

  it('marks new keys inside StrictMode', () => {
    const { result, rerender } = renderHook(({ keys }) => useAppearedKeys([...keys]), {
      initialProps: { keys: ['sheet-count'] },
      wrapper: StrictMode,
    });
    expect(result.current.size).toBe(0);
    rerender({ keys: ['sheet-count', 'length'] });
    expect([...result.current]).toEqual(['length']);
  });

  it('leaves no timer behind after unmount', () => {
    const { rerender, unmount } = renderKeys([]);
    rerender({ keys: ['length'] });
    expect(vi.getTimerCount()).toBe(1);
    unmount();
    expect(vi.getTimerCount()).toBe(0);
  });
});
