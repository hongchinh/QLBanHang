import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useAuthStore } from '@/stores/auth-store';
import { StockPriceModeDialog } from './stock-price-mode-dialog';

const KEY = 'qldh:stock-print-price-mode:u1';
const QR_KEY = 'qldh:voucher-print-qr:u1';
const QR = 'Kèm QR chuyển khoản';

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function setup(open = true, showQr?: boolean) {
  const onConfirm = vi.fn();
  const onOpenChange = vi.fn();
  const view = render(
    <StockPriceModeDialog open={open} onOpenChange={onOpenChange} onConfirm={onConfirm} showQr={showQr} />,
  );
  return { onConfirm, onOpenChange, view };
}

describe('StockPriceModeDialog', () => {
  beforeEach(() => localStorage.clear());

  it('offers all modes with the permission and remembers the choice', async () => {
    setPermissions(['inventory.view_cost']);
    const user = userEvent.setup();
    const { onConfirm, onOpenChange, view } = setup();
    expect(screen.getByText('Chọn giá in')).toBeInTheDocument();
    expect(screen.getByRole('radiogroup', { name: 'Chọn giá in' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Giá trên phiếu' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Không in tiền' })).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Giá vốn' }));
    await user.click(screen.getByRole('button', { name: 'In' }));
    expect(onConfirm).toHaveBeenCalledWith('Cost', false);
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

  it('without showQr there is no checkbox and the QR is off', async () => {
    setPermissions([]);
    const user = userEvent.setup();
    const { onConfirm } = setup();
    expect(screen.queryByRole('checkbox', { name: QR })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'In' }));
    expect(onConfirm).toHaveBeenCalledWith('Voucher', false);
    expect(localStorage.getItem(QR_KEY)).toBeNull();
  });

  describe('with showQr', () => {
    it('is ticked by default and confirms with the QR on Giá trên phiếu', async () => {
      setPermissions([]);
      const user = userEvent.setup();
      const { onConfirm } = setup(true, true);
      const box = screen.getByRole('checkbox', { name: QR });
      expect(box).toBeChecked();
      expect(box).toBeEnabled();
      await user.click(screen.getByRole('button', { name: 'In' }));
      expect(onConfirm).toHaveBeenCalledWith('Voucher', true);
      expect(localStorage.getItem(QR_KEY)).toBe('1');
    });

    it('unticked confirms without the QR and remembers it', async () => {
      setPermissions([]);
      const user = userEvent.setup();
      const { onConfirm } = setup(true, true);
      await user.click(screen.getByRole('checkbox', { name: QR }));
      await user.click(screen.getByRole('button', { name: 'In' }));
      expect(onConfirm).toHaveBeenCalledWith('Voucher', false);
      expect(localStorage.getItem(QR_KEY)).toBe('0');
    });

    it('reads the remembered choice', () => {
      setPermissions([]);
      localStorage.setItem(QR_KEY, '0');
      setup(true, true);
      expect(screen.getByRole('checkbox', { name: QR })).not.toBeChecked();
    });

    it('is disabled for Giá vốn and Không in tiền, keeping the stored choice', async () => {
      setPermissions(['inventory.view_cost']);
      localStorage.setItem(QR_KEY, '0');
      const user = userEvent.setup();
      const { onConfirm } = setup(true, true);
      await user.click(screen.getByRole('radio', { name: 'Giá vốn' }));
      const box = screen.getByRole('checkbox', { name: QR });
      expect(box).toBeDisabled();
      expect(box).not.toBeChecked();
      expect(box).toHaveAttribute('title', 'Chỉ dùng với Giá trên phiếu');

      await user.click(screen.getByRole('radio', { name: 'Giá trên phiếu' }));
      expect(screen.getByRole('checkbox', { name: QR })).toBeEnabled();
      expect(screen.getByRole('checkbox', { name: QR })).not.toBeChecked();

      await user.click(screen.getByRole('radio', { name: 'Giá vốn' }));
      await user.click(screen.getByRole('button', { name: 'In' }));
      expect(onConfirm).toHaveBeenCalledWith('Cost', false);
      expect(localStorage.getItem(QR_KEY)).toBe('0');
    });

    it('Không in tiền disables it and confirms without the QR', async () => {
      setPermissions([]);
      const user = userEvent.setup();
      const { onConfirm } = setup(true, true);
      await user.click(screen.getByRole('radio', { name: 'Không in tiền' }));
      expect(screen.getByRole('checkbox', { name: QR })).toBeDisabled();
      await user.click(screen.getByRole('button', { name: 'In' }));
      expect(onConfirm).toHaveBeenCalledWith('None', false);
    });
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
