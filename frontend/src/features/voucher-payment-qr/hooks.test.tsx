import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { cashKeys } from '@/features/cash-common/keys';
import { useVoucherPaymentQr } from './hooks';
import { voucherPaymentQrKeys } from './keys';
import type { VoucherPaymentQr } from './types';

const apiGet = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api-client', () => ({ apiGet }));

const qr: VoucherPaymentQr = {
  payload: '000201',
  amount: 1_000_000,
  total: 1_000_000,
  cashPaid: 0,
  bankName: 'Ngân hàng TMCP Ngoại thương Việt Nam',
  bankShortName: 'Vietcombank',
  accountNumber: '0123456789',
  accountHolderName: 'CONG TY A',
  content: 'PT000125',
  fundCode: 'NH01',
  fundName: 'Tài khoản Vietcombank',
  sourceStockVoucherCode: null,
};

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient();
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useVoucherPaymentQr', () => {
  beforeEach(() => {
    apiGet.mockReset();
    apiGet.mockResolvedValue(qr);
  });

  it('loads the cash voucher QR', async () => {
    const { result } = renderHook(() => useVoucherPaymentQr('cash', 'c1', { enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(apiGet).toHaveBeenCalledWith('/cash-vouchers/c1/payment-qr');
    expect(result.current.data).toEqual(qr);
  });

  it('loads the stock voucher QR', async () => {
    const { result } = renderHook(() => useVoucherPaymentQr('stock', 's1', { enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(apiGet).toHaveBeenCalledWith('/stock-vouchers/s1/payment-qr');
  });

  it('sends no request when disabled or without an id', () => {
    renderHook(() => useVoucherPaymentQr('cash', 'c1', { enabled: false }), { wrapper });
    renderHook(() => useVoucherPaymentQr('cash', undefined, { enabled: true }), { wrapper });
    expect(apiGet).not.toHaveBeenCalled();
  });

  it('asks the API again on every mount', async () => {
    // A client that would keep an answer fresh and cached: only the hook's own options force the second request.
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, gcTime: Infinity } } });
    const shared = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const first = renderHook(() => useVoucherPaymentQr('cash', 'c1', { enabled: true }), { wrapper: shared });
    await waitFor(() => expect(first.result.current.isSuccess).toBe(true));
    first.unmount();
    const second = renderHook(() => useVoucherPaymentQr('cash', 'c1', { enabled: true }), { wrapper: shared });
    await waitFor(() => expect(second.result.current.isSuccess).toBe(true));
    expect(apiGet).toHaveBeenCalledTimes(2);
  });
});

describe('useVoucherPaymentQr failure', () => {
  it('does not retry a failed request', async () => {
    apiGet.mockReset();
    apiGet.mockRejectedValue(new Error('boom'));
    const client = new QueryClient({ defaultOptions: { queries: { retry: 3, retryDelay: 1 } } });
    const shared = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const { result } = renderHook(() => useVoucherPaymentQr('cash', 'c1', { enabled: true }), { wrapper: shared });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(apiGet).toHaveBeenCalledTimes(1);
  });
});

describe('voucherPaymentQrKeys', () => {
  it('lives under the cash root', () => {
    expect(voucherPaymentQrKeys.detail('cash', 'c1')).toEqual(['cash', 'payment-qr', 'cash', 'c1']);
    expect(voucherPaymentQrKeys.detail('stock', 's1').slice(0, cashKeys.all.length)).toEqual([...cashKeys.all]);
  });
});
