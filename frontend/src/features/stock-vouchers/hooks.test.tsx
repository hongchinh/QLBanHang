import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cashKeys } from '@/features/cash-common/keys';
import { productKeys } from '@/features/products/keys';
import type { StockDirection } from '@/features/stock-reasons/types';
import {
  useCancelStockVoucher,
  useCreateStockVoucher,
  useDeleteStockVoucher,
  usePartnerSearch,
  useRestoreStockVoucher,
  useUpdateStockVoucher,
} from './hooks';
import { inventoryKeys, stockVoucherKeys } from './keys';
import { stockVouchersApi } from './api';
import type { PartnerSearchItem, StockVoucher, UpsertStockVoucherRequest } from './types';

vi.mock('./api', () => ({
  stockVouchersApi: {
    partners: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    cancel: vi.fn(),
    restore: vi.fn(),
    remove: vi.fn(),
  },
}));

function partner(code: string): PartnerSearchItem {
  return { id: code, code, name: code, status: 'Active', isCustomer: true, isSupplier: false };
}

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(qc, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { wrapper, invalidate };
}

describe('usePartnerSearch', () => {
  beforeEach(() => {
    vi.mocked(stockVouchersApi.partners).mockReset();
  });

  it('keeps previous results for a new keyword but not for another type', async () => {
    let resolveNext: ((items: PartnerSearchItem[]) => void) | undefined;
    vi.mocked(stockVouchersApi.partners)
      .mockResolvedValueOnce([partner('KH01')])
      .mockImplementation(() => new Promise((resolve) => (resolveNext = resolve)));
    const { wrapper } = setup();
    const { result, rerender } = renderHook(
      ({ keyword, type }) => usePartnerSearch(type, keyword),
      { wrapper, initialProps: { keyword: 'KH', type: 'In' as StockDirection } },
    );
    await waitFor(() => expect(result.current.data).toEqual([partner('KH01')]));

    rerender({ keyword: 'KH0', type: 'In' });
    expect(result.current.data).toEqual([partner('KH01')]);
    expect(result.current.isPlaceholderData).toBe(true);
    act(() => resolveNext?.([partner('KH02')]));
    await waitFor(() => expect(result.current.data).toEqual([partner('KH02')]));

    rerender({ keyword: 'KH0', type: 'Out' });
    expect(result.current.data).toBeUndefined();
    expect(result.current.isLoading).toBe(true);
  });

  it('passes activeOnly to the api', async () => {
    vi.mocked(stockVouchersApi.partners).mockResolvedValue([]);
    const { wrapper } = setup();
    renderHook(() => usePartnerSearch('In', 'abc', true), { wrapper });
    await waitFor(() => expect(stockVouchersApi.partners).toHaveBeenCalledWith('In', 'abc', true));
  });

  it('activeOnly is part of the query key', () => {
    expect(stockVoucherKeys.partners('In', 'abc', true)).not.toEqual(
      stockVoucherKeys.partners('In', 'abc'),
    );
  });

  it('keeps previous results for the same activeOnly but not when it differs', async () => {
    vi.mocked(stockVouchersApi.partners)
      .mockResolvedValueOnce([partner('KH01')])
      .mockImplementation(() => new Promise(() => undefined));
    const { wrapper } = setup();
    const { result, rerender } = renderHook(
      ({ keyword, activeOnly }: { keyword: string; activeOnly?: boolean }) =>
        usePartnerSearch('In', keyword, activeOnly),
      { wrapper, initialProps: { keyword: 'a' } as { keyword: string; activeOnly?: boolean } },
    );
    await waitFor(() => expect(result.current.data).toEqual([partner('KH01')]));

    rerender({ keyword: 'ab', activeOnly: true });
    expect(result.current.data).toBeUndefined();
    expect(result.current.isLoading).toBe(true);

    rerender({ keyword: 'ab' });
    expect(result.current.data).toEqual([partner('KH01')]);
    expect(result.current.isPlaceholderData).toBe(true);
  });
});

async function invalidations<T>(useHook: () => T, call: (mutation: T) => Promise<unknown>) {
  const { wrapper, invalidate } = setup();
  const { result } = renderHook(useHook, { wrapper });
  await act(() => call(result.current));
  return invalidate.mock.calls.map((c) => c[0]);
}

describe('stock voucher mutations', () => {
  it('a stock-in change also refreshes products (cost price, D35); a stock-out change does not', async () => {
    const { wrapper, invalidate } = setup();
    const { result } = renderHook(() => useCreateStockVoucher(), { wrapper });

    vi.mocked(stockVouchersApi.create).mockResolvedValueOnce({ type: 'Out' } as StockVoucher);
    await act(() => result.current.mutateAsync({} as UpsertStockVoucherRequest));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: inventoryKeys.all });
    expect(invalidate).not.toHaveBeenCalledWith({ queryKey: productKeys.all });

    vi.mocked(stockVouchersApi.create).mockResolvedValueOnce({ type: 'In' } as StockVoucher);
    await act(() => result.current.mutateAsync({} as UpsertStockVoucherRequest));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: productKeys.all });
  });

  it('deleting a stock-in refreshes products', async () => {
    const { wrapper, invalidate } = setup();
    vi.mocked(stockVouchersApi.remove).mockResolvedValueOnce(undefined as never);
    const { result } = renderHook(() => useDeleteStockVoucher(), { wrapper });

    await act(() => result.current.mutateAsync({ id: 'v1', type: 'In', body: { version: 1 } }));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: productKeys.all });
  });

  // P28: automatic receipts/payments and debt entries follow every stock voucher change.
  it('mutations invalidate the cash root too', async () => {
    const saved = { type: 'Out' } as StockVoucher;
    vi.mocked(stockVouchersApi.create).mockResolvedValueOnce(saved);
    vi.mocked(stockVouchersApi.update).mockResolvedValueOnce(saved);
    vi.mocked(stockVouchersApi.cancel).mockResolvedValueOnce(saved);
    vi.mocked(stockVouchersApi.restore).mockResolvedValueOnce(saved);
    vi.mocked(stockVouchersApi.remove).mockResolvedValueOnce(undefined as never);
    const body = { version: 1 };

    const calls = [
      await invalidations(useCreateStockVoucher, (m) => m.mutateAsync({} as UpsertStockVoucherRequest)),
      await invalidations(useUpdateStockVoucher, (m) => m.mutateAsync({ id: 'v1', data: {} as UpsertStockVoucherRequest })),
      await invalidations(useCancelStockVoucher, (m) => m.mutateAsync({ id: 'v1', body })),
      await invalidations(useRestoreStockVoucher, (m) => m.mutateAsync({ id: 'v1', body })),
      await invalidations(useDeleteStockVoucher, (m) => m.mutateAsync({ id: 'v1', type: 'Out', body })),
    ];

    for (const keys of calls) {
      expect(keys).toContainEqual({ queryKey: inventoryKeys.all });
      expect(keys).toContainEqual({ queryKey: cashKeys.all });
    }
  });
});
