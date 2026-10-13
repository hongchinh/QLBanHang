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
