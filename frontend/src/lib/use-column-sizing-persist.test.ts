import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useColumnSizingPersist } from './use-column-sizing-persist';

const STORAGE_KEY = 'qldh:col-sizes:test-table';

describe('useColumnSizingPersist', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });
  afterEach(() => {
    window.localStorage.clear();
  });

  it('returns the initial sizing when localStorage is empty', () => {
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    expect(result.current[0]).toEqual({ code: 100 });
  });

  it('returns an empty object when no initial sizing and localStorage is empty', () => {
    const { result } = renderHook(() => useColumnSizingPersist('test-table'));
    expect(result.current[0]).toEqual({});
  });

  it('persists updates to localStorage under the namespaced key', () => {
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    act(() => {
      result.current[1]({ code: 200 });
    });
    expect(result.current[0]).toEqual({ code: 200 });
    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
      code: 200,
    });
  });

  it('reads previously persisted sizing on mount, merged over initial sizing', () => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ code: 250 }));
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100, name: 160 }),
    );
    expect(result.current[0]).toEqual({ code: 250, name: 160 });
  });

  it('ignores malformed JSON in localStorage and falls back to initial sizing', () => {
    window.localStorage.setItem(STORAGE_KEY, '{not valid json');
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    expect(result.current[0]).toEqual({ code: 100 });
  });

  it('accepts a functional updater like React setState', () => {
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    act(() => {
      result.current[1]((prev) => ({ ...prev, name: 160 }));
    });
    expect(result.current[0]).toEqual({ code: 100, name: 160 });
  });
});
