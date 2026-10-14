import { describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ConfirmationRequest } from '@/lib/confirm-required';
import { ConfirmRequiredDialog } from './confirm-required-dialog';

const warning: ConfirmationRequest = {
  code: 'CREDIT_LIMIT_WARNING',
  flag: 'acknowledgeCreditLimit',
  blocked: false,
  title: 'Vượt hạn mức nợ',
  message: 'Khách hàng sẽ vượt hạn mức nợ.',
  items: [
    { key: 'KH01', messages: ['Dư nợ 9.000.000 vượt hạn mức 5.000.000'] },
    { key: 'KH02', messages: ['Quá hạn 12 ngày'] },
  ],
};

const blocked: ConfirmationRequest = {
  code: 'NEGATIVE_FUND_BLOCKED',
  blocked: true,
  title: 'Không đủ tiền trong quỹ',
  message: 'Không đủ tiền trong quỹ.',
  items: [{ key: 'QTM', messages: ['Âm 1.000'] }],
};

describe('ConfirmRequiredDialog', () => {
  it('renders items and confirms', () => {
    const onConfirm = vi.fn();
    const onClose = vi.fn();
    render(<ConfirmRequiredDialog request={warning} onConfirm={onConfirm} onClose={onClose} />);

    expect(screen.getByText('Vượt hạn mức nợ')).toBeInTheDocument();
    expect(screen.getByText('Khách hàng sẽ vượt hạn mức nợ.')).toBeInTheDocument();
    expect(screen.getByText('KH01')).toBeInTheDocument();
    expect(screen.getByText('Dư nợ 9.000.000 vượt hạn mức 5.000.000')).toBeInTheDocument();
    expect(screen.getByText('KH02')).toBeInTheDocument();
    expect(screen.getByText('Quá hạn 12 ngày')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Vẫn tiếp tục' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('uses a custom confirm label', () => {
    render(
      <ConfirmRequiredDialog request={warning} confirmLabel="Vẫn lưu" onConfirm={vi.fn()} onClose={vi.fn()} />,
    );

    expect(screen.getByRole('button', { name: 'Vẫn lưu' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Vẫn tiếp tục' })).not.toBeInTheDocument();
  });

  it('blocked shows only close', () => {
    const onClose = vi.fn();
    render(<ConfirmRequiredDialog request={blocked} onConfirm={vi.fn()} onClose={onClose} />);

    expect(screen.getByText('Không đủ tiền trong quỹ')).toBeInTheDocument();
    expect(screen.getByText('Không đủ tiền trong quỹ.')).toBeInTheDocument();
    expect(screen.getByText('QTM')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Vẫn tiếp tục' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Quay lại' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Đóng' }));
    expect(onClose).toHaveBeenCalled();
  });

  it('renders nothing without a request', () => {
    render(<ConfirmRequiredDialog request={null} onConfirm={vi.fn()} onClose={vi.fn()} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  // Enter held on the save button: the press opens this dialog, and its first button takes the focus. jsdom has no
  // keyboard activation: a browser clicks a focused button on every Enter keydown that was not cancelled, the
  // auto-repeated ones of a held key included.
  describe('a key held down since before the dialog opened', () => {
    function pressEnter(button: HTMLElement, init: { repeat?: boolean } = {}) {
      if (fireEvent.keyDown(button, { key: 'Enter', ...init })) fireEvent.click(button);
    }

    it.each([
      ['Quay lại', warning],
      ['Vẫn tiếp tục', warning],
      ['Đóng', blocked],
    ])('its repeats do not press "%s"', (name, request) => {
      const onConfirm = vi.fn();
      const onClose = vi.fn();
      render(<ConfirmRequiredDialog request={request} onConfirm={onConfirm} onClose={onClose} />);
      const button = screen.getByRole('button', { name });
      act(() => button.focus());

      pressEnter(button, { repeat: true });
      pressEnter(button, { repeat: true });

      expect(onConfirm).not.toHaveBeenCalled();
      expect(onClose).not.toHaveBeenCalled();
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it.each([
      ['Enter', '{Enter}'],
      ['Space', '[Space]'],
    ])('a single press of %s still presses the focused button', async (_name, keys) => {
      const user = userEvent.setup();
      const onConfirm = vi.fn();
      const onClose = vi.fn();
      render(<ConfirmRequiredDialog request={warning} onConfirm={onConfirm} onClose={onClose} />);
      act(() => screen.getByRole('button', { name: 'Quay lại' }).focus());
      await user.keyboard(keys);
      expect(onClose).toHaveBeenCalledTimes(1);

      act(() => screen.getByRole('button', { name: 'Vẫn tiếp tục' }).focus());
      await user.keyboard(keys);
      expect(onConfirm).toHaveBeenCalledTimes(1);
    });
  });

  it('closing gives the focus to the element restoreFocusTo returns', async () => {
    const target = document.createElement('input');
    document.body.appendChild(target);
    try {
      const { rerender } = render(
        <ConfirmRequiredDialog request={warning} onConfirm={vi.fn()} onClose={vi.fn()} restoreFocusTo={() => target} />,
      );
      expect(document.activeElement).not.toBe(target);

      rerender(<ConfirmRequiredDialog request={null} onConfirm={vi.fn()} onClose={vi.fn()} restoreFocusTo={() => target} />);

      await waitFor(() => expect(document.activeElement).toBe(target));
    } finally {
      target.remove();
    }
  });
});
