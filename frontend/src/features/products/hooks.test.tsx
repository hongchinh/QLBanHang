import { act, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { productsApi } from './api';
import { useProductSearch } from './hooks';

vi.mock('./api', () => ({ productsApi: { search: vi.fn() }, lookupsApi: {} }));

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useProductSearch', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.mocked(productsApi.search).mockResolvedValue([]);
  });
  afterEach(() => vi.useRealTimers());

  it('settledQuery follows the query after the debounce', () => {
    const { result, rerender } = renderHook(({ q }) => useProductSearch(q), {
      wrapper,
      initialProps: { q: 'a' },
    });
    rerender({ q: 'ab' });
    expect(result.current.settledQuery).toBe('a');
    act(() => {
      vi.advanceTimersByTime(200);
    });
    expect(result.current.settledQuery).toBe('ab');
  });
});
