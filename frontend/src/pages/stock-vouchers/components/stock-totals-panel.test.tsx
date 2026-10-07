import { describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { StockTotalsPanel } from './stock-totals-panel';
import type { StockDirection } from '@/features/stock-reasons/types';

function renderPanel(type: StockDirection, withVatToAll = true) {
  const props = {
    type,
    totals: { goodsAmount: 1_000_000, lineDiscountTotal: 50_000, discountTotal: 70_000, vatTotal: 93_000, total: 1_053_000 },
    freight: 30_000,
    orderDiscount: 20_000,
    paidAmount: 1_053_000,
    onFreightChange: vi.fn(),
    onOrderDiscountChange: vi.fn(),
    onPaidAmountChange: vi.fn(),
    onApplyVatToAll: withVatToAll ? vi.fn() : undefined,
    readOnly: false,
  };
  render(<StockTotalsPanel {...props} />);
  return props;
}

describe('StockTotalsPanel', () => {
  it('renders totals and calls handlers for freight, order discount and paid amount', () => {
    const props = renderPanel('Out');

    expect(screen.getByTestId('stock-totals-goods')).toHaveTextContent('1.000.000');
    expect(screen.getByTestId('stock-totals-discount')).toHaveTextContent('70.000');
    expect(screen.getByTestId('stock-totals-vat')).toHaveTextContent('93.000');
    expect(screen.getByTestId('stock-totals-total')).toHaveTextContent('1.053.000');
    expect(screen.getByLabelText('Phí vận chuyển')).toHaveValue('30.000');

    fireEvent.change(screen.getByLabelText('Phí vận chuyển'), { target: { value: '45000' } });
    expect(props.onFreightChange).toHaveBeenLastCalledWith(45_000);

    fireEvent.change(screen.getByLabelText('CK cả đơn'), { target: { value: '10.000' } });
    expect(props.onOrderDiscountChange).toHaveBeenLastCalledWith(10_000);

    fireEvent.change(screen.getByLabelText('Số tiền thanh toán'), { target: { value: '500000' } });
    expect(props.onPaidAmountChange).toHaveBeenLastCalledWith(500_000);
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
});
