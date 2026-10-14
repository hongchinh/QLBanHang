import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
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
  // The list of the full catalog ("Xem danh mục đầy đủ").
  useProducts: () => ({ data: { items: [sheet], totalPages: 1, totalItems: 1 }, isLoading: false }),
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

  // The full catalog gives the focus back to the box as it closes. A pick there has just closed the list: open again,
  // it would take Tab and Enter (Enter picks its highlighted row, which may not be the product just picked).
  describe('a pick in the full catalog', () => {
    function Page({ nextFocusId }: { nextFocusId?: string }) {
      const [text, setText] = useState('TON');
      return (
        <>
          <ProductTypeaheadCell
            value={text}
            onChange={setText}
            onSelect={(s) => setText(s.code)}
            inputId="code"
            nextFocusId={nextFocusId}
          />
          <input id="name" aria-label="Tên hàng" />
        </>
      );
    }

    async function pickInCatalog() {
      const input = screen.getByRole('combobox');
      act(() => input.focus());
      expect(screen.getByRole('listbox')).toBeInTheDocument();
      fireEvent.mouseDown(screen.getByRole('button', { name: /Xem danh mục đầy đủ/ }));
      const catalog = await screen.findByRole('dialog');
      fireEvent.doubleClick(within(catalog).getByText('TON01'));
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      return input;
    }

    it('gives the focus back to the box with the list closed', async () => {
      render(<Page />);
      const input = await pickInCatalog();

      await waitFor(() => expect(document.activeElement).toBe(input));
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(input).toHaveValue('TON01');
      expect(screen.queryByRole('listbox')).toBeNull();
      expect(input).toHaveAttribute('aria-expanded', 'false');
    });

    it('leaves the next focus of the box to open the list as usual', async () => {
      render(<Page />);
      const input = await pickInCatalog();
      await waitFor(() => expect(document.activeElement).toBe(input));

      act(() => input.blur());
      act(() => input.focus());

      expect(screen.getByRole('listbox')).toBeInTheDocument();
    });

    // The quotation grid moves on to the name cell after a pick; coming back to the box later is a focus like any other.
    it('that moves on to the next cell leaves the box opening its list when the user comes back', async () => {
      render(<Page nextFocusId="name" />);
      const input = await pickInCatalog();
      await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText('Tên hàng')));
      // Past the tick in which the closed catalog could hand the focus back.
      await new Promise((resolve) => setTimeout(resolve, 20));

      act(() => input.focus());

      expect(screen.getByRole('listbox')).toBeInTheDocument();
    });
  });

  it('a pick from the list closes it, and the next focus of the box opens it again', () => {
    const onSelect = vi.fn();
    render(<ProductTypeaheadCell value="TON" onChange={vi.fn()} onSelect={onSelect} inputId="code" />);
    const input = screen.getByRole('combobox');
    act(() => input.focus());

    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onSelect).toHaveBeenCalledWith(sheet);
    expect(screen.queryByRole('listbox')).toBeNull();

    act(() => input.blur());
    act(() => input.focus());
    expect(screen.getByRole('listbox')).toBeInTheDocument();
  });

  it('keeps the highlighted option clear of the sticky header and of the list bottom', () => {
    // List box 100..420 with a 28px sticky header; the highlighted row is measured at 100..128 (under the header).
    const rect = (top: number, bottom: number) => ({ top, bottom, left: 0, right: 0, width: 0, height: bottom - top, x: 0, y: top, toJSON: () => ({}) }) as DOMRect;
    const rowRect = rect(100, 128);
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      if (this.tagName === 'THEAD') return rect(100, 128);
      if (this.getAttribute('role') === 'option') return rowRect;
      return rect(100, 420);
    });
    const scrolls: number[] = [];
    let top = 300;
    Object.defineProperty(HTMLElement.prototype, 'scrollTop', { configurable: true, get: () => top, set: (v: number) => { top = v; scrolls.push(v); } });
    try {
      renderCell();
      expect(scrolls).toEqual([272]); // 28px up, out from under the header
    } finally {
      delete (HTMLElement.prototype as { scrollTop?: number }).scrollTop;
      vi.restoreAllMocks();
    }
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
