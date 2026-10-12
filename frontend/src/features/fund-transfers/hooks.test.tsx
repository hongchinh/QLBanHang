import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { fundTransfersApi } from './api';
import {
  useCancelFundTransfer,
  useCreateFundTransfer,
  useDeleteFundTransfer,
  useRestoreFundTransfer,
  useUpdateFundTransfer,
} from './hooks';

vi.mock('./api', () => ({
  fundTransfersApi: {
    list: vi.fn(),
    get: vi.fn(),
    activities: vi.fn(),
    defaults: vi.fn(),
    create: vi.fn().mockResolvedValue({}),
    update: vi.fn().mockResolvedValue({}),
    cancel: vi.fn().mockResolvedValue({}),
    restore: vi.fn().mockResolvedValue({}),
    remove: vi.fn().mockResolvedValue(undefined),
  },
}));

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(qc, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { wrapper, invalidate };
}

const body = { reason: 'x' } as never;
const data = {} as never;

const cases: Array<[string, () => { mutate: (v: never) => void }, unknown]> = [
  ['create', useCreateFundTransfer as never, data],
  ['update', useUpdateFundTransfer as never, { id: 't1', data }],
  ['cancel', useCancelFundTransfer as never, { id: 't1', body }],
  ['restore', useRestoreFundTransfer as never, { id: 't1', body }],
  ['delete', useDeleteFundTransfer as never, { id: 't1', body }],
];

describe('fund transfer mutations', () => {
  it.each(cases)('%s refreshes the cash and inventory roots', async (_name, useHook, variables) => {
    const { wrapper, invalidate } = setup();
    const { result } = renderHook(() => useHook(), { wrapper });

    result.current.mutate(variables as never);

    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: cashKeys.all }));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: inventoryKeys.all });
    expect(fundTransfersApi).toBeDefined();
  });
});
