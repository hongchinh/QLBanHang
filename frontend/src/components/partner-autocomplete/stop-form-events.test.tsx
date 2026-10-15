import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import { StopFormEvents } from './stop-form-events';

function setup() {
  const onFormSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
  const onFormKey = vi.fn();
  const innerSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
  const onOpenChange = vi.fn();
  render(
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- the test stands in for the voucher form.
    <form onSubmit={onFormSubmit} onKeyDown={onFormKey}>
      <input data-testid="outside" />
      <StopFormEvents>
        <Dialog open onOpenChange={onOpenChange}>
          <DialogContent>
            <DialogTitle>T</DialogTitle>
            <form onSubmit={innerSubmit}>
              <input data-testid="first" />
              <button type="submit">ok</button>
            </form>
          </DialogContent>
        </Dialog>
      </StopFormEvents>
    </form>,
  );
  return { onFormSubmit, onFormKey, innerSubmit, onOpenChange };
}

describe('StopFormEvents', () => {
  it('keeps submit and keys of the dialog away from the voucher form', async () => {
    const user = userEvent.setup();
    const { onFormSubmit, onFormKey, innerSubmit } = setup();

    await user.click(screen.getByRole('button', { name: 'ok' }));
    expect(innerSubmit).toHaveBeenCalledTimes(1);

    await user.type(screen.getByTestId('first'), '{Enter}');
    expect(innerSubmit).toHaveBeenCalledTimes(2);

    fireEvent.keyDown(screen.getByTestId('first'), { key: 's', ctrlKey: true });

    expect(onFormSubmit).not.toHaveBeenCalled();
    expect(onFormKey).not.toHaveBeenCalled();
  });

  it('covers the close button too', () => {
    const { onFormKey } = setup();
    const close = screen.getByRole('button', { name: 'Đóng' });

    fireEvent.keyDown(close, { key: 'Enter' });
    fireEvent.keyDown(close, { key: 's', ctrlKey: true });

    expect(onFormKey).not.toHaveBeenCalled();
  });

  it('keeps the Tab loop of the dialog working', () => {
    setup();
    const close = screen.getByRole('button', { name: 'Đóng' });
    const first = screen.getByTestId('first');

    close.focus();
    fireEvent.keyDown(close, { key: 'Tab' });
    expect(first).toHaveFocus();

    fireEvent.keyDown(first, { key: 'Tab', shiftKey: true });
    expect(close).toHaveFocus();
  });

  it('Escape still closes the dialog', async () => {
    const { onOpenChange } = setup();

    await userEvent.setup().keyboard('{Escape}');

    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('a key outside the dialog still reaches the voucher form', () => {
    const { onFormKey } = setup();

    fireEvent.keyDown(screen.getByTestId('outside'), { key: 'Enter' });

    expect(onFormKey).toHaveBeenCalledTimes(1);
  });

  it('adds no layout box', () => {
    setup();
    const wrapper = screen.getByTestId('outside').nextElementSibling as HTMLElement;

    expect(getComputedStyle(wrapper).display).toBe('contents');
  });
});
