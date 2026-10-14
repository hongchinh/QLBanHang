import { describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmDialog } from './confirm-dialog';

function renderDialog() {
  const onConfirm = vi.fn();
  const onOpenChange = vi.fn();
  render(
    <ConfirmDialog
      open
      onOpenChange={onOpenChange}
      title="Hủy phiếu?"
      confirmLabel="Hủy phiếu"
      cancelLabel="Quay lại"
      destructive
      onConfirm={onConfirm}
    />,
  );
  return { onConfirm, onOpenChange };
}

describe('ConfirmDialog', () => {
  it('confirms, or closes without confirming', () => {
    const { onConfirm, onOpenChange } = renderDialog();

    fireEvent.click(screen.getByRole('button', { name: 'Hủy phiếu' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  // Enter held on the page button that opens this dialog: the press opens it, and its first button takes the focus.
  // jsdom has no keyboard activation: a browser clicks a focused button on every Enter keydown that was not cancelled,
  // the auto-repeated ones of a held key included.
  describe('a key held down since before the dialog opened', () => {
    function pressEnter(button: HTMLElement, init: { repeat?: boolean } = {}) {
      if (fireEvent.keyDown(button, { key: 'Enter', ...init })) fireEvent.click(button);
    }

    it.each(['Quay lại', 'Hủy phiếu'])('its repeats do not press "%s"', (name) => {
      const { onConfirm, onOpenChange } = renderDialog();
      const button = screen.getByRole('button', { name });
      act(() => button.focus());

      pressEnter(button, { repeat: true });
      pressEnter(button, { repeat: true });

      expect(onConfirm).not.toHaveBeenCalled();
      expect(onOpenChange).not.toHaveBeenCalled();
    });

    it.each([
      ['Enter', '{Enter}'],
      ['Space', '[Space]'],
    ])('a single press of %s still presses the focused button', async (_name, keys) => {
      const user = userEvent.setup();
      const { onConfirm, onOpenChange } = renderDialog();
      act(() => screen.getByRole('button', { name: 'Quay lại' }).focus());
      await user.keyboard(keys);
      expect(onOpenChange).toHaveBeenCalledWith(false);

      act(() => screen.getByRole('button', { name: 'Hủy phiếu' }).focus());
      await user.keyboard(keys);
      expect(onConfirm).toHaveBeenCalledTimes(1);
    });
  });
});
