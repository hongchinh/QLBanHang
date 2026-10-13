import { describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { StockVoucherPaymentTab } from './stock-voucher-payment-tab';
import type { StockDirection } from '@/features/stock-reasons/types';

function renderPanel(type: StockDirection, withVatToAll = true) {
  const props = {
    type,
    freight: 30_000,
    orderDiscount: 20_000,
    paidAmount: 1_053_000,
    onFreightChange: vi.fn(),
    onOrderDiscountChange: vi.fn(),
    onPaidAmountChange: vi.fn(),
    onApplyVatToAll: withVatToAll ? vi.fn() : undefined,
    readOnly: false,
  };
  render(
    <StockVoucherPaymentTab {...props}>
      <label htmlFor="x">Quỹ</label>
      <select id="x" />
    </StockVoucherPaymentTab>,
  );
  return props;
}

describe('StockVoucherPaymentTab', () => {
  it('calls handlers for freight, order discount and paid amount', () => {
    const props = renderPanel('Out');

    expect(screen.getByLabelText('Phí vận chuyển')).toHaveValue('30.000');

    fireEvent.change(screen.getByLabelText('Phí vận chuyển'), { target: { value: '45000' } });
    expect(props.onFreightChange).toHaveBeenLastCalledWith(45_000);

    fireEvent.change(screen.getByLabelText('CK cả đơn'), { target: { value: '10.000' } });
    expect(props.onOrderDiscountChange).toHaveBeenLastCalledWith(10_000);

    fireEvent.change(screen.getByLabelText('Số tiền thanh toán'), { target: { value: '500000' } });
    expect(props.onPaidAmountChange).toHaveBeenLastCalledWith(500_000);
  });

  it('renders the freight, order discount and paid amount messages under their rows', () => {
    render(
      <StockVoucherPaymentTab
        type="In"
        freight={-5}
        orderDiscount={-7}
        paidAmount={0}
        freightError="Phí vận chuyển không được âm"
        orderDiscountError="Chiết khấu không được âm"
        paidAmountError="Quỹ không đủ"
        onFreightChange={vi.fn()}
        onOrderDiscountChange={vi.fn()}
        onPaidAmountChange={vi.fn()}
        readOnly={false}
      />,
    );
    expect(screen.getByText('Phí vận chuyển không được âm')).toBeInTheDocument();
    expect(screen.getByText('Chiết khấu không được âm')).toBeInTheDocument();
    expect(screen.getByText('Quỹ không đủ')).toBeInTheDocument();
    expect(screen.getByLabelText('Phí vận chuyển')).toHaveAttribute('aria-invalid', 'true');
  });

  it('VAT-to-all input exists only for stock-in and calls onApplyVatToAll', () => {
    const props = renderPanel('In');
    fireEvent.change(screen.getByLabelText('% VAT cho mọi dòng'), { target: { value: '8' } });
    fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }));
    expect(props.onApplyVatToAll).toHaveBeenCalledWith(8);

    cleanup();
    renderPanel('Out');
    expect(screen.queryByLabelText('% VAT cho mọi dòng')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Áp dụng' })).not.toBeInTheDocument();
  });

  it('renders the fund / due date slot and no totals', () => {
    renderPanel('Out');
    expect(screen.getByLabelText('Quỹ')).toBeInTheDocument();
    expect(screen.queryByTestId('stock-totals-total')).not.toBeInTheDocument();
    expect(screen.queryByText('Tổng cộng')).not.toBeInTheDocument();
  });
});
