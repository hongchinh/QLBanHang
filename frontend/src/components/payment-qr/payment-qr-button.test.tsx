import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { PaymentQrButton } from './payment-qr-button';

const dialogProps = vi.hoisted(() => ({ current: null as Record<string, unknown> | null }));

vi.mock('./payment-qr-dialog', () => ({
  PaymentQrDialog: (props: { open: boolean; onOpenChange: (open: boolean) => void }) => {
    dialogProps.current = props as unknown as Record<string, unknown>;
    return props.open ? <div data-testid="dialog-open" /> : null;
  },
}));

describe('PaymentQrButton', () => {
  it('opens the dialog of the voucher', async () => {
    render(<PaymentQrButton kind="stock" id="v1" code="PX00001" />);

    expect(screen.queryByTestId('dialog-open')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'QR chuyển khoản' }));

    expect(screen.getByTestId('dialog-open')).toBeInTheDocument();
    expect(dialogProps.current).toMatchObject({ open: true, kind: 'stock', id: 'v1', voucherCode: 'PX00001' });
  });

  it('closing the dialog sets open to false', async () => {
    render(<PaymentQrButton kind="cash" id="v2" code="01-TT00001" />);
    await userEvent.click(screen.getByRole('button', { name: 'QR chuyển khoản' }));

    const onOpenChange = dialogProps.current?.onOpenChange as (open: boolean) => void;
    expect(screen.getByTestId('dialog-open')).toBeInTheDocument();
    act(() => onOpenChange(false));

    expect(screen.queryByTestId('dialog-open')).not.toBeInTheDocument();
    expect(dialogProps.current).toMatchObject({ open: false, kind: 'cash' });
  });

  it('disabled shows the reason as the title and does not open', async () => {
    render(<PaymentQrButton kind="cash" id="v1" code="01-TT00001" disabled disabledReason="Phiếu đã hủy" />);

    const button = screen.getByRole('button', { name: 'QR chuyển khoản' });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', 'Phiếu đã hủy');
    await userEvent.click(button);
    expect(screen.queryByTestId('dialog-open')).not.toBeInTheDocument();
  });

  it('has no title while enabled', () => {
    render(<PaymentQrButton kind="cash" id="v1" code="01-TT00001" disabledReason="Phiếu đã hủy" />);

    expect(screen.getByRole('button', { name: 'QR chuyển khoản' })).not.toHaveAttribute('title');
  });
});
