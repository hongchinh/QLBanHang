import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { StockTotalsBar } from './stock-totals-bar';

const totals = { goodsAmount: 1_000_000, lineDiscountTotal: 50_000, discountTotal: 70_000, vatTotal: 93_000, total: 1_053_000 };

describe('StockTotalsBar', () => {
  it('shows the totals and jumps to the editable amounts', () => {
    const onJump = vi.fn();
    render(<StockTotalsBar totals={totals} freight={30_000} orderDiscount={20_000} paidAmount={500_000} lineCount={15} onJump={onJump} />);

    expect(screen.getByTestId('stock-totals-lines')).toHaveTextContent('15 dòng');
    expect(screen.getByTestId('stock-totals-goods')).toHaveTextContent('1.000.000');
    // "CK" is the line discounts only; the order discount is its own "CK đơn" item (no double count).
    expect(screen.getByTestId('stock-totals-discount')).toHaveTextContent('50.000');
    expect(screen.getByTestId('stock-totals-vat')).toHaveTextContent('93.000');
    expect(screen.getByTestId('stock-totals-total')).toHaveTextContent('1.053.000');

    fireEvent.click(screen.getByRole('button', { name: /Phí VC/ }));
    fireEvent.click(screen.getByRole('button', { name: /CK đơn/ }));
    fireEvent.click(screen.getByRole('button', { name: /Đã thanh toán/ }));
    expect(onJump.mock.calls.map((c) => c[0])).toEqual(['freight', 'orderDiscount', 'paidAmount']);
    expect(screen.getByRole('button', { name: /Phí VC/ })).toHaveTextContent('30.000');
  });
});
