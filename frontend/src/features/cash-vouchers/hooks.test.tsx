import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { debtApi } from '@/features/debt/api';
import { useOpenItems } from '@/features/debt/hooks';
import type { OpenItem } from '@/features/debt/types';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { cashVouchersApi } from './api';
import {
  useCancelCashVoucher,
  useCreateCashVoucher,
  useCashPartnerSearch,
  useDeleteCashVoucher,
  useRestoreCashVoucher,
  useUpdateCashVoucher,
} from './hooks';
import { cashVoucherKeys } from './keys';
import type { CashPartnerSearchItem, CashVoucher, UpsertCashVoucherRequest } from './types';

vi.mock('./api', () => ({
  cashVouchersApi: {
    partners: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    cancel: vi.fn(),
    restore: vi.fn(),
    remove: vi.fn(),
  },
}));

vi.mock('@/features/debt/api', () => ({
  debtApi: { openItems: vi.fn() },
}));

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(qc, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { qc, wrapper, invalidate };
}

const saved = { id: 'v1', type: 'Receipt' } as CashVoucher;

describe('cash voucher mutations', () => {
  it('mutations invalidate cash and inventory roots', async () => {
    vi.mocked(cashVouchersApi.create).mockResolvedValue(saved);
    vi.mocked(cashVouchersApi.update).mockResolvedValue(saved);
    vi.mocked(cashVouchersApi.cancel).mockResolvedValue(saved);
    vi.mocked(cashVouchersApi.restore).mockResolvedValue(saved);
    vi.mocked(cashVouchersApi.remove).mockResolvedValue(undefined);

    const runs: ((wrapper: React.FC<{ children: React.ReactNode }>) => Promise<unknown>)[] = [
      async (wrapper) => {
        const { result } = renderHook(() => useCreateCashVoucher(), { wrapper });
        return act(() => result.current.mutateAsync({} as UpsertCashVoucherRequest));
      },
      async (wrapper) => {
        const { result } = renderHook(() => useUpdateCashVoucher(), { wrapper });
        return act(() => result.current.mutateAsync({ id: 'v1', data: {} as UpsertCashVoucherRequest }));
      },
      async (wrapper) => {
        const { result } = renderHook(() => useCancelCashVoucher(), { wrapper });
        return act(() => result.current.mutateAsync({ id: 'v1', body: { version: 1 } }));
      },
      async (wrapper) => {
        const { result } = renderHook(() => useRestoreCashVoucher(), { wrapper });
        return act(() => result.current.mutateAsync({ id: 'v1', body: { version: 1 } }));
      },
      async (wrapper) => {
        const { result } = renderHook(() => useDeleteCashVoucher(), { wrapper });
        return act(() => result.current.mutateAsync({ id: 'v1', body: { version: 1 } }));
      },
    ];

    for (const run of runs) {
      const { wrapper, invalidate } = setup();
      await run(wrapper);
      expect(invalidate).toHaveBeenCalledWith({ queryKey: cashKeys.all });
      expect(invalidate).toHaveBeenCalledWith({ queryKey: inventoryKeys.all });
    }
  });

  it('delete drops the cached detail before invalidating', async () => {
    vi.mocked(cashVouchersApi.remove).mockResolvedValue(undefined);
    const { qc, wrapper } = setup();
    qc.setQueryData(cashVoucherKeys.detail('v1'), saved);
    const { result } = renderHook(() => useDeleteCashVoucher(), { wrapper });

    await act(() =>
      result.current.mutateAsync({ id: 'v1', body: { version: 3, confirmRemoveSettlements: true } }),
    );

    expect(cashVouchersApi.remove).toHaveBeenCalledWith('v1', { version: 3, confirmRemoveSettlements: true });
    expect(qc.getQueryData(cashVoucherKeys.detail('v1'))).toBeUndefined();
  });
});

function cashPartner(code: string): CashPartnerSearchItem {
  return { id: code, code, name: code, status: 'Active', isCustomer: true, isSupplier: false };
}

describe('useCashPartnerSearch', () => {
  beforeEach(() => {
    vi.mocked(cashVouchersApi.partners).mockReset();
  });

  it('passes activeOnly to the api', async () => {
    vi.mocked(cashVouchersApi.partners).mockResolvedValue([]);
    const { wrapper } = setup();
    renderHook(() => useCashPartnerSearch('Receipt', 'abc', true), { wrapper });
    await waitFor(() =>
      expect(cashVouchersApi.partners).toHaveBeenCalledWith('Receipt', 'abc', true),
    );
  });

  it('activeOnly is part of the query key', () => {
    expect(cashVoucherKeys.partners('Receipt', 'abc', true)).not.toEqual(
      cashVoucherKeys.partners('Receipt', 'abc'),
    );
  });

  it('keeps previous results for the same activeOnly but not when it differs', async () => {
    vi.mocked(cashVouchersApi.partners)
      .mockResolvedValueOnce([cashPartner('KH01')])
      .mockImplementation(() => new Promise(() => undefined));
    const { wrapper } = setup();
    const { result, rerender } = renderHook(
      ({ keyword, activeOnly }: { keyword: string; activeOnly?: boolean }) =>
        useCashPartnerSearch('Receipt', keyword, activeOnly),
      { wrapper, initialProps: { keyword: 'a' } as { keyword: string; activeOnly?: boolean } },
    );
    await waitFor(() => expect(result.current.data).toEqual([cashPartner('KH01')]));

    rerender({ keyword: 'ab', activeOnly: true });
    expect(result.current.data).toBeUndefined();
    expect(result.current.isLoading).toBe(true);

    rerender({ keyword: 'ab' });
    expect(result.current.data).toEqual([cashPartner('KH01')]);
    expect(result.current.isPlaceholderData).toBe(true);
  });
});

describe('useOpenItems', () => {
  it('loads under the cash root and waits until enabled', async () => {
    const items = [{ entryId: 'e1', docCode: 'PX00001', open: 1_000 }] as OpenItem[];
    vi.mocked(debtApi.openItems).mockResolvedValue(items);
    const params = { partnerId: 'p1', side: 'Receivable' as const, direction: 'Increase' as const };
    const { qc, wrapper } = setup();

    const { result, rerender } = renderHook(({ enabled }) => useOpenItems(params, { enabled }), {
      wrapper,
      initialProps: { enabled: false },
    });
    expect(debtApi.openItems).not.toHaveBeenCalled();

    rerender({ enabled: true });
    await waitFor(() => expect(result.current.data).toEqual(items));
    expect(debtApi.openItems).toHaveBeenCalledWith(params);
    expect(qc.getQueryData([...cashKeys.all, 'debt', 'open-items', params])).toEqual(items);
  });
});
