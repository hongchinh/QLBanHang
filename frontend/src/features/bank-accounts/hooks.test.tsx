import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useMyBankAccounts } from './hooks';
import { bankAccountsApi } from './api';

vi.mock('./api', () => ({
  bankAccountsApi: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    setDefault: vi.fn(),
  },
}));

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('useMyBankAccounts', () => {
  it('fetches saved accounts', async () => {
    vi.mocked(bankAccountsApi.list).mockResolvedValue([
      {
        id: 'acc-1', bankId: 'bank-1', bankCode: 'VCB', bankName: 'Vietcombank', bankBin: '970436',
        accountNumber: '0123456789', accountName: 'Nguyen Van A', isDefault: true,
      },
    ]);

    const { result } = renderHook(() => useMyBankAccounts(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].isDefault).toBe(true);
  });
});
