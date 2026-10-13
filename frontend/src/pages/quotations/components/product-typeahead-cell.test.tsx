import { describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
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
});
