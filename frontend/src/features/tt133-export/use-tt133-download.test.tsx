import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { Tt133ExportForm } from './types';

const downloadTt133 = vi.fn();
vi.mock('./api', () => ({ downloadTt133: (...a: unknown[]) => downloadTt133(...a) }));
const toast = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...a: unknown[]) => toast(...a) }));

import { useTt133Download } from './use-tt133-download';

const form: Tt133ExportForm = {
  code: 'S04a-DNN',
  report: 'cash-book',
  params: { kind: 'Cash', from: '2026-10-01', to: '2026-10-31' },
  fileBase: 'S04a-DNN_20261001_20261031',
};

describe('useTt133Download', () => {
  beforeEach(() => {
    downloadTt133.mockReset();
    toast.mockReset();
  });

  it('downloads the form with the extra params merged in', async () => {
    downloadTt133.mockResolvedValue(undefined);
    const { result } = renderHook(() => useTt133Download());

    await act(() => result.current.download(form, 'xlsx', { all: true }));

    expect(downloadTt133).toHaveBeenCalledWith(
      'cash-book',
      'xlsx',
      { kind: 'Cash', from: '2026-10-01', to: '2026-10-31', all: true },
      'S04a-DNN_20261001_20261031',
    );
    expect(toast).not.toHaveBeenCalled();
  });

  it('shows a destructive toast on failure and does not throw', async () => {
    downloadTt133.mockRejectedValue(new Error('Xuất PDF thất bại, hãy dùng Excel.'));
    const { result } = renderHook(() => useTt133Download());

    await act(() => result.current.download(form, 'pdf'));

    expect(toast).toHaveBeenCalledWith({
      variant: 'destructive',
      title: 'Không thể xuất file',
      description: 'Xuất PDF thất bại, hãy dùng Excel.',
    });
  });

  it('is pending while the call runs', async () => {
    let resolve: () => void = () => {};
    downloadTt133.mockReturnValue(
      new Promise<void>((r) => {
        resolve = r;
      }),
    );
    const { result } = renderHook(() => useTt133Download());
    expect(result.current.isPending).toBe(false);

    let pending: Promise<void> = Promise.resolve();
    act(() => {
      pending = result.current.download(form, 'xlsx');
    });
    expect(result.current.isPending).toBe(true);

    await act(async () => {
      resolve();
      await pending;
    });
    await waitFor(() => expect(result.current.isPending).toBe(false));
  });
});
