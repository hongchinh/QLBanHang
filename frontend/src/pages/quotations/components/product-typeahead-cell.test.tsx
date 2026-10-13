import { afterEach, describe, expect, it, vi } from 'vitest';
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

  describe('placement against the window', () => {
    function placeCell(inputRect: { top: number; bottom: number }, listHeight: number, innerHeight = 768) {
      vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(innerHeight);
      vi.spyOn(HTMLInputElement.prototype, 'getBoundingClientRect').mockReturnValue({
        ...inputRect, left: 40, right: 240, width: 200, height: inputRect.bottom - inputRect.top, x: 40, y: inputRect.top, toJSON: () => ({}),
      } as DOMRect);
      vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(listHeight);
      renderCell();
      return screen.getByRole('listbox').parentElement as HTMLElement;
    }
    afterEach(() => vi.restoreAllMocks());

    it('opens below the input when there is room (unchanged)', () => {
      const box = placeCell({ top: 100, bottom: 130 }, 300);
      expect(box.style.top).toBe('134px');
      expect(box.style.bottom).toBe('');
      expect(box.style.maxHeight).toBe('');
    });

    it('opens above the input when the list does not fit below but fits above', () => {
      const box = placeCell({ top: 600, bottom: 630 }, 300);
      expect(box.style.top).toBe('');
      expect(box.style.bottom).toBe('172px'); // 768 - 600 + 4
      expect(box.style.maxHeight).toBe('');
    });

    it('a long result list counts as its capped 320px, not its full content height', () => {
      // 20 results: 679px of content, shown in a 320px scrolling box. 768-380-8 = 380 below is enough.
      const box = placeCell({ top: 350, bottom: 380 }, 679);
      expect(box.style.top).toBe('384px');
      expect(box.style.bottom).toBe('');
      expect(box.style.maxHeight).toBe('');
    });

    it('caps the height to the larger side when neither side fits', () => {
      const box = placeCell({ top: 150, bottom: 180 }, 320, 400);
      // below: 400-180-8 = 212, above: 150-8 = 142 -> stays below, capped
      expect(box.style.top).toBe('184px');
      expect(box.style.maxHeight).toBe('208px');
    });

    it('caps the height above when the space above is larger but still too small', () => {
      const box = placeCell({ top: 250, bottom: 280 }, 320, 400);
      // below: 112, above: 242 -> above, capped to 242 - 4
      expect(box.style.bottom).toBe('154px');
      expect(box.style.maxHeight).toBe('238px');
    });
  });
});
