import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { loadHelpIndex, resetHelpIndexCacheForTests, useHelpIndex, useHelpLoadFailed } from './load-help';

vi.mock('./content-index', () => {
  throw new Error('offline');
});

describe('loadHelpIndex when the chunk cannot load', () => {
  beforeEach(() => resetHelpIndexCacheForTests());

  it('rejects, flags the failure and clears the cache so a retry is possible', async () => {
    const first = loadHelpIndex();
    await expect(first).rejects.toThrow();
    expect(renderHook(() => useHelpLoadFailed()).result.current).toBe(true);
    const second = loadHelpIndex();
    expect(second).not.toBe(first);
    await expect(second).rejects.toThrow();
    expect(renderHook(() => useHelpIndex()).result.current).toBeNull();
  });
});
