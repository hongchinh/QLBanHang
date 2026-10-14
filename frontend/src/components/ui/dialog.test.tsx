import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Dialog, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from './dialog';

// Opened from state, with no DialogTrigger: Radix has no trigger to give the focus back to.
function StateDialog({ opener }: { opener?: 'remove' | 'disable' }) {
  const [open, setOpen] = useState(false);
  const [removed, setRemoved] = useState(false);
  const [used, setUsed] = useState(false);
  return (
    <>
      {!removed && (
        <button type="button" data-testid="opener" disabled={opener === 'disable' && used}
          onClick={() => {
            setUsed(true);
            setOpen(true);
          }}>
          Mở
        </button>
      )}
      <button type="button">Khác</button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogTitle>Tiêu đề</DialogTitle>
          <DialogDescription>Mô tả</DialogDescription>
          <button type="button" onClick={() => setOpen(false)}>Xong</button>
          {opener === 'remove' && (
            <button type="button" onClick={() => setRemoved(true)}>Gỡ</button>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}

describe('Dialog focus return', () => {
  it('gives the focus back to the element focused when it opened, without a trigger', async () => {
    render(<StateDialog />);
    const opener = screen.getByTestId('opener');
    act(() => opener.focus());
    fireEvent.click(opener);
    await screen.findByText('Tiêu đề');
    expect(document.activeElement).not.toBe(opener);

    fireEvent.keyDown(document.activeElement!, { key: 'Escape' });

    await waitFor(() => expect(screen.queryByText('Tiêu đề')).toBeNull());
    expect(document.activeElement).toBe(opener);
  });

  // Radix asks where the focus goes a tick after the dialog has gone (after its exit animation in a browser). A page
  // that moves the focus itself once the dialog is closed (the next field after a pick) must keep it.
  it('does not take the focus back from an element that got it once the dialog had closed', async () => {
    render(<StateDialog />);
    const opener = screen.getByTestId('opener');
    act(() => opener.focus());
    fireEvent.click(opener);
    await screen.findByText('Tiêu đề');

    fireEvent.click(screen.getByRole('button', { name: 'Xong' }));
    expect(screen.queryByText('Tiêu đề')).toBeNull();
    const other = screen.getByRole('button', { name: 'Khác' });
    act(() => other.focus());
    // Past the tick in which the closed dialog hands the focus back.
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(document.activeElement).toBe(other);
  });

  // With a DialogTrigger Radix would put the focus back on the trigger itself, whatever has it by then.
  it('does not take the focus back to its trigger from an element that got it once the dialog had closed', async () => {
    render(
      <>
        <Dialog>
          <DialogTrigger asChild>
            <button type="button">Mở</button>
          </DialogTrigger>
          <DialogContent>
            <DialogTitle>Tiêu đề</DialogTitle>
            <DialogDescription>Mô tả</DialogDescription>
          </DialogContent>
        </Dialog>
        <button type="button">Khác</button>
      </>,
    );
    const trigger = screen.getByRole('button', { name: 'Mở' });
    act(() => trigger.focus());
    fireEvent.click(trigger);
    await screen.findByText('Tiêu đề');

    fireEvent.click(screen.getByRole('button', { name: 'Đóng' }));
    expect(screen.queryByText('Tiêu đề')).toBeNull();
    const other = screen.getByRole('button', { name: 'Khác' });
    act(() => other.focus());
    // Past the tick in which the closed dialog hands the focus back.
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(document.activeElement).toBe(other);
  });

  // Enter held on the opener: the press opens the dialog, a repeat presses its first button, which closes it, and the
  // focus is back on the opener. jsdom has no keyboard activation: a browser clicks a focused button on every Enter
  // keydown that was not cancelled, the auto-repeated ones of a held key included.
  it('a key still held down when the focus is back on the opener does not open the dialog again', async () => {
    const pressEnter = (button: HTMLElement, init: { repeat?: boolean } = {}) => {
      if (fireEvent.keyDown(button, { key: 'Enter', ...init })) fireEvent.click(button);
    };
    render(<StateDialog />);
    const opener = screen.getByTestId('opener');
    act(() => opener.focus());
    pressEnter(opener);
    await screen.findByText('Tiêu đề');
    pressEnter(screen.getByRole('button', { name: 'Xong' }), { repeat: true });
    await waitFor(() => expect(screen.queryByText('Tiêu đề')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(opener));

    pressEnter(opener, { repeat: true });
    pressEnter(opener, { repeat: true });
    expect(screen.queryByText('Tiêu đề')).toBeNull();

    // Released and pressed again: the user opens it.
    fireEvent.keyUp(opener, { key: 'Enter' });
    pressEnter(opener);
    await screen.findByText('Tiêu đề');
  });

  // jsdom ignores focus() on a disabled button, so where the focus ends up cannot show whether the dialog tried to give
  // it to the opener: the test watches the focus calls themselves.
  it('does not focus an opener that is disabled when the dialog closes', async () => {
    render(<StateDialog opener="disable" />);
    const opener = screen.getByTestId('opener');
    act(() => opener.focus());
    fireEvent.click(opener);
    await screen.findByText('Tiêu đề');
    expect(opener).toBeDisabled();
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');
    try {
      fireEvent.click(screen.getByRole('button', { name: 'Xong' }));

      await waitFor(() => expect(screen.queryByText('Tiêu đề')).toBeNull());
      // Past the tick in which the closed dialog hands the focus back.
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(focus.mock.contexts).not.toContain(opener);
      expect(document.activeElement).toBe(document.body);
    } finally {
      focus.mockRestore();
    }
  });

  // The product and customer catalog dialogs: their search box takes the focus as the dialog renders, and Radix then
  // does not report the opening (onOpenAutoFocus).
  it('gives the focus back to the opener when a field of the dialog took the focus by itself', async () => {
    function CatalogDialog() {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button type="button" data-testid="opener" onClick={() => setOpen(true)}>
            Chọn hàng
          </button>
          <Dialog open={open} onOpenChange={setOpen}>
            <DialogContent>
              <DialogTitle>Danh mục hàng</DialogTitle>
              <DialogDescription>Mô tả</DialogDescription>
              {/* eslint-disable-next-line jsx-a11y/no-autofocus -- what the catalog dialogs do */}
              <input aria-label="Tìm hàng" autoFocus />
            </DialogContent>
          </Dialog>
        </>
      );
    }
    render(<CatalogDialog />);
    const opener = screen.getByTestId('opener');
    act(() => opener.focus());
    fireEvent.click(opener);
    const search = await screen.findByLabelText('Tìm hàng');
    expect(document.activeElement).toBe(search);

    fireEvent.keyDown(search, { key: 'Escape' });

    await waitFor(() => expect(screen.queryByText('Danh mục hàng')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(opener));
  });

  it('does not throw or focus anything when the opener left the page', async () => {
    render(<StateDialog opener="remove" />);
    const opener = screen.getByTestId('opener');
    act(() => opener.focus());
    fireEvent.click(opener);
    await screen.findByText('Tiêu đề');

    fireEvent.click(screen.getByRole('button', { name: 'Gỡ' }));
    expect(opener.isConnected).toBe(false);
    // focus() on an element that left the page does nothing either: the test watches the calls themselves.
    const focus = vi.spyOn(HTMLElement.prototype, 'focus');
    try {
      fireEvent.keyDown(document.activeElement!, { key: 'Escape' });

      await waitFor(() => expect(screen.queryByText('Tiêu đề')).toBeNull());
      // Past the tick in which the closed dialog hands the focus back.
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(focus.mock.contexts).not.toContain(opener);
      // Nothing has it, not even the other button of the page.
      expect(document.activeElement).toBe(document.body);
    } finally {
      focus.mockRestore();
    }
  });
});
