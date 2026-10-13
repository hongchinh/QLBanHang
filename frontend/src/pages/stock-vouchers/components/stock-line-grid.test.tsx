import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { StockLineGrid } from './stock-line-grid';
import { stockGridPrefsKey } from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';
import { toFormDefaults, createEmptyStockLine } from '@/features/stock-vouchers/payload';
import type {
  StockLineFormValues,
  StockVoucherFormParsed,
  StockVoucherFormValues,
} from '@/features/stock-vouchers/schema';
import type { ProductListItem, ProductSuggestion } from '@/features/products/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import type { Warehouse } from '@/features/warehouses/types';
import type { StockLineComputed } from '@/pages/stock-vouchers/utils/compute-stock-line';

const useProductSearchMock = vi.fn();
const useProductsMock = vi.fn();

vi.mock('@/features/products/hooks', () => ({
  useProductSearch: (...args: unknown[]) => useProductSearchMock(...args),
  useProducts: (...args: unknown[]) => useProductsMock(...args),
  useProductGroups: () => ({ data: [] }),
  useProduct: () => ({ data: undefined, isLoading: false }),
}));

const WH1 = '11111111-1111-1111-1111-111111111111';
const WH2 = '22222222-2222-2222-2222-222222222222';
const P1 = '33333333-3333-3333-3333-333333333333';
const P2 = '44444444-4444-4444-4444-444444444444';

const warehouses: Warehouse[] = [
  { id: WH1, code: 'KHO01', name: 'Kho chính', branchId: 'b', branchCode: 'CN01', branchName: 'CN 1', isActive: true },
  { id: WH2, code: 'KHO02', name: 'Kho phụ', branchId: 'b', branchCode: 'CN01', branchName: 'CN 1', isActive: true },
];

