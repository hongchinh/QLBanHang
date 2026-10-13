import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import type { ProductSuggestion } from '@/features/products/types';
import { ProductTypeaheadCell } from './product-typeahead-cell';

const sheet: ProductSuggestion = {
  id: '33333333-3333-3333-3333-333333333333',
  code: 'TON01',
  name: 'Tôn lạnh',
  unitName: 'Tấm',
  pricingMode: 'PerSquareMeter',
  defaultPrice: 120_000,
  costPrice: 90_000,
  defaultTaxRate: 8,
  length: 2000,
  width: 1200,
  thickness: undefined,
  trackInventory: true,
  purchaseDiscountRate: 3,
  salesDiscountRate: 5,
  priceIncludesVat: true,
};

vi.mock('@/features/products/hooks', () => ({
  useProductSearch: () => ({ data: [sheet], isLoading: false, isError: false }),
  useProducts: () => ({ data: { items: [], totalPages: 1 }, isLoading: false }),
  useProductGroups: () => ({ data: [] }),
  useProduct: () => ({ data: undefined, isLoading: false }),
}));

function renderCell(openOnFocus?: boolean) {
  render(
    <ProductTypeaheadCell value="TON" onChange={vi.fn()} onSelect={vi.fn()} inputId="code" openOnFocus={openOnFocus} />,
  );
  act(() => document.getElementById('code')!.focus());
}

describe('ProductTypeaheadCell', () => {
  // Quotations rely on this default; the stock grid opts out.
  it('opens the suggestion list when a filled cell takes focus', () => {
    renderCell();
    expect(screen.getByRole('listbox')).toBeInTheDocument();
  });

  it('stays closed on focus with openOnFocus={false}', () => {
    renderCell(false);
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  // A modal dialog turns pointer events off on <body>; the list is portaled there, outside the
  // dialog content, so it must turn them back on or the mouse cannot pick a row.
  it('picks a row with the mouse inside a modal dialog and leaves the dialog open', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const onOpenChange = vi.fn();
    render(
      <Dialog open onOpenChange={onOpenChange}>
        <DialogContent aria-describedby={undefined}>
          <DialogTitle>Chọn mặt hàng</DialogTitle>
          <ProductTypeaheadCell value="TON" onChange={vi.fn()} onSelect={onSelect} inputId="code" />
        </DialogContent>
      </Dialog>,
    );
    act(() => document.getElementById('code')!.focus());

    await user.click(screen.getByRole('option', { name: /TON01/ }));

    expect(onSelect).toHaveBeenCalledWith(sheet);
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
    expect(screen.getByRole('dialog', { name: 'Chọn mặt hàng' })).toBeInTheDocument();
  });
});
