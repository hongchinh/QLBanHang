import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useAuthStore } from '@/stores/auth-store';
import { StockPriceModeDialog } from './stock-price-mode-dialog';

const KEY = 'qldh:stock-print-price-mode:u1';

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function setup(open = true) {
  const onConfirm = vi.fn();
  const onOpenChange = vi.fn();
  const view = render(<StockPriceModeDialog open={open} onOpenChange={onOpenChange} onConfirm={onConfirm} />);
  return { onConfirm, onOpenChange, view };
}

describe('StockPriceModeDialog', () => {
  beforeEach(() => localStorage.clear());

  it('offers all modes with the permission and remembers the choice', async () => {
    setPermissions(['inventory.view_cost']);
    const user = userEvent.setup();
    const { onConfirm, onOpenChange, view } = setup();
    expect(screen.getByText('Chọn giá in')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Giá trên phiếu' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Không in tiền' })).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Giá vốn' }));
    await user.click(screen.getByRole('button', { name: 'In' }));
    expect(onConfirm).toHaveBeenCalledWith('Cost');
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(localStorage.getItem(KEY)).toBe('Cost');

    view.rerender(<StockPriceModeDialog open={false} onOpenChange={onOpenChange} onConfirm={onConfirm} />);
    view.rerender(<StockPriceModeDialog open onOpenChange={onOpenChange} onConfirm={onConfirm} />);
    expect(screen.getByRole('radio', { name: 'Giá vốn' })).toBeChecked();
  });

  it('hides Giá vốn without the permission', () => {
    setPermissions([]);
    localStorage.setItem(KEY, 'Cost');
    setup();
    expect(screen.queryByRole('radio', { name: 'Giá vốn' })).not.toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Giá trên phiếu' })).toBeChecked();
  });

  it('cancels without confirming', async () => {
    setPermissions([]);
    const user = userEvent.setup();
    const { onConfirm, onOpenChange } = setup();
    await user.click(screen.getByRole('button', { name: 'Hủy' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
