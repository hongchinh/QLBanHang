import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useBanks } from './hooks';
import { banksApi } from './api';

vi.mock('./api', () => ({
  banksApi: { list: vi.fn() },
}));

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('useBanks', () => {
  it('fetches the bank list', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: '1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);

    const { result } = renderHook(() => useBanks(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].code).toBe('VCB');
  });
});
