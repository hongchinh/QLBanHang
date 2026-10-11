import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { debtApi } from './api';
import {
  useCreateSettlementBatch,
  useDebtPartnerSearch,
  useDebtSettlements,
  useDeleteSettlement,
  useDeleteSettlementBatch,
} from './hooks';
import type { SettlementBatch } from './types';

vi.mock('./api', () => ({
  debtApi: {
    searchPartners: vi.fn(),
    settlements: vi.fn(),
    createSettlementBatch: vi.fn(),
    deleteSettlement: vi.fn(),
    deleteSettlementBatch: vi.fn(),
  },
  partnerSummaryApi: { get: vi.fn() },
}));

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(qc, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { qc, wrapper, invalidate };
}

beforeEach(() => {
  vi.mocked(debtApi.searchPartners).mockReset().mockResolvedValue([]);
  vi.mocked(debtApi.settlements).mockReset().mockResolvedValue([]);
});

describe('useDebtPartnerSearch', () => {
  it('sends the side and keyword', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtPartnerSearch('Payable', ' ab '), { wrapper });

    await waitFor(() => expect(debtApi.searchPartners).toHaveBeenCalled());
    expect(debtApi.searchPartners).toHaveBeenCalledWith({ side: 'Payable', keyword: 'ab' });
  });

  it('searches partners with both roles without a side', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtPartnerSearch(undefined, '', { bothRoles: true }), { wrapper });

    await waitFor(() => expect(debtApi.searchPartners).toHaveBeenCalled());
    expect(debtApi.searchPartners).toHaveBeenCalledWith({ side: undefined, keyword: undefined, bothRoles: true });
  });
});

describe('useDebtSettlements', () => {
  it('loads the settlements of a partner and side', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtSettlements({ partnerId: 'p1', side: 'Receivable' }), { wrapper });

    await waitFor(() => expect(debtApi.settlements).toHaveBeenCalled());
    expect(debtApi.settlements).toHaveBeenCalledWith({ partnerId: 'p1', side: 'Receivable' });
  });

  it('waits for a partner and for enabled', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtSettlements({ partnerId: '', side: 'Receivable' }), { wrapper });
    renderHook(() => useDebtSettlements({ partnerId: 'p1', side: 'Payable' }, { enabled: false }), { wrapper });

    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(debtApi.settlements).not.toHaveBeenCalled();
  });
});

describe('settlement mutations', () => {
  it('create and delete invalidate the cash root', async () => {
    vi.mocked(debtApi.createSettlementBatch).mockResolvedValue({ batchId: 'b1', settlements: [] } as SettlementBatch);
    vi.mocked(debtApi.deleteSettlement).mockResolvedValue(undefined);
    vi.mocked(debtApi.deleteSettlementBatch).mockResolvedValue(undefined);

    const runs: ((wrapper: React.FC<{ children: React.ReactNode }>) => Promise<unknown>)[] = [
      async (wrapper) => {
        const { result } = renderHook(() => useCreateSettlementBatch(), { wrapper });
        return act(() =>
          result.current.mutateAsync({
            partnerId: 'p1',
            side: 'Receivable',
            items: [{ increaseEntryId: 'i1', decreaseEntryId: 'd1', amount: 100 }],
          }),
        );
      },
      async (wrapper) => {
        const { result } = renderHook(() => useDeleteSettlement(), { wrapper });
        return act(() => result.current.mutateAsync('s1'));
      },
      async (wrapper) => {
        const { result } = renderHook(() => useDeleteSettlementBatch(), { wrapper });
        return act(() => result.current.mutateAsync('b1'));
      },
    ];

    for (const run of runs) {
      const { wrapper, invalidate } = setup();
      await run(wrapper);
      expect(invalidate).toHaveBeenCalledWith({ queryKey: cashKeys.all });
      expect(invalidate).toHaveBeenCalledWith({ queryKey: inventoryKeys.all });
    }

    expect(debtApi.createSettlementBatch).toHaveBeenCalledWith({
      partnerId: 'p1',
      side: 'Receivable',
      items: [{ increaseEntryId: 'i1', decreaseEntryId: 'd1', amount: 100 }],
    });
    expect(debtApi.deleteSettlement).toHaveBeenCalledWith('s1');
    expect(debtApi.deleteSettlementBatch).toHaveBeenCalledWith('b1');
  });
});
