import type { ComponentProps } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { StockVoucherTabs } from './stock-voucher-tabs';

function renderTabs(props: Partial<ComponentProps<typeof StockVoucherTabs>> = {}) {
  const onValueChange = vi.fn();
  render(
    <StockVoucherTabs
      value="lines"
      onValueChange={onValueChange}
      lineCount={15}
      errorTabs={new Set()}
      lines={<input id="grid-cell" />}
      other={<input id="other-cell" />}
      payment={<input id="payment-cell" />}
      {...props}
    />,
  );
  return onValueChange;
}

describe('StockVoucherTabs', () => {
  it('keeps every panel mounted, shows the line count and switches on click', () => {
    const onValueChange = renderTabs();
    expect(screen.getByRole('tab', { name: /Hàng hóa/ })).toHaveAttribute('data-state', 'active');
    expect(screen.getByRole('tab', { name: /Hàng hóa/ })).toHaveTextContent('15');
    expect(document.getElementById('payment-cell')).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: /Lịch sử/ })).not.toBeInTheDocument();

    fireEvent.mouseDown(screen.getByRole('tab', { name: /Thanh toán/ }));
    expect(onValueChange).toHaveBeenCalledWith('payment');
  });

  it('marks tabs with errors and shows history only when given', () => {
    renderTabs({ errorTabs: new Set(['payment']), history: <p>log</p> });
    expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();
    expect(screen.getByTestId('tab-error-payment')).toHaveClass('bg-destructive', 'dark:bg-red-500');
    expect(screen.queryByTestId('tab-error-other')).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /Lịch sử/ })).toBeInTheDocument();
  });

  it('offers a slot on the tab row for the grid toolbar, only while Hàng hóa is active', () => {
    const toolbarRef = vi.fn();
    const { rerender } = render(
      <StockVoucherTabs value="lines" onValueChange={vi.fn()} lineCount={1} errorTabs={new Set()} toolbarRef={toolbarRef}
        lines={<i />} other={<i />} payment={<i />} />,
    );
    const slot = screen.getByTestId('stock-lines-toolbar-slot');
    expect(toolbarRef).toHaveBeenCalledWith(slot);
    expect(slot).not.toHaveClass('hidden');
    // The slot shares the row with the tab list: it is not inside the tablist.
    expect(screen.getByRole('tablist')).not.toContainElement(slot);
    rerender(
      <StockVoucherTabs value="payment" onValueChange={vi.fn()} lineCount={1} errorTabs={new Set()} toolbarRef={toolbarRef}
        lines={<i />} other={<i />} payment={<i />} />,
    );
    expect(screen.getByTestId('stock-lines-toolbar-slot')).toHaveClass('hidden');
  });
});
