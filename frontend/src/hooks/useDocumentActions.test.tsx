import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
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
type DialogProps = ReturnType<typeof useDocumentActions<Action>>['confirmDialogProps'];

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

  // A resend usually takes longer than the dialog's exit: the dialog asks for a target while the save button is still
  // disabled, and only the end of the resend can give the focus back.
  it('after confirming, a resend that settles after the dialog has gone gives the focus back to that element', async () => {
    let answer!: () => void;
    const perform = vi
      .fn<(a: Action) => Promise<void>>()
      .mockImplementationOnce(blurAndReject('NEGATIVE_FUND_WARNING'))
      .mockImplementationOnce(
        () =>
          new Promise<void>((resolve) => {
            (document.activeElement as HTMLElement | null)?.blur();
            answer = resolve;
          }),
      );
    render(<Harness perform={perform} />);
    const save = screen.getByTestId('save');
    act(() => save.focus());

    fireEvent.click(save);
    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'Vẫn lưu' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    // Past the tick in which the closed dialog asks where the focus goes.
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(save).toBeDisabled();
    expect(document.activeElement).toBe(document.body);

    await act(async () => answer());
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

  it('stops watching the focus once the form is gone', () => {
    const add = vi.spyOn(document, 'addEventListener');
    const remove = vi.spyOn(document, 'removeEventListener');
    try {
      const { unmount } = render(<Harness perform={vi.fn()} />);
      const listeners = add.mock.calls.filter(([type, , capture]) => type === 'focusin' && capture === true);
      expect(listeners).toHaveLength(1);
      unmount();
      expect(remove).toHaveBeenCalledWith('focusin', listeners[0][1], true);
    } finally {
      add.mockRestore();
      remove.mockRestore();
    }
  });

  // A Radix AlertDialog has role="alertdialog": it comes and goes like any dialog.
  it('never takes a control of an alert dialog for where the user was', async () => {
    const perform = vi.fn<(a: Action) => Promise<void>>().mockImplementation(blurAndReject('NEGATIVE_FUND_WARNING'));
    function Page() {
      const [alert, setAlert] = useState(true);
      return (
        <>
          <Harness perform={perform} />
          {alert && (
            <div role="alertdialog" aria-label="Thông báo">
              <button type="button" onClick={() => setAlert(false)}>
                OK
              </button>
            </div>
          )}
        </>
      );
    }
    render(<Page />);
    const save = screen.getByTestId('save');
    act(() => save.focus());
    const ok = screen.getByRole('button', { name: 'OK' });
    act(() => ok.focus());
    fireEvent.click(ok);
    expect(ok.isConnected).toBe(false);

    fireEvent.click(save);
    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(save));
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

    // jsdom ignores focus() on a disabled control, so where the focus ends up cannot show whether the dialog tried to
    // give it to that element: the test watches the focus calls themselves.
    it('does not even try to focus that element while it is disabled', async () => {
      render(<LostOriginPage fate="disabled while the action ran" onDiscardDraft={vi.fn()} />);
      const origin = screen.getByLabelText('Mã hàng');
      act(() => origin.focus());
      fireEvent.keyDown(origin, { key: 's', ctrlKey: true });
      await screen.findByRole('dialog');
      expect(origin).toBeDisabled();
      const focus = vi.spyOn(HTMLElement.prototype, 'focus');
      try {
        fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

        await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
        // Past the tick in which the closed dialog hands the focus back.
        await new Promise((resolve) => setTimeout(resolve, 20));
        expect(focus.mock.contexts).not.toContain(origin);
      } finally {
        focus.mockRestore();
      }
    });
  });

  // The sidebar link that opened the form still has the focus when the form mounts. The user was never on it while
  // working in the form, and an Enter there leaves the form.
  it('never gives the focus to what had it before the form opened', async () => {
    const perform = vi.fn<(a: Action) => Promise<void>>().mockRejectedValue(apiError(422, 'NEGATIVE_FUND_WARNING'));
    function Shell({ children }: { children?: ReactNode }) {
      return (
        <>
          <a href="#stock-out">Phiếu xuất</a>
          <main id="main-content">{children}</main>
        </>
      );
    }
    const { rerender } = render(<Shell />);
    const link = screen.getByRole('link', { name: 'Phiếu xuất' });
    act(() => link.focus());
    rerender(
      <Shell>
        <Harness perform={perform} />
      </Shell>,
    );
    expect(document.activeElement).toBe(link);
    // A click on the page takes the focus off the link, and the save button does not take it (as in Safari).
    act(() => link.blur());
    fireEvent.click(screen.getByTestId('save'));
    await screen.findByRole('dialog');
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    // Past the tick in which the closed dialog hands the focus back.
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(document.activeElement).toBe(document.body);
  });

  // The other side of that rule. A form that puts the focus in its first field as it mounts does so before the hook
  // listens. The fields stay enabled while an action runs, so that field still has the focus when the 422 dialog opens,
  // and the dialog itself gives the focus back to what had it then.
  it('a field focused as the form mounts gets the focus back from the dismissed 422 dialog', async () => {
    function FormPage() {
      const first = useRef<HTMLInputElement>(null);
      useLayoutEffect(() => first.current?.focus(), []);
      const { run, confirmDialogProps } = useDocumentActions<Action>({
        ...options,
        perform: async () => {
          throw apiError(422, 'NEGATIVE_FUND_WARNING');
        },
      });
      return (
        <>
          <input
            ref={first}
            aria-label="Khách hàng"
            onKeyDown={(event) => {
              if (event.key === 's' && event.ctrlKey) void run({ kind: 'save', flags: {} });
            }}
          />
          <ConfirmRequiredDialog {...confirmDialogProps} />
        </>
      );
    }
    render(<FormPage />);
    const first = screen.getByLabelText('Khách hàng');
    expect(document.activeElement).toBe(first);

    fireEvent.keyDown(first, { key: 's', ctrlKey: true });
    await screen.findByRole('dialog');
    expect(document.activeElement).not.toBe(first);
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(first));
  });

  // The app header and the sidebar sit outside main#main-content. Escape takes the focus off the header search, and
  // Ctrl+S on <body> saves (the stock voucher page): the header is not where the user was in the page.
  describe('a control outside the page area', () => {
    function WithHeader({ children }: { children: ReactNode }) {
      return (
        <>
          <input aria-label="Tìm kiếm" />
          <main id="main-content">{children}</main>
        </>
      );
    }

    function leaveHeaderSearch() {
      const search = screen.getByLabelText('Tìm kiếm');
      act(() => search.focus());
      act(() => search.blur());
    }

    it('does not get the focus when an action started from <body> ends', async () => {
      const perform = vi.fn(async () => {});
      render(
        <WithHeader>
          <Harness perform={perform} />
        </WithHeader>,
      );
      leaveHeaderSearch();

      // As Ctrl+S on <body>: nothing has the focus when the action starts.
      fireEvent.click(screen.getByTestId('save'));
      await waitFor(() => expect(perform).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(screen.getByTestId('save')).toBeEnabled());
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(document.activeElement).toBe(document.body);
    });

    it('does not get the focus when the 422 dialog of such an action is dismissed', async () => {
      const perform = vi.fn<(a: Action) => Promise<void>>().mockRejectedValue(apiError(422, 'NEGATIVE_FUND_WARNING'));
      render(
        <WithHeader>
          <Harness perform={perform} />
        </WithHeader>,
      );
      leaveHeaderSearch();

      fireEvent.click(screen.getByTestId('save'));
      await screen.findByRole('dialog');
      fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      // Past the tick in which the closed dialog hands the focus back.
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(document.activeElement).toBe(document.body);
    });

    it('does not count: the dismissed 422 dialog gives the focus back to where the user was in the page before', async () => {
      const perform = vi.fn<(a: Action) => Promise<void>>().mockRejectedValue(apiError(422, 'NEGATIVE_FUND_WARNING'));
      render(
        <WithHeader>
          <Harness perform={perform} />
        </WithHeader>,
      );
      const save = screen.getByTestId('save');
      act(() => save.focus());
      leaveHeaderSearch();

      fireEvent.click(save);
      await screen.findByRole('dialog');
      fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));

      await waitFor(() => expect(document.activeElement).toBe(save));
      expect(perform).toHaveBeenCalledTimes(1);
    });

    // The user had left the page's controls (Escape out of the header search) and pressed Ctrl+S on <body>. The action
    // took the focus from nothing, so it gives nothing back: a control the user had left getting the focus would take
    // a blind Enter (the save button would save again).
    it('an action started from <body> leaves the focus there, even after the user was on a page control', async () => {
      const perform = vi.fn(async () => {});
      render(
        <WithHeader>
          <Harness perform={perform} />
        </WithHeader>,
      );
      act(() => screen.getByTestId('save').focus());
      leaveHeaderSearch();

      fireEvent.click(screen.getByTestId('save'));
      await waitFor(() => expect(perform).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(screen.getByTestId('save')).toBeEnabled());
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(document.activeElement).toBe(document.body);
    });
  });

  // While a dialog, menu or popover is open, Radix puts focus guards at the edges of <body>. A Tab can land on one
  // (from the browser toolbar) before the focus trap moves it on: that is not where the user was either.
  it('never takes a Radix focus guard for where the user was', async () => {
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
    const guard = document.querySelector<HTMLElement>('[data-radix-focus-guard]');
    expect(guard).not.toBeNull();
    act(() => guard!.focus());

    fireEvent.click(screen.getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(perform).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(document.activeElement).toBe(save));
  });

  // In a browser the dialog stays on the page for its exit animation (about 150 ms) and only then asks where the focus
  // goes; a fast answer to the confirmed resend is there first. jsdom has no animations, so the test plays the dialog:
  // it confirms, lets the resend settle, and then asks.
  describe('the 422 dialog asks for the focus target after its confirmed resend has settled', () => {
    // A cancel that needs a confirmation: resolves once the confirmed resend has settled.
    async function cancelConfirmAndSettle(outcome: 'the voucher stays as it was' | 'the voucher is cancelled') {
      let dialogProps!: DialogProps;
      let attempts = 0;
      function CancelPage() {
        const [cancelled, setCancelled] = useState(false);
        const { busy, run, confirmDialogProps } = useDocumentActions<Action>({
          ...options,
          perform: async () => {
            attempts += 1;
            // A real browser drops the focus of a button that gets disabled.
            (document.activeElement as HTMLElement | null)?.blur();
            if (attempts === 1) throw apiError(422, 'NEGATIVE_FUND_WARNING');
            if (outcome === 'the voucher is cancelled') setCancelled(true);
          },
        });
        dialogProps = confirmDialogProps;
        return (
          <main id="main-content">
            {/* A cancelled voucher: "Khôi phục" comes first and "Hủy phiếu" is gone. */}
            {cancelled && <button type="button">Khôi phục</button>}
            <button type="button" disabled={busy}>
              Cập nhật
            </button>
            {!cancelled && (
              <button type="button" disabled={busy} onClick={() => void run({ kind: 'delete', flags: {} })}>
                Hủy phiếu
              </button>
            )}
          </main>
        );
      }
      render(<CancelPage />);
      const origin = screen.getByRole('button', { name: 'Hủy phiếu' });
      act(() => origin.focus());
      fireEvent.click(origin);
      await waitFor(() => expect(dialogProps.request).not.toBeNull());
      act(() => dialogProps.onConfirm());
      await waitFor(() => expect(attempts).toBe(2));
      await waitFor(() => expect(screen.getByRole('button', { name: 'Cập nhật' })).toBeEnabled());
      // What the dialog calls when it is gone: the function of the latest render.
      return { origin, restoreFocusTo: () => dialogProps.restoreFocusTo() };
    }

    it('gets the element the action started from when it is usable again', async () => {
      const { origin, restoreFocusTo } = await cancelConfirmAndSettle('the voucher stays as it was');

      expect(restoreFocusTo()).toBe(origin);
    });

    it('gets nothing when that element is gone: never another control of the page', async () => {
      const { restoreFocusTo } = await cancelConfirmAndSettle('the voucher is cancelled');

      expect(screen.getByRole('button', { name: 'Khôi phục' })).toBeEnabled();
      expect(restoreFocusTo()).toBeNull();
      expect(document.activeElement).toBe(document.body);
    });
  });

  // Browser Back with the 422 dialog open: the page and its dialog go, and the closed dialog still asks where the focus
  // goes a tick later. The hook of a page that is gone must not put the focus anywhere.
  describe('once the page was replaced while the 422 dialog was open', () => {
    // The search box of the app shell stays when the page changes, and so would a control kept in the page area
    // around the routed page (the filter box here).
    function Shell({ page, children }: { page: 'form' | 'list'; children: ReactNode }) {
      return (
        <>
          <input aria-label="Tìm kiếm" />
          <main id="main-content">
            <input aria-label="Lọc" />
            {page === 'form' ? children : <button type="button">Thêm phiếu</button>}
          </main>
        </>
      );
    }

    it('the hook names no focus target any more, not even a control still in the page area', async () => {
      let dialogProps!: DialogProps;
      function FormPage() {
        const { run, confirmDialogProps } = useDocumentActions<Action>({
          ...options,
          perform: async () => {
            throw apiError(422, 'NEGATIVE_FUND_WARNING');
          },
        });
        dialogProps = confirmDialogProps;
        return (
          <button type="button" data-testid="save" onClick={() => void run({ kind: 'save', flags: {} })}>
            Lưu
          </button>
        );
      }
      const { rerender } = render(
        <Shell page="form">
          <FormPage />
        </Shell>,
      );
      const filter = screen.getByLabelText('Lọc');
      act(() => filter.focus());
      // A click that leaves the focus where it was, as Safari does for a button.
      fireEvent.click(screen.getByTestId('save'));
      await waitFor(() => expect(dialogProps.request).not.toBeNull());
      expect(dialogProps.restoreFocusTo()).toBe(filter);

      rerender(
        <Shell page="list">
          <FormPage />
        </Shell>,
      );

      expect(dialogProps.restoreFocusTo()).toBeNull();
    });

    it('nothing is focused: the focus stays where it is', async () => {
      const perform = vi.fn<(a: Action) => Promise<void>>().mockRejectedValue(apiError(422, 'NEGATIVE_FUND_WARNING'));
      const { rerender } = render(
        <Shell page="form">
          <Harness perform={perform} />
        </Shell>,
      );
      // The user was last in the search box, then clicked on the page: nothing has the focus when the dialog opens.
      const search = screen.getByLabelText('Tìm kiếm');
      act(() => search.focus());
      act(() => search.blur());
      fireEvent.click(screen.getByTestId('save'));
      await screen.findByRole('dialog');

      rerender(
        <Shell page="list">
          <Harness perform={perform} />
        </Shell>,
      );
      expect(screen.queryByRole('dialog')).toBeNull();
      // Past the tick in which the closed dialog hands the focus back.
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(document.activeElement).toBe(document.body);
    });

    // The dialog itself gives the focus back to what had it when it opened when the hook names nothing. Not here:
    // the dialog did not close, it went away with its page.
    it('nothing is focused, even when the search box had the focus as the dialog opened', async () => {
      const perform = vi.fn<(a: Action) => Promise<void>>().mockRejectedValue(apiError(422, 'NEGATIVE_FUND_WARNING'));
      const { rerender } = render(
        <Shell page="form">
          <Harness perform={perform} />
        </Shell>,
      );
      const search = screen.getByLabelText('Tìm kiếm');
      act(() => search.focus());
      // A click that leaves the focus where it was, as Safari does for a button.
      fireEvent.click(screen.getByTestId('save'));
      await screen.findByRole('dialog');
      expect(document.activeElement).not.toBe(search);

      rerender(
        <Shell page="list">
          <Harness perform={perform} />
        </Shell>,
      );
      expect(screen.queryByRole('dialog')).toBeNull();
      // Past the tick in which the closed dialog hands the focus back.
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(document.activeElement).toBe(document.body);
    });
  });

  // The buttons of the page are disabled while an action runs, so a real browser drops the focus of the one that was
  // used: simulated with blur() inside perform (jsdom keeps the focus on a disabled button).
  describe('after an action that needs no confirmation', () => {
    const dropFocus = () => (document.activeElement as HTMLElement | null)?.blur();

    it.each([
      ['a success', async () => {}],
      [
        'a server error (500)',
        async () => {
          throw apiError(500, 'INTERNAL');
        },
      ],
      [
        'a version conflict (409)',
        async () => {
          throw apiError(409, 'CONCURRENCY');
        },
      ],
    ])('the focus is back on the button the action started from after %s', async (_outcome, answer) => {
      const perform = vi.fn(async () => {
        dropFocus();
        await answer();
      });
      render(<Harness perform={perform} />);
      const save = screen.getByTestId('save');
      act(() => save.focus());

      fireEvent.click(save);
      expect(document.activeElement).toBe(document.body);

      await waitFor(() => expect(document.activeElement).toBe(save));
      expect(perform).toHaveBeenCalledTimes(1);
    });

    it('does not take the focus from where the page put it meanwhile', async () => {
      // The page moves the focus itself, e.g. to the first field the server rejected.
      const perform = vi.fn(async () => {
        dropFocus();
        screen.getByTestId('other').focus();
        throw apiError(500, 'INTERNAL');
      });
      render(
        <>
          <input data-testid="other" />
          <Harness perform={perform} />
        </>,
      );
      const save = screen.getByTestId('save');
      act(() => save.focus());
      fireEvent.click(save);
      expect(save).toBeDisabled();

      await waitFor(() => expect(save).toBeEnabled());
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(document.activeElement).toBe(screen.getByTestId('other'));
    });

    // E.g. the confirm dialog the action started from, still on the page for its exit animation.
    it('does not pull the focus out of a dialog', async () => {
      let answer!: () => void;
      const perform = vi.fn(() => {
        dropFocus();
        return new Promise<void>((resolve) => (answer = resolve));
      });
      render(
        <>
          <Harness perform={perform} />
          <div role="dialog" aria-label="Xóa phiếu?">
            <button type="button" data-testid="in-dialog">
              Xóa
            </button>
          </div>
        </>,
      );
      const save = screen.getByTestId('save');
      const inDialog = screen.getByTestId('in-dialog');
      act(() => save.focus());
      fireEvent.click(save);
      act(() => inDialog.focus());

      await act(async () => answer());
      await waitFor(() => expect(save).toBeEnabled());
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(document.activeElement).toBe(inDialog);
    });

    it('focuses nothing when that button is gone once the action ended', async () => {
      function CancelPage() {
        const [cancelled, setCancelled] = useState(false);
        const { busy, run } = useDocumentActions<Action>({
          ...options,
          perform: async () => {
            dropFocus();
            setCancelled(true);
          },
        });
        return (
          <main id="main-content">
            {cancelled && <button type="button">Khôi phục</button>}
            {!cancelled && (
              <button type="button" disabled={busy} onClick={() => void run({ kind: 'delete', flags: {} })}>
                Hủy phiếu
              </button>
            )}
          </main>
        );
      }
      render(<CancelPage />);
      const cancel = screen.getByRole('button', { name: 'Hủy phiếu' });
      act(() => cancel.focus());
      fireEvent.click(cancel);

      await screen.findByRole('button', { name: 'Khôi phục' });
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(document.activeElement).toBe(document.body);
    });

    // The dialog takes the focus itself and gives it back when it closes: nothing to do in between. In a browser the
    // dialog and the enabled buttons come in the same render. The page here only shows the request: in jsdom the real
    // dialog would open and take the focus a render before the buttons are enabled again, and hide a focus call.
    it('leaves the focus to the 422 dialog when the action ends with one', async () => {
      function RequestOnlyPage() {
        const { busy, run, confirmDialogProps } = useDocumentActions<Action>({
          ...options,
          perform: blurAndReject('NEGATIVE_FUND_WARNING'),
        });
        return (
          <>
            <button type="button" data-testid="save" disabled={busy} onClick={() => void run({ kind: 'save', flags: {} })}>
              Lưu
            </button>
            {confirmDialogProps.request && <p>{confirmDialogProps.request.title}</p>}
          </>
        );
      }
      render(<RequestOnlyPage />);
      const save = screen.getByTestId('save');
      act(() => save.focus());
      const focus = vi.spyOn(HTMLElement.prototype, 'focus');
      try {
        fireEvent.click(save);
        await screen.findByText('Quỹ bị âm');
        await waitFor(() => expect(save).toBeEnabled());
        await new Promise((resolve) => setTimeout(resolve, 20));

        expect(focus.mock.contexts).not.toContain(save);
        expect(document.activeElement).toBe(document.body);
      } finally {
        focus.mockRestore();
      }
    });

    // jsdom has no keyboard activation. A browser clicks a focused button on every Enter keydown that was not
    // cancelled, the auto-repeated ones of a held key included.
    function pressEnter(button: HTMLElement, init: { repeat?: boolean } = {}) {
      if (fireEvent.keyDown(button, { key: 'Enter', ...init })) fireEvent.click(button);
    }

    it('a key that is still held down does not press the button again when it has the focus back', async () => {
      const perform = vi.fn(async () => dropFocus());
      render(<Harness perform={perform} />);
      const save = screen.getByTestId('save');
      act(() => save.focus());

      pressEnter(save);
      await waitFor(() => expect(document.activeElement).toBe(save));
      pressEnter(save, { repeat: true });
      pressEnter(save, { repeat: true });
      expect(perform).toHaveBeenCalledTimes(1);

      // Released and pressed again: a save the user asked for.
      fireEvent.keyUp(save, { key: 'Enter' });
      pressEnter(save);
      await waitFor(() => expect(perform).toHaveBeenCalledTimes(2));
    });
  });
});
