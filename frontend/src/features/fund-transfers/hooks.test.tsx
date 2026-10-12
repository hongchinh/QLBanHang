import { beforeEach, describe, expect, it, vi } from 'vitest';
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
import type { FundTransferActionRequest, UpsertFundTransferRequest } from './types';

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

type Wrapper = ({ children }: { children: React.ReactNode }) => React.JSX.Element;

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(qc, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { wrapper, invalidate };
}

const body: FundTransferActionRequest = { version: 1, acknowledgeNegativeFund: false };
const data: UpsertFundTransferRequest = {
  voucherAt: '2026-01-01T00:00:00Z',
  fromFundId: 'f1',
  toFundId: 'f2',
  amount: 1,
  acknowledgeNegativeFund: false,
};

type ApiMethod = 'create' | 'update' | 'cancel' | 'restore' | 'remove';

const cases: Array<[string, ApiMethod, (render: Wrapper) => void]> = [
  ['create', 'create', (w) => renderHook(() => useCreateFundTransfer(), { wrapper: w }).result.current.mutate(data)],
  [
    'update',
    'update',
    (w) => renderHook(() => useUpdateFundTransfer(), { wrapper: w }).result.current.mutate({ id: 't1', data }),
  ],
  [
    'cancel',
    'cancel',
    (w) => renderHook(() => useCancelFundTransfer(), { wrapper: w }).result.current.mutate({ id: 't1', body }),
  ],
  [
    'restore',
    'restore',
    (w) => renderHook(() => useRestoreFundTransfer(), { wrapper: w }).result.current.mutate({ id: 't1', body }),
  ],
  [
    'delete',
    'remove',
    (w) => renderHook(() => useDeleteFundTransfer(), { wrapper: w }).result.current.mutate({ id: 't1', body }),
  ],
];

beforeEach(() => {
  vi.clearAllMocks();
});

describe('fund transfer mutations', () => {
  it.each(cases)('%s calls the api and refreshes the cash and inventory roots', async (_name, method, run) => {
    const { wrapper, invalidate } = setup();

    run(wrapper);

    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: cashKeys.all }));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: inventoryKeys.all });
    expect(fundTransfersApi[method]).toHaveBeenCalledTimes(1);
  });
});
