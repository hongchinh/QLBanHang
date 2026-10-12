import { act, renderHook } from '@testing-library/react';
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from '@/lib/use-toast';
import type { ConfirmFlags } from '@/lib/confirm-required';
import { useDocumentActions } from './useDocumentActions';

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

type Action = { kind: 'save' | 'delete'; flags: ConfirmFlags };

function apiError(status: number, code: string, details?: Record<string, string[]>, token?: string) {
  const config = { headers: new AxiosHeaders(), url: '/x' } as InternalAxiosRequestConfig;
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, null, {
    status,
    statusText: '',
    headers: {},
    config,
    data: { success: false, error: { code, message: `Server says ${code}`, details, token }, timestamp: '' },
  });
}

const errorTitles = { save: 'Không thể lưu', delete: 'Không thể xóa' };
const confirmLabels = { save: 'Vẫn lưu', delete: 'Vẫn xóa' };

function setup(overrides: Partial<Parameters<typeof useDocumentActions<Action>>[0]> = {}) {
  const perform = vi.fn<(a: Action, f: ConfirmFlags) => Promise<void>>().mockResolvedValue(undefined);
  const reload = vi.fn().mockResolvedValue({ version: 2 });
  const onReloaded = vi.fn();
  const hook = renderHook(() =>
    useDocumentActions<Action>({
      perform,
      reload,
      onReloaded,
      errorTitles,
      confirmLabels,
      concurrencyMessage: 'Đã tải lại',
      concurrencyReloadFailedMessage: 'Không tải lại được',
      ...overrides,
    }),
  );
  return { perform, reload, onReloaded, hook };
}

