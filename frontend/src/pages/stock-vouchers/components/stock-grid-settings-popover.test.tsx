import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { StockGridSettingsPopover } from './stock-grid-settings-popover';
import { DEFAULT_STOCK_GRID_PREFS } from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';

describe('StockGridSettingsPopover', () => {
  it('toggles columns and enter stops, and resets', () => {
    const onToggleColumn = vi.fn();
    const onToggleEnterStop = vi.fn();
    const onReset = vi.fn();
    render(
      <StockGridSettingsPopover
        prefs={DEFAULT_STOCK_GRID_PREFS}
        onToggleColumn={onToggleColumn}
        onToggleEnterStop={onToggleEnterStop}
        onReset={onReset}
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));

    expect(screen.getByRole('checkbox', { name: 'Ghi chú' })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: '%VAT' })).toBeChecked();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Ghi chú' }));
    expect(onToggleColumn).toHaveBeenCalledWith('note');

    expect(screen.getByRole('checkbox', { name: 'Enter dừng ở SL — Cái' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Enter dừng ở Rộng — m²' })).toBeChecked();
    expect(screen.queryByRole('checkbox', { name: 'Enter dừng ở Rộng — Cái' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Enter dừng ở %VAT — Cái' }));
    expect(onToggleEnterStop).toHaveBeenCalledWith('PerUnit', 'vat-rate');

    fireEvent.click(screen.getByRole('button', { name: 'Khôi phục mặc định' }));
    expect(onReset).toHaveBeenCalled();
  });

  it('says under the Hiện cột checkboxes that the dimension columns follow the goods of the voucher', () => {
    render(
      <StockGridSettingsPopover
        prefs={DEFAULT_STOCK_GRID_PREFS}
        onToggleColumn={vi.fn()}
        onToggleEnterStop={vi.fn()}
        onReset={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));

    const note = screen.getByText('Số tấm, Dài, Rộng, Dày tự hiện theo hàng tính md, m², m³ có trong phiếu.');
    expect(note.tagName).toBe('P');
    expect(note).toHaveClass('text-xs', 'text-muted-foreground');
    const section = screen.getByRole('heading', { name: 'Hiện cột' }).closest('section')!;
    expect(section).toContainElement(note);
    const checkboxes = within(section).getAllByRole('checkbox');
    const last = checkboxes[checkboxes.length - 1];
    expect(last.compareDocumentPosition(note) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});
