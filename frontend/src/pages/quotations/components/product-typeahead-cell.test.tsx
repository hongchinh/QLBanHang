import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import type * as DialogModule from '@/components/ui/dialog';
import type { ProductQuickAddDialogProps } from '@/features/products/components/product-quick-add-dialog';
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

const created: ProductSuggestion = { ...sheet, id: '44444444-4444-4444-4444-444444444444', code: 'TL-50', name: 'Tôn lạnh 0.50' };

interface SearchState {
  data: ProductSuggestion[] | undefined;
  isLoading: boolean;
  isError: boolean;
  isFetching: boolean;
  /** Undefined: the results belong to the text shown. */
  settledQuery: string | undefined;
}
const settledSearch = (): SearchState => ({ data: [sheet], isLoading: false, isError: false, isFetching: false, settledQuery: undefined });
let searchState = settledSearch();

let permissions: string[] = [];
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => permissions.includes(p), isInRole: () => false }),
}));

// Stands in for the real form: a save hands back `created`, then closes, as the real dialog does.
vi.mock('@/features/products/components/product-quick-add-dialog', async () => {
  const ui = await vi.importActual<typeof DialogModule>('@/components/ui/dialog');
  return {
    ProductQuickAddDialog: ({ open, onOpenChange, seed, onCreated, onCloseAutoFocus }: ProductQuickAddDialogProps) => (
      <ui.Dialog open={open} onOpenChange={onOpenChange}>
        <ui.DialogContent aria-describedby={undefined} onCloseAutoFocus={onCloseAutoFocus}>
          <ui.DialogTitle>Thêm nhanh hàng hóa</ui.DialogTitle>
          <p data-testid="seed">{seed}</p>
          <input aria-label="fake field" />
          <button
            type="button"
            onClick={() => {
              onCreated(created);
              onOpenChange(false);
            }}
          >
            fake lưu
          </button>
          <button type="submit">fake submit</button>
        </ui.DialogContent>
      </ui.Dialog>
    ),
  };
});

