import { useState } from 'react';
import { act, fireEvent, render, renderHook, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from '@/lib/use-toast';
import type { ConfirmFlags } from '@/lib/confirm-required';
import { ConfirmRequiredDialog } from '@/components/confirm-required-dialog/confirm-required-dialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
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

describe('useDocumentActions focus return', () => {
  // The save button is disabled while busy, so a real browser blurs it: simulated with blur() inside perform.
  function Harness({ perform }: { perform: (a: Action) => Promise<void> }) {
    const { busy, run, confirmDialogProps } = useDocumentActions<Action>({
      perform,
      reload: async () => undefined,
      onReloaded: () => {},
      errorTitles,
      confirmLabels,
      concurrencyMessage: 'Đã tải lại',
      concurrencyReloadFailedMessage: 'Không tải lại được',
    });
    return (
      <>
        <button type="button" data-testid="save" disabled={busy} onClick={() => void run({ kind: 'save', flags: {} })}>
          Lưu
        </button>
        <ConfirmRequiredDialog {...confirmDialogProps} />
      </>
    );
  }

  const blurAndReject = (code: string) => async () => {
    (document.activeElement as HTMLElement | null)?.blur();
    throw apiError(422, code);
  };

  it('dismissing the dialog gives the focus back to what had it when the action started', async () => {
    const perform = vi.fn<(a: Action) => Promise<void>>().mockImplementation(blurAndReject('NEGATIVE_FUND_WARNING'));
    render(<Harness perform={perform} />);
    const save = screen.getByTestId('save');
    act(() => save.focus());

    fireEvent.click(save);
    await screen.findByRole('dialog');
    expect(document.activeElement).not.toBe(save);
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(document.activeElement).toBe(save);
  });

  it('after confirming, the focus is back on that element once the action settled', async () => {
    const perform = vi
      .fn<(a: Action) => Promise<void>>()
      .mockImplementationOnce(blurAndReject('NEGATIVE_FUND_WARNING'))
      .mockImplementationOnce(async () => {
        (document.activeElement as HTMLElement | null)?.blur();
      });
    render(<Harness perform={perform} />);
    const save = screen.getByTestId('save');
    act(() => save.focus());

    fireEvent.click(save);
    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(perform).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(document.activeElement).toBe(save));
  });

  it('does not take the focus from an element the user moved to meanwhile', async () => {
    const perform = vi
      .fn<(a: Action) => Promise<void>>()
      .mockImplementationOnce(blurAndReject('NEGATIVE_FUND_WARNING'))
      .mockImplementationOnce(async () => {});
    render(
      <>
        <input data-testid="other" />
        <Harness perform={perform} />
      </>,
    );
    const save = screen.getByTestId('save');
    act(() => save.focus());
    fireEvent.click(save);
    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'Vẫn lưu' }));
    const other = screen.getByTestId('other');
    act(() => other.focus());

    await waitFor(() => expect(perform).toHaveBeenCalledTimes(2));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(document.activeElement).toBe(other);
  });

  // The same rule as any dialog opened from state: back to what had the focus when it opened.
  it('dismissing gives the focus back to where the user went while the action ran, not to where it started', async () => {
    const perform = vi.fn<(a: Action) => Promise<void>>().mockImplementation(async () => {
      await new Promise((resolve) => setTimeout(resolve, 20));
      throw apiError(422, 'NEGATIVE_FUND_WARNING');
    });
    render(
      <>
        <input data-testid="other" />
        <Harness perform={perform} />
      </>,
    );
    const save = screen.getByTestId('save');
    const other = screen.getByTestId('other');
    act(() => save.focus());
    fireEvent.click(save);
    act(() => other.focus());

    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(other));
  });

  it('still knows where the user went when that control stops its focus event', async () => {
    const perform = vi.fn<(a: Action) => Promise<void>>().mockImplementation(async () => {
      await new Promise((resolve) => setTimeout(resolve, 20));
      throw apiError(422, 'NEGATIVE_FUND_WARNING');
    });
    render(
      <>
        <input data-testid="other" />
        <Harness perform={perform} />
      </>,
    );
    const save = screen.getByTestId('save');
    const other = screen.getByTestId('other');
    other.addEventListener('focusin', (event) => event.stopPropagation());
    act(() => save.focus());
    fireEvent.click(save);
    act(() => other.focus());

    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(other));
  });

  const options = {
    reload: async () => undefined,
    onReloaded: () => {},
    errorTitles,
    confirmLabels,
    concurrencyMessage: 'Đã tải lại',
    concurrencyReloadFailedMessage: 'Không tải lại được',
  };

  // A cancel / delete / restore is confirmed in a ConfirmDialog first, so the action starts from a button of a dialog
  // that is closing. A save button sits first in the page: a blind Enter on it would save.
  it('an action confirmed in a dialog returns the focus to the page button that asked for it, not to the first control', async () => {
    function ViaConfirmHarness() {
      const [asking, setAsking] = useState(false);
      const { busy, run, confirmDialogProps } = useDocumentActions<Action>({
        ...options,
        // The answer takes a while: the page buttons are disabled when the confirm dialog gives the focus back.
        perform: async () => {
          await new Promise((resolve) => setTimeout(resolve, 20));
          throw apiError(422, 'NEGATIVE_FUND_WARNING');
        },
      });
      return (
        <main id="main-content">
          <button type="button" data-testid="save" disabled={busy}>
            Cập nhật
          </button>
          <button type="button" data-testid="delete" disabled={busy} onClick={() => setAsking(true)}>
            Xóa
          </button>
          <ConfirmDialog
            open={asking}
            onOpenChange={setAsking}
            title="Xóa phiếu?"
            confirmLabel="Xóa luôn"
            onConfirm={() => {
              setAsking(false);
              void run({ kind: 'delete', flags: {} });
            }}
          />
          <ConfirmRequiredDialog {...confirmDialogProps} />
        </main>
      );
    }
    render(<ViaConfirmHarness />);
    const remove = screen.getByTestId('delete');
    act(() => remove.focus());
    fireEvent.click(remove);
    const confirm = await screen.findByRole('button', { name: 'Xóa luôn' });
    act(() => confirm.focus());
    fireEvent.click(confirm);

    await screen.findByText('Quỹ bị âm');
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(remove));
    expect(document.activeElement).not.toBe(screen.getByTestId('save'));
  });

  // The first control of a new stock voucher is "Xóa nháp": one press deletes the draft and resets the form, with no
  // question asked. The focus must never land there, or on any control the user was not on.
  describe('when the element the user was on is gone or disabled', () => {
    const fates = [
      // Ctrl+S from a blank trailing row: the page drops the row first, then starts the save.
      'removed before the action started',
      // A status change takes a control away (a cancelled voucher has no "Hủy phiếu") or leaves it disabled.
      'removed while the action ran',
      'disabled while the action ran',
    ] as const;

    function LostOriginPage({ fate, onDiscardDraft }: { fate: (typeof fates)[number]; onDiscardDraft: () => void }) {
      const [lost, setLost] = useState(false);
      const { run, confirmDialogProps } = useDocumentActions<Action>({
        ...options,
        perform: async () => {
          setLost(true);
          // A real browser drops the focus of a control that gets disabled or removed.
          (document.activeElement as HTMLElement | null)?.blur();
          throw apiError(422, 'NEGATIVE_FUND_WARNING');
        },
      });
      const removed = lost && fate !== 'disabled while the action ran';
      return (
        <main id="main-content">
          <button type="button" aria-label="Xóa nháp" onClick={onDiscardDraft}>
            ×
          </button>
          <button type="button">Lưu và thoát</button>
          {!removed && (
            <input
              aria-label="Mã hàng"
              disabled={lost}
              onKeyDown={(event) => {
                if (event.key !== 's' || !event.ctrlKey) return;
                if (fate === 'removed before the action started') setLost(true);
                setTimeout(() => void run({ kind: 'save', flags: {} }), 0);
              }}
            />
          )}
          <ConfirmRequiredDialog {...confirmDialogProps} />
        </main>
      );
    }

    it.each(fates)('focuses nothing (%s): never another control of the page', async (fate) => {
      const onDiscardDraft = vi.fn();
      render(<LostOriginPage fate={fate} onDiscardDraft={onDiscardDraft} />);
      const origin = screen.getByLabelText('Mã hàng');
      act(() => origin.focus());
      fireEvent.keyDown(origin, { key: 's', ctrlKey: true });
      await screen.findByRole('dialog');
      fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      // Past the tick in which the closed dialog hands the focus back.
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(document.activeElement).toBe(document.body);
      // So a blind Enter runs nothing.
      await userEvent.keyboard('{Enter}');
      expect(onDiscardDraft).not.toHaveBeenCalled();
    });
  });
});