describe('useDocumentActions', () => {
  beforeEach(() => vi.mocked(toast).mockClear());

  it('performs the action once with its flags and toggles busy', async () => {
    const { perform, hook } = setup();
    let release!: () => void;
    perform.mockImplementationOnce(() => new Promise<void>((resolve) => (release = resolve)));

    let running!: Promise<void>;
    act(() => {
      running = hook.result.current.run({ kind: 'save', flags: {} });
    });
    expect(hook.result.current.busy).toBe(true);
    await act(async () => {
      release();
      await running;
    });

    expect(perform).toHaveBeenCalledTimes(1);
    expect(perform).toHaveBeenCalledWith({ kind: 'save', flags: {} }, {});
    expect(hook.result.current.busy).toBe(false);
    expect(toast).not.toHaveBeenCalled();
  });

  it('asks for confirmation on a confirmable 422 and resends with the flag added', async () => {
    const { perform, hook } = setup();
    perform.mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_WARNING'));

    await act(() => hook.result.current.run({ kind: 'save', flags: { acknowledgeNegativeStock: true } }));
    expect(hook.result.current.busy).toBe(false);
    expect(hook.result.current.confirmDialogProps.request?.code).toBe('NEGATIVE_FUND_WARNING');
    expect(hook.result.current.confirmDialogProps.confirmLabel).toBe('Vẫn lưu');

    await act(async () => hook.result.current.confirmDialogProps.onConfirm());

    expect(perform).toHaveBeenCalledTimes(2);
    expect(perform).toHaveBeenLastCalledWith(
      { kind: 'save', flags: { acknowledgeNegativeStock: true, acknowledgeNegativeFund: true } },
      { acknowledgeNegativeStock: true, acknowledgeNegativeFund: true },
    );
    expect(hook.result.current.confirmDialogProps.request).toBeNull();
    expect(toast).not.toHaveBeenCalled();
  });

  it('shows a blocked 422 without resending on confirm', async () => {
    const { perform, hook } = setup();
    perform.mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_BLOCKED'));

    await act(() => hook.result.current.run({ kind: 'delete', flags: {} }));
    expect(hook.result.current.confirmDialogProps.request?.blocked).toBe(true);

    await act(async () => hook.result.current.confirmDialogProps.onConfirm());
    expect(perform).toHaveBeenCalledTimes(1);
    expect(hook.result.current.confirmDialogProps.request).toBeNull();
  });

  it('closes the dialog without resending', async () => {
    const { perform, hook } = setup();
    perform.mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_WARNING'));

    await act(() => hook.result.current.run({ kind: 'save', flags: {} }));
    act(() => hook.result.current.confirmDialogProps.onClose());

    expect(hook.result.current.confirmDialogProps.request).toBeNull();
    expect(perform).toHaveBeenCalledTimes(1);
  });

  it('keeps the flags already confirmed and uses the latest settlement token when the list changed', async () => {
    const { perform, hook } = setup();
    perform
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_WARNING'))
      .mockRejectedValueOnce(apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', { A: ['x'] }, 'token-1'))
      .mockRejectedValueOnce(apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', { A: ['x'], B: ['y'] }, 'token-2'))
      .mockResolvedValueOnce(undefined);

    await act(() => hook.result.current.run({ kind: 'save', flags: {} }));
    await act(async () => hook.result.current.confirmDialogProps.onConfirm());
    expect(hook.result.current.confirmDialogProps.request?.token).toBe('token-1');

    // The list changed between the two confirmations: the second 422 replaces the pending request.
    await act(async () => hook.result.current.confirmDialogProps.onConfirm());
    expect(hook.result.current.confirmDialogProps.request?.token).toBe('token-2');

    await act(async () => hook.result.current.confirmDialogProps.onConfirm());
    expect(perform).toHaveBeenCalledTimes(4);
    expect(perform.mock.calls[2][1]).toEqual({
      acknowledgeNegativeFund: true,
      confirmRemoveSettlements: true,
      confirmedSettlements: 'token-1',
    });
    expect(perform.mock.calls[3][1]).toEqual({
      acknowledgeNegativeFund: true,
      confirmRemoveSettlements: true,
      confirmedSettlements: 'token-2',
    });
  });

  it('reloads on CONCURRENCY and toasts the concurrency message', async () => {
    const { perform, reload, onReloaded, hook } = setup();
    perform.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));

    await act(() => hook.result.current.run({ kind: 'save', flags: {} }));

    expect(reload).toHaveBeenCalledTimes(1);
    expect(onReloaded).toHaveBeenCalledWith({ version: 2 });
    expect(toast).toHaveBeenCalledWith({ variant: 'destructive', title: 'Đã tải lại' });
  });

  it('keeps local edits when the reload fails', async () => {
    const { perform, reload, onReloaded, hook } = setup();
    reload.mockResolvedValue(undefined);
    perform.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));

    await act(() => hook.result.current.run({ kind: 'save', flags: {} }));

    expect(onReloaded).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith({ variant: 'destructive', title: 'Không tải lại được' });
  });

  it('keeps local edits when the reload rejects', async () => {
    const { perform, reload, onReloaded, hook } = setup();
    reload.mockRejectedValue(new Error('offline'));
    perform.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));

    await act(() => hook.result.current.run({ kind: 'save', flags: {} }));

    expect(onReloaded).not.toHaveBeenCalled();
    expect(toast).toHaveBeenCalledWith({ variant: 'destructive', title: 'Không tải lại được' });
    expect(hook.result.current.busy).toBe(false);
  });

  it('falls through to the generic toast on VALIDATION without applyValidationErrors', async () => {
    const { perform, hook } = setup();
    perform.mockRejectedValueOnce(apiError(400, 'VALIDATION', { other: ['Sai'] }));

    await act(() => hook.result.current.run({ kind: 'save', flags: {} }));

    expect(toast).toHaveBeenCalledWith({
      variant: 'destructive',
      title: 'Không thể lưu',
      description: 'Server says VALIDATION; Sai',
    });
  });

  it('toasts the server message when every validation key was mapped', async () => {
    const applyValidationErrors = vi.fn().mockReturnValue(true);
    const { perform, hook } = setup({ applyValidationErrors });
    perform.mockRejectedValueOnce(apiError(400, 'VALIDATION', { fundId: ['Bắt buộc'] }));

    await act(() => hook.result.current.run({ kind: 'save', flags: {} }));

    expect(applyValidationErrors).toHaveBeenCalledWith({ fundId: ['Bắt buộc'] }, { kind: 'save', flags: {} });
    expect(toast).toHaveBeenCalledWith({
      variant: 'destructive',
      title: 'Không thể lưu',
      description: 'Server says VALIDATION',
    });
  });

  it('falls back to the formatted details when a validation key was not mapped', async () => {
    const { perform, hook } = setup({ applyValidationErrors: () => false });
    perform.mockRejectedValueOnce(apiError(400, 'VALIDATION', { other: ['Sai'] }));

    await act(() => hook.result.current.run({ kind: 'delete', flags: {} }));

    expect(toast).toHaveBeenCalledWith({
      variant: 'destructive',
      title: 'Không thể xóa',
      description: 'Server says VALIDATION; Sai',
    });
  });

  it('toasts any other error with its details', async () => {
    const { perform, hook } = setup();
    perform.mockRejectedValueOnce(apiError(403, 'FORBIDDEN'));

    await act(() => hook.result.current.run({ kind: 'delete', flags: {} }));

    expect(toast).toHaveBeenCalledWith({
      variant: 'destructive',
      title: 'Không thể xóa',
      description: 'Server says FORBIDDEN',
    });
    expect(hook.result.current.busy).toBe(false);
  });
});
