import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { productKeys } from '@/features/products/keys';
import { useCreateStockVoucher, useDeleteStockVoucher, usePartnerSearch } from './hooks';
import { inventoryKeys } from './keys';
import { stockVouchersApi } from './api';
import type { PartnerSearchItem, StockVoucher, UpsertStockVoucherRequest } from './types';

vi.mock('./api', () => ({
  stockVouchersApi: {
    partners: vi.fn(),
    create: vi.fn(),
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

  it('keeps previous results for a new keyword but not for another reason', async () => {
    let resolveNext: ((items: PartnerSearchItem[]) => void) | undefined;
    vi.mocked(stockVouchersApi.partners)
      .mockResolvedValueOnce([partner('KH01')])
      .mockImplementation(() => new Promise((resolve) => (resolveNext = resolve)));
    const { wrapper } = setup();
    const { result, rerender } = renderHook(
      ({ keyword, reasonId }) => usePartnerSearch('In', keyword, reasonId),
      { wrapper, initialProps: { keyword: 'KH', reasonId: 'r1' } },
    );
    await waitFor(() => expect(result.current.data).toEqual([partner('KH01')]));

    rerender({ keyword: 'KH0', reasonId: 'r1' });
    expect(result.current.data).toEqual([partner('KH01')]);
    expect(result.current.isPlaceholderData).toBe(true);
    act(() => resolveNext?.([partner('KH02')]));
    await waitFor(() => expect(result.current.data).toEqual([partner('KH02')]));

    rerender({ keyword: 'KH0', reasonId: 'r2' });
    expect(result.current.data).toBeUndefined();
    expect(result.current.isLoading).toBe(true);
  });
});

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
});