vi.mock('@/features/products/hooks', () => ({
  useProductSearch: () => searchState,
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
  beforeEach(() => {
    searchState = settledSearch();
    permissions = [];
  });

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

    // A Tab during the catalog's close animation: the box never gets the focus back from the catalog, so nothing has
    // used up the pick. A later focus of the box is the user's own and opens the list.
    it('opens the list on a later focus when the focus went somewhere else as the catalog closed', async () => {
      render(<Page />);
      const now = vi.spyOn(performance, 'now');
      try {
        const input = screen.getByRole('combobox');
        act(() => input.focus());
        fireEvent.mouseDown(screen.getByRole('button', { name: /Xem danh mục đầy đủ/ }));
        const catalog = await screen.findByRole('dialog');
        now.mockReturnValue(10_000);
        fireEvent.doubleClick(within(catalog).getByText('TON01'));
        act(() => screen.getByLabelText('Tên hàng').focus());
        await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
        await new Promise((resolve) => setTimeout(resolve, 20));
        act(() => screen.getByLabelText('Tên hàng').focus());

        now.mockReturnValue(12_000);
        act(() => input.focus());

        expect(screen.getByRole('listbox')).toBeInTheDocument();
      } finally {
        now.mockRestore();
      }
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

  describe('quick add', () => {
    const ADD_ROW = 'Thêm mới hàng hóa "TL-50"';

    function setup({
      allowQuickAdd = true,
      openOnFocus,
      nextFocusId,
    }: { allowQuickAdd?: boolean; openOnFocus?: boolean; nextFocusId?: string } = {}) {
      const onSelect = vi.fn();
      const onChange = vi.fn();
      // A fresh element each time: React skips a re-render of the very same element.
      const element = () => (
        <>
          <ProductTypeaheadCell
            value="TL-50"
            onChange={onChange}
            onSelect={onSelect}
            inputId="code"
            nextFocusId={nextFocusId}
            openOnFocus={openOnFocus}
            allowQuickAdd={allowQuickAdd}
          />
          <input id="next" aria-label="next" />
        </>
      );
      const view = render(element());
      const input = screen.getByRole('combobox');
      act(() => input.focus());
      return { onSelect, onChange, input, rerender: () => view.rerender(element()) };
    }

    const quickAddDialog = () => screen.queryByRole('dialog', { name: 'Thêm nhanh hàng hóa' });

    it('shows no add row and ignores F4 without allowQuickAdd', () => {
      permissions = ['products.create'];
      searchState.data = [];
      const { input } = setup({ allowQuickAdd: false });

      expect(screen.getByRole('listbox')).toBeInTheDocument();
      expect(screen.queryByText(/Thêm mới hàng hóa/)).toBeNull();
      fireEvent.keyDown(input, { key: 'F4' });
      expect(quickAddDialog()).toBeNull();
    });

    it('shows no add row and ignores F4 without products.create', () => {
      searchState.data = [];
      const { input } = setup();

      expect(screen.queryByText(/Thêm mới hàng hóa/)).toBeNull();
      fireEvent.keyDown(input, { key: 'F4' });
      expect(quickAddDialog()).toBeNull();
    });

    describe('with products.create', () => {
      beforeEach(() => {
        permissions = ['products.create'];
      });

      it('highlights the add row when nothing matches, and Enter opens the dialog with the typed text', async () => {
        searchState.data = [];
        const { input, onSelect } = setup();

        const addRow = screen.getByRole('option', { name: new RegExp(ADD_ROW) });
        expect(addRow).toHaveAttribute('aria-selected', 'true');
        expect(input).toHaveAttribute('aria-activedescendant', addRow.id);
        expect(screen.getByText('Không tìm thấy sản phẩm phù hợp')).toBeInTheDocument();

        fireEvent.keyDown(input, { key: 'Enter' });

        expect(await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' })).toBeInTheDocument();
        expect(screen.getByTestId('seed')).toHaveTextContent('TL-50');
        expect(screen.queryByRole('listbox')).toBeNull();
        expect(onSelect).not.toHaveBeenCalled();
      });

      it('puts the add row after the results; ArrowDown reaches it and Enter opens the dialog', async () => {
        const { input, onSelect } = setup();
        const options = screen.getAllByRole('option');
        expect(options).toHaveLength(2);
        expect(options[1]).toHaveTextContent(ADD_ROW);
        expect(options[0]).toHaveAttribute('aria-selected', 'true');

        fireEvent.keyDown(input, { key: 'ArrowDown' });
        expect(screen.getByRole('option', { name: new RegExp(ADD_ROW) })).toHaveAttribute('aria-selected', 'true');
        fireEvent.keyDown(input, { key: 'Enter' });

        expect(await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' })).toBeInTheDocument();
        expect(onSelect).not.toHaveBeenCalled();
      });

      it('Enter on the first result still picks it', () => {
        const { input, onSelect } = setup();

        fireEvent.keyDown(input, { key: 'Enter' });

        expect(onSelect).toHaveBeenCalledWith(sheet);
        expect(quickAddDialog()).toBeNull();
      });

      it('an Enter that commits an IME composition acts on nothing: no dialog, no pick', () => {
        searchState.data = [];
        const first = setup();
        fireEvent.keyDown(first.input, { key: 'Enter', isComposing: true, keyCode: 229 });
        expect(quickAddDialog()).toBeNull();
        expect(first.onSelect).not.toHaveBeenCalled();
      });

      it('an IME Enter with a result highlighted picks nothing', () => {
        const { input, onSelect } = setup();
        fireEvent.keyDown(input, { key: 'Enter', isComposing: true, keyCode: 229 });
        expect(onSelect).not.toHaveBeenCalled();
        expect(quickAddDialog()).toBeNull();
      });

      it('opens the add row with the mouse', async () => {
        searchState.data = [];
        setup();

        fireEvent.mouseDown(screen.getByRole('option', { name: new RegExp(ADD_ROW) }));

        expect(await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' })).toBeInTheDocument();
      });

      it('Tab leaves the cell when only the add row is there; with results it still cycles them', () => {
        searchState.data = [];
        const { input, rerender } = setup();
        expect(screen.getByRole('option', { name: new RegExp(ADD_ROW) })).toBeInTheDocument();
        expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(true);

        searchState = { ...settledSearch(), data: [sheet, created] };
        rerender();
        expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(false);
        expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(false);
        // Two results: the second Tab is back on the first, never on the add row.
        expect(screen.getByRole('option', { name: /Tôn lạnh 0\.50/ })).toHaveAttribute('aria-selected', 'false');
        expect(screen.getAllByRole('option')[0]).toHaveAttribute('aria-selected', 'true');
      });

      it('Tab from the add row goes to the first result', () => {
        searchState = { ...settledSearch(), data: [sheet, created] };
        const { input } = setup();
        fireEvent.keyDown(input, { key: 'ArrowDown' });
        fireEvent.keyDown(input, { key: 'ArrowDown' });
        expect(screen.getByRole('option', { name: new RegExp(ADD_ROW) })).toHaveAttribute('aria-selected', 'true');

        fireEvent.keyDown(input, { key: 'Tab' });

        expect(screen.getAllByRole('option')[0]).toHaveAttribute('aria-selected', 'true');
      });

      it('F4 opens the dialog with the list closed', async () => {
        const { input } = setup({ openOnFocus: false });
        expect(screen.queryByRole('listbox')).toBeNull();

        fireEvent.keyDown(input, { key: 'F4' });

        expect(await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' })).toBeInTheDocument();
        expect(screen.getByTestId('seed')).toHaveTextContent('TL-50');
      });

      describe('an Enter before the results of the typed text', () => {
        beforeEach(() => {
          searchState = { ...settledSearch(), data: [], settledQuery: 'TL' };
        });

        it('hides the add row and waits, then picks the result that arrives', () => {
          const { input, onSelect, rerender } = setup();
          expect(screen.queryByText(/Thêm mới hàng hóa/)).toBeNull();

          const notCancelled = fireEvent.keyDown(input, { key: 'Enter' });
          expect(notCancelled).toBe(false);
          expect(onSelect).not.toHaveBeenCalled();
          expect(quickAddDialog()).toBeNull();

          searchState = { ...settledSearch(), settledQuery: 'TL-50' };
          rerender();

          expect(onSelect).toHaveBeenCalledTimes(1);
          expect(onSelect).toHaveBeenCalledWith(sheet);
        });

        it('waits on a refetch too', () => {
          searchState = { ...settledSearch(), data: [], isFetching: true };
          const { input, onSelect, rerender } = setup();
          expect(screen.queryByText(/Thêm mới hàng hóa/)).toBeNull();

          expect(fireEvent.keyDown(input, { key: 'Enter' })).toBe(false);

          searchState = settledSearch();
          rerender();
          expect(onSelect).toHaveBeenCalledWith(sheet);
        });

        it('ends when the box loses the focus: nothing is picked and the focus is not moved', () => {
          vi.useFakeTimers();
          try {
            const { input, onSelect, rerender } = setup({ nextFocusId: 'next' });
            expect(fireEvent.keyDown(input, { key: 'Enter' })).toBe(false);
            act(() => screen.getByLabelText('next').focus());
            fireEvent.blur(input);
            act(() => screen.getByLabelText('next').focus());

            searchState = { ...settledSearch(), settledQuery: 'TL-50' };
            rerender();
            act(() => {
              vi.runAllTimers();
            });

            expect(onSelect).not.toHaveBeenCalled();
            expect(screen.getByLabelText('next')).toHaveFocus();
          } finally {
            vi.useRealTimers();
          }
        });

        it('does not pick when the search failed, whatever stale data is there', () => {
          const { input, onSelect, rerender } = setup();
          fireEvent.keyDown(input, { key: 'Enter' });

          searchState = { ...settledSearch(), isError: true, settledQuery: 'TL-50' };
          rerender();

          expect(onSelect).not.toHaveBeenCalled();
        });

        it('with no result arriving, keeps the list open on the add row and opens no dialog', () => {
          const { input, onSelect, rerender } = setup();
          fireEvent.keyDown(input, { key: 'Enter' });

          searchState = { ...settledSearch(), data: [], settledQuery: 'TL-50' };
          rerender();

          expect(onSelect).not.toHaveBeenCalled();
          expect(screen.getByRole('option', { name: new RegExp(ADD_ROW) })).toHaveAttribute('aria-selected', 'true');
          expect(quickAddDialog()).toBeNull();
        });
      });

      describe('the dialog closing', () => {
        it('after a save, picks the new product once, after the dialog has closed, and moves to nextFocusId', async () => {
          const { input, onSelect } = setup({ nextFocusId: 'next' });
          let dialogAtPick: Element | null = null;
          onSelect.mockImplementation(() => {
            dialogAtPick = document.querySelector('[role="dialog"]');
          });
          fireEvent.keyDown(input, { key: 'F4' });
          await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' });

          fireEvent.click(screen.getByRole('button', { name: 'fake lưu' }));

          await waitFor(() => expect(onSelect).toHaveBeenCalledTimes(1));
          expect(onSelect).toHaveBeenCalledWith(created);
          expect(dialogAtPick).toBeNull();
          await waitFor(() => expect(document.getElementById('next')).toHaveFocus());
          await new Promise((resolve) => setTimeout(resolve, 20));
          expect(onSelect).toHaveBeenCalledTimes(1);
        });

        it('after a save without nextFocusId, leaves the focus in the cell with the list closed', async () => {
          const { input, onSelect } = setup();
          fireEvent.keyDown(input, { key: 'F4' });
          await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' });

          fireEvent.click(screen.getByRole('button', { name: 'fake lưu' }));

          await waitFor(() => expect(onSelect).toHaveBeenCalledWith(created));
          await waitFor(() => expect(input).toHaveFocus());
          await new Promise((resolve) => setTimeout(resolve, 20));
          expect(screen.queryByRole('listbox')).toBeNull();
          expect(onSelect).toHaveBeenCalledTimes(1);
        });

        it('on Esc, picks nothing and gives the focus back to the cell with its text and the list closed', async () => {
          const user = userEvent.setup();
          const { input, onSelect, onChange } = setup();
          fireEvent.keyDown(input, { key: 'F4' });
          await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' });

          await user.keyboard('{Escape}');

          await waitFor(() => expect(quickAddDialog()).toBeNull());
          await waitFor(() => expect(input).toHaveFocus());
          await new Promise((resolve) => setTimeout(resolve, 20));
          expect(onSelect).not.toHaveBeenCalled();
          expect(onChange).not.toHaveBeenCalled();
          expect(input).toHaveValue('TL-50');
          expect(screen.queryByRole('listbox')).toBeNull();
        });
      });

      it('keeps keys and submit of the dialog away from the voucher form', async () => {
        const submitSpy = vi.fn((e: React.FormEvent) => e.preventDefault());
        const keySpy = vi.fn();
        render(
          // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- the test stands in for the voucher form.
          <form onSubmit={submitSpy} onKeyDown={keySpy}>
            <ProductTypeaheadCell value="TL-50" onChange={vi.fn()} onSelect={vi.fn()} inputId="code" allowQuickAdd />
          </form>,
        );
        const input = screen.getByRole('combobox');
        act(() => input.focus());
        fireEvent.keyDown(input, { key: 'F4' });
        const dialog = await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' });
        keySpy.mockClear();

        fireEvent.keyDown(within(dialog).getByLabelText('fake field'), { key: 'Enter' });
        fireEvent.keyDown(within(dialog).getByLabelText('fake field'), { key: 's', ctrlKey: true });
        fireEvent.submit(within(dialog).getByRole('button', { name: 'fake submit' }));
        await userEvent.setup().click(within(dialog).getByRole('button', { name: 'fake submit' }));

        expect(keySpy).not.toHaveBeenCalled();
        expect(submitSpy).not.toHaveBeenCalled();
      });
    });

    it('without allowQuickAdd, an IME Enter with a result highlighted picks nothing', () => {
      const { input, onSelect } = setup({ allowQuickAdd: false });
      fireEvent.keyDown(input, { key: 'Enter', isComposing: true, keyCode: 229 });
      expect(onSelect).not.toHaveBeenCalled();
    });

    it('without allowQuickAdd, an Enter before the results passes through to the grid and picks nothing', () => {
      permissions = ['products.create'];
      searchState = { ...settledSearch(), data: [], settledQuery: 'TL' };
      const { input, onSelect, rerender } = setup({ allowQuickAdd: false });

      const notCancelled = fireEvent.keyDown(input, { key: 'Enter' });
      expect(notCancelled).toBe(true);

      searchState = { ...settledSearch(), settledQuery: 'TL-50' };
      rerender();
      expect(onSelect).not.toHaveBeenCalled();
    });
  });
});