const sheet: ProductSuggestion = {
  id: P1,
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

const listItem: ProductListItem = {
  id: P2,
  code: 'GO01',
  name: 'Gỗ thông',
  unitName: 'Thanh',
  pricingMode: 'PerCubicMeter',
  defaultPrice: 9_000_000,
  costPrice: 7_000_000,
  status: 'Active',
  trackInventory: true,
  defaultTaxRate: 10,
  length: 3000,
  width: 100,
  thickness: 50,
  purchaseDiscountRate: 2,
  salesDiscountRate: 4,
  priceIncludesVat: false,
};

function line(overrides: Partial<StockLineFormValues> = {}): StockLineFormValues {
  return {
    ...createEmptyStockLine(0, WH1),
    productId: P1,
    productCode: 'SP01',
    productName: 'Sản phẩm',
    unitName: 'Cái',
    quantity: 1,
    unitPrice: 10_000,
    ...overrides,
  };
}

function zeroComputed(overrides: Partial<StockLineComputed> = {}): StockLineComputed {
  return {
    quantity: 0,
    amount: 0,
    discountAmount: 0,
    orderDiscountAllocated: 0,
    vatAmount: 0,
    netAmount: 0,
    freightAllocated: 0,
    ...overrides,
  };
}

interface RenderOptions {
  type?: StockDirection;
  lines?: StockLineFormValues[];
  computed?: StockLineComputed[];
  stockAt?: Record<string, number>;
  readOnly?: boolean;
}

function renderGrid(opts: RenderOptions = {}) {
  const formRef: { current: UseFormReturn<StockVoucherFormValues, unknown, StockVoucherFormParsed> | null } = {
    current: null,
  };
  function Harness() {
    const defaults = toFormDefaults();
    const form = useForm<StockVoucherFormValues, unknown, StockVoucherFormParsed>({
      defaultValues: { ...defaults, warehouseId: WH1, lines: opts.lines ?? defaults.lines },
    });
    formRef.current = form;
    return (
      <StockLineGrid
        form={form}
        type={opts.type ?? 'In'}
        warehouses={warehouses}
        computed={opts.computed ?? []}
        stockAt={opts.stockAt ?? {}}
        readOnly={opts.readOnly ?? false}
      />
    );
  }
  render(<Harness />);
  return { values: () => formRef.current!.getValues(), form: () => formRef.current! };
}

function storePrefs(prefs: { hiddenColumns?: string[]; enterStops?: Record<string, string[]> }, type: 'In' | 'Out' = 'In') {
  localStorage.setItem(
    stockGridPrefsKey('', type),
    JSON.stringify({ version: 1, hiddenColumns: prefs.hiddenColumns ?? [], enterStops: prefs.enterStops ?? {} }),
  );
}
const showAllColumns = () => storePrefs({ hiddenColumns: [] });

function headers() {
  return screen.getAllByRole('columnheader').map((th) => th.textContent);
}

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

async function pickFromTypeahead(code: string) {
  fireEvent.change(byId('stock-line-product-code-0'), { target: { value: code } });
  const listbox = await screen.findByRole('listbox');
  fireEvent.mouseDown(within(listbox).getByText(code));
}

describe('StockLineGrid', () => {
  beforeEach(() => {
    localStorage.clear();
    useProductSearchMock.mockReset();
    useProductSearchMock.mockReturnValue({ data: [sheet], isLoading: false, isError: false });
    useProductsMock.mockReset();
    useProductsMock.mockReturnValue({ data: { items: [listItem], totalPages: 1 }, isLoading: false });
  });

  it('selecting a product on a stock-in fills cost price, purchase discount, VAT and dimensions', async () => {
    const { values } = renderGrid({ type: 'In' });
    await pickFromTypeahead('TON01');

    expect(values().lines[0]).toMatchObject({
      productId: P1,
      productCode: 'TON01',
      productName: 'Tôn lạnh',
      pricingMode: 'PerSquareMeter',
      unitName: 'm²',
      trackInventory: true,
      priceIncludesVat: true,
      unitPrice: 90_000,
      discountRate: 3,
      vatRate: 8,
      length: 2000,
      width: 1200,
      sheetCount: '',
      quantity: 0,
      discountManual: false,
      warehouseId: WH1,
    });
  });

  it('selecting a product on a stock-out fills sales price and sales discount', async () => {
    const { values } = renderGrid({ type: 'Out' });
    await pickFromTypeahead('TON01');

    expect(values().lines[0]).toMatchObject({ unitPrice: 120_000, discountRate: 5, vatRate: 8 });
  });

  it('dimension inputs follow the pricing mode', () => {
    renderGrid({
      lines: [
        line({ pricingMode: 'PerUnit' }),
        line({ _uiKey: 'b', pricingMode: 'PerLinearMeter' }),
        line({ _uiKey: 'c', pricingMode: 'PerSquareMeter' }),
        line({ _uiKey: 'd', pricingMode: 'PerCubicMeter' }),
      ],
    });
    const enabled = (idx: number) =>
      ['sheet-count', 'length', 'width', 'thickness', 'quantity'].filter(
        (field) => !byId(`stock-line-${field}-${idx}`).disabled,
      );

    expect(enabled(0)).toEqual(['quantity']);
    expect(enabled(1)).toEqual(['sheet-count', 'length']);
    expect(enabled(2)).toEqual(['sheet-count', 'length', 'width']);
    expect(enabled(3)).toEqual(['sheet-count', 'length', 'width', 'thickness']);
  });

  it('editing discount amount switches the line to manual discount', () => {
    showAllColumns();
    const { values } = renderGrid({
      lines: [line({ quantity: 2, unitPrice: 50_000, discountRate: 10 })],
      computed: [zeroComputed({ quantity: 2, amount: 100_000, discountAmount: 10_000 })],
    });
    expect(byId('stock-line-discount-amount-0')).toHaveValue('10.000');

    fireEvent.change(byId('stock-line-discount-amount-0'), { target: { value: '5000' } });
    expect(values().lines[0]).toMatchObject({ discountManual: true, discountAmount: 5000 });

    fireEvent.change(byId('stock-line-discount-rate-0'), { target: { value: '7' } });
    expect(values().lines[0]).toMatchObject({ discountManual: false, discountRate: 7 });
  });

  it('shows stock at voucher time for tracked lines and a dash for untracked ones', () => {
    renderGrid({
      lines: [
        line({ productId: P1, trackInventory: true, warehouseId: WH2 }),
        line({ _uiKey: 'b', productId: P2, trackInventory: false }),
      ],
      stockAt: { [`${P1}:${WH2}`]: 12.5, [`${P2}:${WH1}`]: 3 },
    });

    expect(byId('stock-line-stock-at-0')).toHaveTextContent('12.5');
    expect(byId('stock-line-stock-at-1')).toHaveTextContent('—');
  });

  it('small cubic quantities keep six decimals', () => {
    renderGrid({
      lines: [line({ pricingMode: 'PerCubicMeter', sheetCount: 1, length: 1000, width: 59, thickness: 62 })],
      computed: [zeroComputed({ quantity: 0.003658 + 0.000001 })],
    });

    expect(byId('stock-line-quantity-0')).toHaveValue('0.003659');
  });

  it('selecting through the catalog dialog applies the same autofill', async () => {
    const { values } = renderGrid({ type: 'In' });
    fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'GO' } });
    fireEvent.mouseDown(await screen.findByText('Xem danh mục đầy đủ'));
    fireEvent.doubleClick(await screen.findByText('Gỗ thông'));

    await waitFor(() =>
      expect(values().lines[0]).toMatchObject({
        productId: P2,
        pricingMode: 'PerCubicMeter',
        unitName: 'm³',
        unitPrice: 7_000_000,
        discountRate: 2,
        vatRate: 10,
        length: 3000,
        width: 100,
        thickness: 50,
      }),
    );
  });

  it('read-only grid disables inputs', () => {
    renderGrid({ lines: [line(), line({ _uiKey: 'b' })], readOnly: true });

    const controls = document.body.querySelectorAll('input, select, textarea');
    expect(controls.length).toBeGreaterThan(0);
    controls.forEach((el) => expect(el).toBeDisabled());
    expect(screen.queryByRole('button', { name: /Thêm dòng/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Xóa dòng/ })).not.toBeInTheDocument();
  });

  it('cell labels carry the row number and cell errors are described', async () => {
    let setError: ((message: string) => void) | undefined;
    function ErrorHarness() {
      const form = useForm<StockVoucherFormValues, unknown, StockVoucherFormParsed>({
        defaultValues: { ...toFormDefaults(), warehouseId: WH1, lines: [line(), line({ _uiKey: 'b' })] },
      });
      setError = (message) => form.setError('lines.1.unitPrice', { type: 'server', message });
      return (
        <StockLineGrid form={form} type="In" warehouses={warehouses} computed={[]} stockAt={{}} readOnly={false} />
      );
    }
    render(<ErrorHarness />);

    expect(screen.getByLabelText('Số lượng dòng 2')).toBe(byId('stock-line-quantity-1'));
    expect(screen.getByRole('button', { name: 'Xóa dòng 1' })).toBeInTheDocument();

    act(() => setError?.('Đơn giá không hợp lệ'));
    const price = await screen.findByLabelText('Đơn giá dòng 2');
    await waitFor(() => expect(price).toHaveAttribute('aria-invalid', 'true'));
    expect(price).toHaveAccessibleDescription('Đơn giá không hợp lệ');
  });

  it('an emptied %VAT stays empty while typing, counts as 0 and shows 0 after blur', () => {
    const { values } = renderGrid({ lines: [line({ vatRate: 10 })] });
    const vat = byId('stock-line-vat-rate-0');

    fireEvent.change(vat, { target: { value: '' } });
    expect(vat).toHaveValue(null);
    expect(values().lines[0].vatRate).toBe(0);

    fireEvent.blur(vat);
    expect(vat).toHaveValue(0);
  });

  it('hides Kho, Tiền CK, Tiền VAT and Ghi chú by default', () => {
    renderGrid({ lines: [line()] });
    expect(headers()).toEqual([
      'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'SL tồn',
      'Đơn giá', 'Số tiền', '%CK', '%VAT', 'Số còn lại', '',
    ]);
    expect(document.getElementById('stock-line-warehouse-0')).toBeNull();
    expect(document.getElementById('stock-line-note-0')).toBeNull();
  });

  it('stored prefs show every column', () => {
    showAllColumns();
    renderGrid({ lines: [line()] });
    expect(headers()).toEqual([
      'STT', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Kho', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'SL tồn',
      'Đơn giá', 'Số tiền', '%CK', 'Tiền CK', '%VAT', 'Tiền VAT', 'Ghi chú', 'Số còn lại', '',
    ]);
  });

  it('forces Kho when a line leaves the header warehouse', () => {
    renderGrid({ lines: [line(), line({ _uiKey: 'b', warehouseId: WH2 })] });
    expect(headers()).toContain('Kho');
  });

  it('keeps Kho hidden while every line uses the header warehouse', () => {
    renderGrid({ lines: [line(), line({ _uiKey: 'b' })] });
    expect(headers()).not.toContain('Kho');
  });

  it('a read-only grid has no delete column (15 headers by default)', () => {
    renderGrid({ lines: [line()], readOnly: true });
    expect(headers()).toHaveLength(15);
    expect(headers()).not.toContain('');
  });

  it('a line error on a hidden column shows it', async () => {
    const { form } = renderGrid({ lines: [line()] });
    expect(headers()).not.toContain('Ghi chú');
    act(() => form().setError('lines.0.note', { type: 'server', message: 'Quá dài' }));
    await waitFor(() => expect(headers()).toContain('Ghi chú'));
  });

  it('an array-level lines error (empty grid) does not crash the render', async () => {
    const { form } = renderGrid({ lines: [line()] });
    act(() => form().setError('lines', { type: 'server', message: 'Phiếu phải có ít nhất 1 dòng' }));
    await waitFor(() => expect(headers()).toContain('STT'));
  });

  it('pins #, Mã and Tên left and Số còn lại + delete right with cumulative offsets', () => {
    renderGrid({ lines: [line()] });
    const ths = screen.getAllByRole('columnheader');
    const byText = (t: string) => ths.find((th) => th.textContent === t)!;
    expect(byText('STT')).toHaveClass('sticky-left');
    expect(byText('STT')).toHaveStyle({ left: '0px' });
    expect(byText('Mã hàng')).toHaveStyle({ left: '42px' });
    expect(byText('Tên hàng')).toHaveStyle({ left: '162px' });
    expect(byText('Số còn lại')).toHaveClass('sticky-right');
    expect(byText('Số còn lại')).toHaveStyle({ right: '42px' });
    expect(byText('ĐVT')).not.toHaveClass('sticky-left');
    // Toolbar sits above the table.
    const toolbar = screen.getByRole('button', { name: /Thêm dòng/ });
    expect(toolbar.compareDocumentPosition(screen.getByRole('table')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    // The fixed-layout table is exactly as wide as the column sum, so the sticky offsets match real widths.
    expect(screen.getByRole('table')).toHaveStyle({ width: '1428px', minWidth: '1428px' });
  });

  it('a read-only grid keeps the toolbar settings but has no Thêm dòng', () => {
    renderGrid({ lines: [line()], readOnly: true });
    expect(screen.queryByRole('button', { name: /Thêm dòng/ })).toBeNull();
    expect(document.querySelector('.stock-grid-toolbar')).not.toBeNull();
  });

  // Assert on the grid itself: the popover's own matrix table also has a "Ghi chú" columnheader,
  // so headers() would pass before the click.
  it('the settings popover shows a hidden column at once and stores the choice', () => {
    renderGrid({ lines: [line()] });
    expect(document.getElementById('stock-line-note-0')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Ghi chú' }));
    expect(document.getElementById('stock-line-note-0')).not.toBeNull();
    const stored = JSON.parse(localStorage.getItem(stockGridPrefsKey('', 'In'))!);
    expect(stored.hiddenColumns).not.toContain('note');
  });

  it('a read-only grid can still reveal a hidden column from the popover', () => {
    renderGrid({ lines: [line()], readOnly: true });
    expect(document.getElementById('stock-line-note-0')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Ghi chú' }));
    expect(document.getElementById('stock-line-note-0')).not.toBeNull();
  });

  const enter = (id: string, shift = false) => fireEvent.keyDown(byId(id), { key: 'Enter', shiftKey: shift });
  const focused = () => document.activeElement?.id;

  it('picking a m² product focuses Số tấm, then Enter walks Dài, Rộng, Đơn giá and opens a new line', async () => {
    renderGrid({ lines: [createEmptyStockLine(0, WH1)] });
    await pickFromTypeahead('TON01');
    await waitFor(() => expect(focused()).toBe('stock-line-sheet-count-0'));
    enter('stock-line-sheet-count-0');
    expect(focused()).toBe('stock-line-length-0');
    enter('stock-line-length-0');
    expect(focused()).toBe('stock-line-width-0');
    enter('stock-line-width-0');
    expect(focused()).toBe('stock-line-unit-price-0');
    enter('stock-line-unit-price-0');
    await waitFor(() => expect(focused()).toBe('stock-line-product-code-1'));
  });

  it('a PerUnit line stops at SL and Đơn giá; stored stops add %VAT', () => {
    storePrefs({ hiddenColumns: [], enterStops: { PerUnit: ['quantity', 'unit-price', 'vat-rate'] } });
    renderGrid({ lines: [line({ pricingMode: 'PerUnit' }), line({ _uiKey: 'b', pricingMode: 'PerUnit' })] });
    enter('stock-line-product-code-0');
    expect(focused()).toBe('stock-line-quantity-0');
    enter('stock-line-quantity-0');
    expect(focused()).toBe('stock-line-unit-price-0');
    enter('stock-line-unit-price-0');
    expect(focused()).toBe('stock-line-vat-rate-0');
    enter('stock-line-vat-rate-0');
    expect(focused()).toBe('stock-line-product-code-1');
  });

  it('Shift+Enter from a product code goes to the last stop of the line above, by its mode', () => {
    renderGrid({ lines: [line({ pricingMode: 'PerSquareMeter' }), line({ _uiKey: 'b' })] });
    enter('stock-line-product-code-1', true);
    expect(focused()).toBe('stock-line-unit-price-0');
    enter('stock-line-unit-price-0', true);
    expect(focused()).toBe('stock-line-width-0');
  });

  it('Enter on a product code without a product does nothing', () => {
    showAllColumns(); // Kho visible: the old code would move to the Kho select, so this test fails in RED
    renderGrid({ lines: [createEmptyStockLine(0, WH1)] });
    byId('stock-line-product-code-0').focus();
    enter('stock-line-product-code-0');
    expect(focused()).toBe('stock-line-product-code-0');
    expect(document.getElementById('stock-line-product-code-1')).toBeNull();
  });

  it('ArrowDown keeps the column, or falls back to Mã when the row below cannot use it', () => {
    renderGrid({
      lines: [line({ pricingMode: 'PerSquareMeter' }), line({ _uiKey: 'b', pricingMode: 'PerUnit' }), line({ _uiKey: 'c', pricingMode: 'PerUnit' })],
    });
    fireEvent.keyDown(byId('stock-line-width-0'), { key: 'ArrowDown' });
    expect(focused()).toBe('stock-line-product-code-1');
    fireEvent.keyDown(byId('stock-line-unit-price-1'), { key: 'ArrowDown' });
    expect(focused()).toBe('stock-line-unit-price-2');
    fireEvent.keyDown(byId('stock-line-unit-price-2'), { key: 'ArrowUp' });
    expect(focused()).toBe('stock-line-unit-price-1');
  });

  // Programmatic focus on a filled Mã cell must not open the suggestion list: an open list would swallow
  // up/down (list highlight) and Enter (re-pick item 0, resetting quantity and price).
  it('focusing a filled Mã does not open the list; ArrowDown moves rows and keeps the quantity', () => {
    const { values } = renderGrid({ lines: [line({ quantity: 5 }), line({ _uiKey: 'b' })] });
    act(() => byId('stock-line-product-code-0').focus());
    expect(screen.queryByRole('listbox')).toBeNull();
    fireEvent.keyDown(byId('stock-line-product-code-0'), { key: 'ArrowDown' });
    expect(focused()).toBe('stock-line-product-code-1');
    expect(values().lines[0].quantity).toBe(5);
  });

  it('ArrowUp on the first row and ArrowDown on the last row are swallowed so number cells keep their value', () => {
    renderGrid({ lines: [line({ pricingMode: 'PerSquareMeter', width: 1200 }), line({ _uiKey: 'b', pricingMode: 'PerSquareMeter', width: 800 })] });
    const first = byId('stock-line-width-0');
    const last = byId('stock-line-width-1');
    expect(first).toHaveAttribute('type', 'number');
    // fireEvent returns false when the event was cancelled (the browser would otherwise step the value).
    expect(fireEvent.keyDown(first, { key: 'ArrowUp' })).toBe(false);
    expect(fireEvent.keyDown(last, { key: 'ArrowDown' })).toBe(false);
  });

  // Chrome's select() also focuses the element, so a late select() would pull the focus back.
  it('a number cell selects its text on focus only while it is still the active element', async () => {
    renderGrid({ lines: [line({ quantity: 5 }), line({ _uiKey: 'b' })] });
    const select = vi.spyOn(HTMLInputElement.prototype, 'select');
    try {
      act(() => byId('stock-line-quantity-0').focus());
      act(() => byId('stock-line-quantity-1').focus());
      await new Promise((resolve) => requestAnimationFrame(() => resolve(null)));
      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(select.mock.contexts).toEqual([byId('stock-line-quantity-1')]);
    } finally {
      select.mockRestore();
    }
  });

  it('arrows leave the warehouse select alone', () => {
    showAllColumns();
    renderGrid({ lines: [line(), line({ _uiKey: 'b' })] });
    byId('stock-line-warehouse-0').focus();
    fireEvent.keyDown(byId('stock-line-warehouse-0'), { key: 'ArrowDown' });
    expect(focused()).toBe('stock-line-warehouse-0');
  });

  it('Ctrl+Delete removes the focused line and keeps the column on the next one', async () => {
    const { values } = renderGrid({
      lines: [line({ productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' })],
    });
    fireEvent.keyDown(byId('stock-line-quantity-0'), { key: 'Delete', ctrlKey: true });
    await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['B']));
    await waitFor(() => expect(focused()).toBe('stock-line-quantity-0'));
  });

  it('Ctrl+Delete on a middle or last line keeps the column, on the next line or else the previous one', async () => {
    const { values } = renderGrid({
      lines: [line({ productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' }), line({ _uiKey: 'c', productCode: 'C' })],
    });
    fireEvent.keyDown(byId('stock-line-quantity-1'), { key: 'Delete', ctrlKey: true });
    await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['A', 'C']));
    await waitFor(() => expect(focused()).toBe('stock-line-quantity-1'));
    // The last line: there is no next line, so focus goes to the previous one.
    byId('stock-line-product-code-0').focus();
    fireEvent.keyDown(byId('stock-line-quantity-1'), { key: 'Delete', ctrlKey: true });
    await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['A']));
    await waitFor(() => expect(focused()).toBe('stock-line-quantity-0'));
  });

  it('Ctrl+Delete on the first of three lines removes the DOM node of that row and keeps the others mounted and focused', async () => {
    const { values } = renderGrid({
      lines: [line({ _uiKey: 'a', productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' }), line({ _uiKey: 'c', productCode: 'C' })],
    });
    const rowA = byId('stock-line-quantity-0');
    const rowB = byId('stock-line-quantity-1');
    const rowC = byId('stock-line-quantity-2');
    act(() => rowA.focus());
    fireEvent.keyDown(rowA, { key: 'Delete', ctrlKey: true });
    await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['B', 'C']));
    // The right row is removed in the first commit: B and C are never torn down and rebuilt.
    expect(rowA.isConnected).toBe(false);
    expect(rowB.isConnected).toBe(true);
    expect(rowC.isConnected).toBe(true);
    await waitFor(() => expect(document.activeElement).toBe(rowB));
  });

  it('focus never rests on <body> while the row is removed (Ctrl+Delete and the delete button)', async () => {
    // A setTimeout/effect refocus passes the waitFor checks above but leaves a window with the focus on <body>.
    // Record the active element right after every DOM removal: it must already sit on a grid cell.
    const resting: Array<Element | null> = [];
    const original = Node.prototype.removeChild;
    const spy = vi.spyOn(Node.prototype, 'removeChild').mockImplementation(function <T extends Node>(this: Node, child: T): T {
      const result = original.call(this, child) as T;
      if ((child as Element).tagName === 'TR') resting.push(document.activeElement);
      return result;
    });
    try {
      const { values } = renderGrid({
        lines: [line({ _uiKey: 'a', productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' }), line({ _uiKey: 'c', productCode: 'C' })],
      });
      act(() => byId('stock-line-quantity-1').focus());
      fireEvent.keyDown(byId('stock-line-quantity-1'), { key: 'Delete', ctrlKey: true });
      await waitFor(() => expect(values().lines).toHaveLength(2));
      fireEvent.click(screen.getAllByRole('button', { name: /Xóa/ })[1]);
      await waitFor(() => expect(values().lines).toHaveLength(1));
    } finally {
      spy.mockRestore();
    }
    expect(resting).toHaveLength(2);
    for (const el of resting) expect(el).not.toBe(document.body);
  });

  it('deleting the only line, by Ctrl+Delete or by its button, moves focus to Thêm dòng', async () => {
    const { values } = renderGrid({ lines: [line({ productCode: 'A' })] });
    fireEvent.keyDown(byId('stock-line-quantity-0'), { key: 'Delete', ctrlKey: true });
    await waitFor(() => expect(values().lines).toHaveLength(0));
    await waitFor(() => expect(focused()).toBe('stock-line-add'));
    // Insert from the button adds a line again, so the keyboard never dead-ends.
    fireEvent.click(byId('stock-line-add'));
    await waitFor(() => expect(values().lines).toHaveLength(1));
    byId('stock-line-add').blur();
    fireEvent.click(screen.getByRole('button', { name: /Xóa/ }));
    await waitFor(() => expect(values().lines).toHaveLength(0));
    await waitFor(() => expect(focused()).toBe('stock-line-add'));
  });

  it('the delete button keeps focus in the grid, on the Mã of the line that takes its place', async () => {
    const { values } = renderGrid({ lines: [line({ productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' })] });
    fireEvent.click(screen.getAllByRole('button', { name: /Xóa/ })[0]);
    await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['B']));
    await waitFor(() => expect(focused()).toBe('stock-line-product-code-0'));
  });

  it('Ctrl+Delete does nothing on a read-only grid', async () => {
    const { values } = renderGrid({
      lines: [line({ productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' })],
      readOnly: true,
    });
    fireEvent.keyDown(byId('stock-line-quantity-0'), { key: 'Delete', ctrlKey: true });
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(values().lines.map((l) => l.productCode)).toEqual(['A', 'B']);
  });

  it('the keyboard guide lists the new keys', () => {
    renderGrid({ lines: [line()] });
    const guide = document.querySelector('.keyboard-guide')!;
    expect(guide).toHaveTextContent('Ctrl+Del');
    expect(guide).toHaveTextContent('Alt+1…4');
    expect(guide).toHaveTextContent('Dòng trên/dưới');
  });

  it('below 2xl the same keys are reachable from the Phím tắt popover', async () => {
    renderGrid({ lines: [line()] });
    fireEvent.click(screen.getByRole('button', { name: /Phím tắt/ }));
    await waitFor(() => expect(document.querySelector('.keyboard-guide-popover')).not.toBeNull());
    const popover = document.querySelector('.keyboard-guide-popover')!;
    expect(popover).toHaveTextContent('Ctrl+Del');
    expect(popover).toHaveTextContent('Alt+1…4');
    expect(popover).toHaveTextContent('Dòng trên/dưới');
  });
});
