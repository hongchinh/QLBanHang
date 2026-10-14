import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from './dialog';

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

  it('does not focus an opener that is disabled when the dialog closes', async () => {
    render(<StateDialog opener="disable" />);
    const opener = screen.getByTestId('opener');
    act(() => opener.focus());
    fireEvent.click(opener);
    await screen.findByText('Tiêu đề');

    fireEvent.click(screen.getByRole('button', { name: 'Xong' }));

    await waitFor(() => expect(screen.queryByText('Tiêu đề')).toBeNull());
    expect(document.activeElement).not.toBe(opener);
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
    fireEvent.keyDown(document.activeElement!, { key: 'Escape' });

    await waitFor(() => expect(screen.queryByText('Tiêu đề')).toBeNull());
    expect(opener.isConnected).toBe(false);
  });
});
