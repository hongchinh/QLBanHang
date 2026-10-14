import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { VoucherPrintRequest } from './types';

const fetchVoucherPrint = vi.fn();
vi.mock('./api', () => ({ fetchVoucherPrint: (...a: unknown[]) => fetchVoucherPrint(...a) }));
const show = vi.fn();
const close = vi.fn();
const openPdfTab = vi.fn();
vi.mock('@/lib/pdf-tab', () => ({ openPdfTab: (...a: unknown[]) => openPdfTab(...a) }));
const saveBlob = vi.fn();
vi.mock('@/lib/blob-download', () => ({ saveBlob: (...a: unknown[]) => saveBlob(...a) }));
const toast = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...a: unknown[]) => toast(...a) }));

import { useVoucherPrint } from './use-voucher-print';

const one: VoucherPrintRequest = {
  kind: 'cash',
  type: 'Receipt',
  vouchers: [{ id: 'a', code: 'PT00001' }],
  format: 'pdf',
};

describe('useVoucherPrint', () => {
  beforeEach(() => {
    for (const m of [fetchVoucherPrint, show, close, openPdfTab, saveBlob, toast]) m.mockReset();
    openPdfTab.mockReturnValue({ show, close });
  });

  it('pdf: the tab opens before the request ends', async () => {
    let resolve: (b: Blob) => void = () => {};
    fetchVoucherPrint.mockReturnValue(new Promise<Blob>((r) => (resolve = r)));
    const { result } = renderHook(() => useVoucherPrint());

    let pending: Promise<void> = Promise.resolve();
    act(() => {
      pending = result.current.print(one);
    });
    expect(openPdfTab).toHaveBeenCalledWith('PT00001');

    const blob = new Blob(['x']);
    await act(async () => {
      resolve(blob);
      await pending;
    });
    expect(show).toHaveBeenCalledWith(blob);
    expect(saveBlob).not.toHaveBeenCalled();
  });

  it('pdf of several vouchers uses a count as the tab title', async () => {
    fetchVoucherPrint.mockResolvedValue(new Blob());
    const { result } = renderHook(() => useVoucherPrint());
    await act(() =>
      result.current.print({
        ...one,
        vouchers: [
          { id: 'a', code: 'PT00001' },
          { id: 'b', code: 'PT00002' },
        ],
      }),
    );
    expect(openPdfTab).toHaveBeenCalledWith('In 2 phiếu');
  });

  it('xlsx saves the file', async () => {
    const blob = new Blob(['x']);
    fetchVoucherPrint.mockResolvedValue(blob);
    const { result } = renderHook(() => useVoucherPrint());
    await act(() => result.current.print({ ...one, format: 'xlsx' }));
    expect(saveBlob).toHaveBeenCalledWith(blob, 'PT00001.xlsx');
    expect(openPdfTab).not.toHaveBeenCalled();
  });

  it('failure closes the tab and shows a toast', async () => {
    fetchVoucherPrint.mockRejectedValue(new Error('Lỗi máy chủ'));
    const { result } = renderHook(() => useVoucherPrint());
    await act(() => result.current.print(one));
    expect(close).toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith({
      variant: 'destructive',
      title: 'Không thể in phiếu',
      description: expect.any(String),
    });
  });

  it('isPending is true while printing', async () => {
    let resolve: (b: Blob) => void = () => {};
    fetchVoucherPrint.mockReturnValue(new Promise<Blob>((r) => (resolve = r)));
    const { result } = renderHook(() => useVoucherPrint());
    expect(result.current.isPending).toBe(false);

    let pending: Promise<void> = Promise.resolve();
    act(() => {
      pending = result.current.print({ ...one, format: 'xlsx' });
    });
    expect(result.current.isPending).toBe(true);

    await act(async () => {
      resolve(new Blob());
      await pending;
    });
    await waitFor(() => expect(result.current.isPending).toBe(false));
  });
});
