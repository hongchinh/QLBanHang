import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useForm, type FieldPath, type UseFormReturn } from 'react-hook-form';
import { StockLineGrid } from './stock-line-grid';
import { stockGridPrefsKey } from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';
import { toFormDefaults, createEmptyStockLine } from '@/features/stock-vouchers/payload';
import type {
  StockLineFormValues,
  StockVoucherFormParsed,
  StockVoucherFormValues,
} from '@/features/stock-vouchers/schema';
import type * as DialogModule from '@/components/ui/dialog';
import type { ProductQuickAddDialogProps } from '@/features/products/components/product-quick-add-dialog';
import type { ProductListItem, ProductSuggestion } from '@/features/products/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import type { Warehouse } from '@/features/warehouses/types';
import type { StockLineComputed } from '@/pages/stock-vouchers/utils/compute-stock-line';

const useProductSearchMock = vi.fn();
const useProductsMock = vi.fn();

// No `user`: the Enter preferences stay under the key of user ''.
let permissions: string[] = ['products.create'];
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => permissions.includes(p), isInRole: () => false }),
}));

// Stands in for the real form: a save hands back a product, then closes, as the real dialog does.
vi.mock('@/features/products/components/product-quick-add-dialog', async () => {
  const ui = await vi.importActual<typeof DialogModule>('@/components/ui/dialog');
  return {
    ProductQuickAddDialog: ({ open, onOpenChange, onCreated, onCloseAutoFocus }: ProductQuickAddDialogProps) => (
      <ui.Dialog open={open} onOpenChange={onOpenChange}>
        <ui.DialogContent aria-describedby={undefined} onCloseAutoFocus={onCloseAutoFocus}>
          <ui.DialogTitle>Thêm nhanh hàng hóa</ui.DialogTitle>
          <button
            type="button"
            onClick={() => {
              onCreated(quickAddResult);
              onOpenChange(false);
            }}
          >
            fake lưu
          </button>
        </ui.DialogContent>
      </ui.Dialog>
    ),
  };
});

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

const quickAdded: ProductSuggestion = {
  ...sheet,
  id: 'new-id',
  code: 'TL-50',
  name: 'Tôn lạnh 0.50',
  pricingMode: 'PerUnit',
};
// What the mocked quick add dialog hands back; reset to quickAdded before each test.
let quickAddResult: ProductSuggestion = quickAdded;

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

// Runs `steps` outside act(), as the browser does, then lets the renders that follow them land.
async function outsideAct(steps: () => Promise<void>) {
  const env = globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean };
  const previous = env.IS_REACT_ACT_ENVIRONMENT;
  env.IS_REACT_ACT_ENVIRONMENT = false;
  try {
    await steps();
    await new Promise((resolve) => setTimeout(resolve, 20));
  } finally {
    env.IS_REACT_ACT_ENVIRONMENT = previous;
  }
}

// Only the microtasks run, in which React renders what an event changed: no task, frame or timer.
async function eventRendered() {
  for (let i = 0; i < 5; i++) await Promise.resolve();
}

describe('StockLineGrid', () => {
  beforeEach(() => {
    localStorage.clear();
    permissions = ['products.create'];
    quickAddResult = quickAdded;
    useProductSearchMock.mockReset();
    useProductSearchMock.mockImplementation((text: string) => ({
      data: [sheet],
      isLoading: false,
      isError: false,
      isFetching: false,
      settledQuery: text,
    }));
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

  it('an error on Ghi chú marks the cell and shows its message under it, like the other cells', () => {
    const { form } = renderGrid({ lines: [line({ note: 'qua dai' })] });

    act(() => form().setError('lines.0.note', { type: 'server', message: 'Ghi chú quá dài.' }));

    const note = byId('stock-line-note-0');
    expect(note).toHaveAttribute('aria-invalid', 'true');
    expect(note).toHaveAccessibleDescription('Ghi chú quá dài.');
    expect(screen.getByText('Ghi chú quá dài.')).toBeVisible();
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

  // Unlike %VAT, an emptied Số tấm / Dài / Rộng / Dày means "no value": the line keeps '', not 0.
  it.each([
    ['Số tấm', 'sheet-count', 'sheetCount'],
    ['Dài', 'length', 'length'],
    ['Rộng', 'width', 'width'],
    ['Dày', 'thickness', 'thickness'],
  ] as const)('an emptied %s leaves the line without that value, not with 0', (_label, cell, field) => {
    const { values } = renderGrid({
      lines: [line({ pricingMode: 'PerCubicMeter', sheetCount: 2, length: 3000, width: 100, thickness: 50 })],
    });
    const input = byId(`stock-line-${cell}-0`);

    fireEvent.change(input, { target: { value: '' } });

    expect(values().lines[0][field]).toBe('');
    expect(input).toHaveValue(null);
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

  // The page drops a server error at the first change of the value it was about, so the correction ends what forced
  // the column in at its first key. The rule: a column stays while the focus is in one of its cells.
  describe('a hidden column that an error or the warehouse forced into view', () => {
    // What the page does with a server error: it goes as soon as the value it was about changes.
    function clearServerErrorsOnChange(form: UseFormReturn<StockVoucherFormValues, unknown, StockVoucherFormParsed>) {
      form.watch((_values, { name }) => {
        const path = name as FieldPath<StockVoucherFormValues> | undefined;
        if (path && form.getFieldState(path).error?.type === 'server') form.clearErrors(path);
      });
    }

    it.each([
      ['Tiền CK', 'discount-amount', 'discountAmount', [], ['5', '50', '500', '5000'], 5000],
      ['Ghi chú', 'note', 'note', [], ['m', 'mo', 'mot'], 'mot'],
      ['%VAT', 'vat-rate', 'vatRate', ['vat-rate'], ['1', '10'], 10],
    ] as const)('%s keeps its cell, the focus and every key while the user corrects a server error in it', (header, cell, field, hidden, keys, typed) => {
      if (hidden.length) storePrefs({ hiddenColumns: [...hidden] });
      const { values, form } = renderGrid({ lines: [line({ discountManual: true, discountAmount: 20_000, note: 'qua dai', vatRate: 8 })] });
      clearServerErrorsOnChange(form());
      expect(headers()).not.toContain(header);
      act(() => form().setError(`lines.0.${field}`, { type: 'server', message: 'Giá trị không hợp lệ.' }));
      const input = byId(`stock-line-${cell}-0`);
      act(() => input.focus()); // where the page puts the focus after a rejected save

      for (const text of keys) {
        fireEvent.change(input, { target: { value: text } });
        expect(input.isConnected).toBe(true);
        expect(document.activeElement).toBe(input);
      }

      expect(form().getFieldState(`lines.0.${field}`).error).toBeUndefined();
      expect(values().lines[0][field]).toBe(typed);
      expect(headers()).toContain(header);
      // Once the focus leaves the cell, the column the user hid goes again.
      enter(`stock-line-${cell}-0`, true); // Shift+Enter: back to Đơn giá
      expect(focused()).toBe('stock-line-unit-price-0');
      expect(headers()).not.toContain(header);
      expect(values().lines[0][field]).toBe(typed);
    });

    it('Kho keeps its select and the focus when the user picks the header warehouse in it', () => {
      const { values } = renderGrid({ lines: [line({ warehouseId: WH2 })] });
      const select = byId('stock-line-warehouse-0');
      act(() => select.focus());

      fireEvent.change(select, { target: { value: WH1 } });

      expect(values().lines[0].warehouseId).toBe(WH1);
      expect(select.isConnected).toBe(true);
      expect(document.activeElement).toBe(select);
      act(() => byId('stock-line-unit-price-0').focus());
      expect(headers()).not.toContain('Kho');
    });

    // When the window loses the focus (the user switches to another program) the cell gets a blur event but keeps the
    // focus of the page: it must still be there when the user comes back.
    it('the column stays while the window is in the background', () => {
      const { form } = renderGrid({ lines: [line({ note: 'qua dai' })] });
      clearServerErrorsOnChange(form());
      act(() => form().setError('lines.0.note', { type: 'server', message: 'Giá trị không hợp lệ.' }));
      const input = byId('stock-line-note-0');
      act(() => input.focus());
      fireEvent.change(input, { target: { value: 'm' } });

      fireEvent.blur(input);
      fireEvent.focusOut(input);

      expect(document.activeElement).toBe(input);
      expect(input.isConnected).toBe(true);
      expect(headers()).toContain('Ghi chú');
    });

    // A click on another cell: the browser blurs the cell the focus leaves (the page has no focused element for that
    // moment), runs the microtasks, in which React renders, and only then focuses the clicked cell.
    it('a click on another cell of the column that only the focus holds keeps that column', async () => {
      const { form } = renderGrid({ lines: [line({ note: 'qua dai' }), line({ _uiKey: 'b', note: 'ghi chu 2' })] });
      clearServerErrorsOnChange(form());
      act(() => form().setError('lines.0.note', { type: 'server', message: 'Giá trị không hợp lệ.' }));
      const first = byId('stock-line-note-0');
      const second = byId('stock-line-note-1');
      act(() => first.focus());
      fireEvent.change(first, { target: { value: 'm' } }); // the error is gone: only the focus holds Ghi chú

      await outsideAct(async () => {
        const noFocus = vi.spyOn(document, 'activeElement', 'get').mockReturnValue(document.body);
        try {
          first.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: second }));
        } finally {
          noFocus.mockRestore();
        }
        await eventRendered();
        second.focus();
      });

      expect(second.isConnected).toBe(true);
      expect(document.activeElement).toBe(second);
      expect(headers()).toContain('Ghi chú');
    });
  });

  it('the add-line button carries the create-toned add icon', () => {
    renderGrid({ lines: [line()] });
    const icon = screen.getByRole('button', { name: /Thêm dòng/ }).querySelector('[data-icon="add"]');
    expect(icon).toHaveClass('text-icon-create');
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
  // Resolves after the frame callbacks the grid has requested so far have run.
  const nextFrame = () => new Promise((resolve) => requestAnimationFrame(() => resolve(null)));

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

  it('an Enter that commits an IME composition does not move the focus', () => {
    renderGrid({ lines: [line({ pricingMode: 'PerSquareMeter' })] });
    byId('stock-line-product-code-0').focus();
    fireEvent.keyDown(byId('stock-line-product-code-0'), { key: 'Enter', isComposing: true, keyCode: 229 });
    expect(focused()).toBe('stock-line-product-code-0');
    enter('stock-line-product-code-0');
    expect(focused()).not.toBe('stock-line-product-code-0');
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
      // The focus moves twice inside one batch, so the first cell never renders its raw text: only the cell that
      // still has the focus may select.
      act(() => {
        byId('stock-line-quantity-0').focus();
        byId('stock-line-quantity-1').focus();
      });
      // It has selected already: a keyboard focus waits for no frame.
      expect(select.mock.contexts).toEqual([byId('stock-line-quantity-1')]);
      // ... and nothing else is selected one frame later.
      await new Promise((resolve) => requestAnimationFrame(() => resolve(null)));
      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(select.mock.contexts).toEqual([byId('stock-line-quantity-1')]);
    } finally {
      select.mockRestore();
    }
  });

  // A cell that takes and loses the focus inside one batch never renders its raw text, so it still waits to select
  // on its next render. That render comes later, with the focus somewhere else (here the user types in the second
  // cell and the whole grid renders), and must select nothing. jsdom's select() does not move the focus the way
  // Chrome's does, so the test watches select() itself.
  it('a number cell that lost the focus before it rendered does not select on a later render', () => {
    const { values } = renderGrid({ lines: [line({ quantity: 5 }), line({ _uiKey: 'b', quantity: 7 })] });
    const select = vi.spyOn(HTMLInputElement.prototype, 'select');
    try {
      const first = byId('stock-line-quantity-0');
      const second = byId('stock-line-quantity-1');
      act(() => {
        first.focus();
        second.focus();
      });
      expect(select.mock.contexts).toEqual([second]);

      fireEvent.change(second, { target: { value: '8' } });

      expect(values().lines[1].quantity).toBe(8);
      expect(select.mock.contexts).toEqual([second]);
    } finally {
      select.mockRestore();
    }
  });

  // A digit can arrive before the next frame; the whole raw text must already be selected then. fireEvent runs in
  // act(), which returns once React has rendered what the key changed (in the browser that render comes in a
  // microtask right after the key event, before the next key); no frame or timer runs in between.
  it('keyboard navigation selects the raw text of the number cell in the render it causes, with no frame or timer', () => {
    renderGrid({ lines: [line({ quantity: 5, unitPrice: 30000 })] });
    byId('stock-line-quantity-0').focus();
    enter('stock-line-quantity-0'); // quantity -> unit price, no frame awaited
    const price = byId('stock-line-unit-price-0') as HTMLInputElement;
    expect(focused()).toBe('stock-line-unit-price-0');
    expect(price.value).toBe('30000');
    expect([price.selectionStart, price.selectionEnd]).toEqual([0, 5]);
  });

  // The path the defect was reported on: a digit typed right after Ctrl+Delete was appended to the old value of the
  // cell that took the focus.
  it('Ctrl+Delete: the cell that takes the focus has its raw text selected in the render it causes, with no frame or timer', () => {
    renderGrid({
      lines: [line({ productCode: 'A', quantity: 5 }), line({ _uiKey: 'b', productCode: 'B', quantity: 1250 })],
    });
    const first = byId('stock-line-quantity-0');
    const second = byId('stock-line-quantity-1');
    expect(second.value).toBe('1,250'); // idle: formatted
    act(() => first.focus());
    fireEvent.keyDown(first, { key: 'Delete', ctrlKey: true }); // no frame awaited
    expect(document.activeElement).toBe(second);
    expect(second.value).toBe('1250');
    expect([second.selectionStart, second.selectionEnd]).toEqual([0, 4]);
  });

  // The select runs in a layout effect with no dependency list, that is after every render, so it has to fire once
  // per keyboard focus. Typing renders the cell: a select on those renders would make each digit replace the one
  // typed before it.
  it('typing after a keyboard focus does not select the text again', () => {
    const { values } = renderGrid({ lines: [line({ quantity: 5, unitPrice: 30000 })] });
    const select = vi.spyOn(HTMLInputElement.prototype, 'select');
    try {
      act(() => byId('stock-line-quantity-0').focus());
      enter('stock-line-quantity-0'); // Đơn giá takes the focus by keyboard
      const price = byId('stock-line-unit-price-0');
      const selectsOnPrice = () => select.mock.contexts.filter((el) => el === price).length;
      expect(selectsOnPrice()).toBe(1);

      fireEvent.change(price, { target: { value: '4' } });
      fireEvent.change(price, { target: { value: '45' } });

      expect(selectsOnPrice()).toBe(1);
      // The caret is after the two digits and nothing is selected: the next digit will be appended.
      expect([price.selectionStart, price.selectionEnd]).toEqual([2, 2]);
      expect(values().lines[0].unitPrice).toBe(45);
    } finally {
      select.mockRestore();
    }
  });

  // What a click on Đơn giá / SL does in Chrome (measured; the same as before the keyboard fix): the browser puts the
  // caret where the user clicked, right after the focus event, and one frame later the cell selects its WHOLE text.
  // So the first click on a cell selects the whole number: the caret does not stay where the user clicked.
  // jsdom places no caret; the test shows the cell's part: no select in the focus event, everything a frame later.
  it('a number cell focused with the mouse selects its whole text one frame later, not in the focus event', async () => {
    renderGrid({ lines: [line({ quantity: 1250 })] });
    const select = vi.spyOn(HTMLInputElement.prototype, 'select');
    try {
      const cell = byId('stock-line-quantity-0');
      fireEvent.mouseDown(cell);
      act(() => cell.focus());
      expect(select).not.toHaveBeenCalled();
      await nextFrame();
      expect(select.mock.contexts).toEqual([cell]);
      expect(cell.value).toBe('1250');
      expect([cell.selectionStart, cell.selectionEnd]).toEqual([0, 4]);
    } finally {
      select.mockRestore();
    }
  });

  // Only a click in the cell that already has the focus leaves the caret where the user clicked (measured in
  // Chrome): that click brings no focus event, so the cell selects nothing. jsdom cannot show the caret; the test
  // shows that no select() follows such a click.
  it('a click inside the number cell that already has the focus selects nothing', async () => {
    renderGrid({ lines: [line({ quantity: 1250 })] });
    const select = vi.spyOn(HTMLInputElement.prototype, 'select');
    try {
      const cell = byId('stock-line-quantity-0');
      fireEvent.mouseDown(cell);
      act(() => cell.focus());
      await nextFrame();
      expect(select.mock.contexts).toEqual([cell]); // the first click: the whole text
      select.mockClear();

      fireEvent.mouseDown(cell);
      fireEvent.mouseUp(cell);
      fireEvent.click(cell);
      await nextFrame();

      expect(select).not.toHaveBeenCalled();
      expect(document.activeElement).toBe(cell);
    } finally {
      select.mockRestore();
    }
  });

  // A press inside the cell that already has the focus (the user moves the caret) brings no focus event that would
  // use it up. The cell forgets the press when it loses the focus; otherwise its next keyboard visit would count as a
  // mouse one and select one frame late.
  it('a click inside the focused number cell does not make the next keyboard focus select late', () => {
    renderGrid({ lines: [line({ quantity: 5, unitPrice: 30000 })] });
    const price = byId('stock-line-unit-price-0');
    act(() => price.focus());
    fireEvent.mouseDown(price); // the cell already has the focus: no focus event follows
    enter('stock-line-unit-price-0', true); // Shift+Enter: back to SL
    expect(focused()).toBe('stock-line-quantity-0');
    expect(price.value).toBe('30.000'); // idle again: formatted
    enter('stock-line-quantity-0'); // Enter: Đơn giá again, by keyboard, no frame awaited
    expect(focused()).toBe('stock-line-unit-price-0');
    expect(price.value).toBe('30000');
    expect([price.selectionStart, price.selectionEnd]).toEqual([0, 5]);
  });

  // The mouse path selects one frame after the focus event, and by then the focus may be somewhere else. Chrome's
  // select() also focuses the element, so without the "still the active element" check of that late select the
  // cell the user has already left would take the focus back.
  it('of two number cells clicked within one frame, only the one that still has the focus is selected', async () => {
    renderGrid({ lines: [line({ quantity: 5 }), line({ _uiKey: 'b', quantity: 7 })] });
    const select = vi.spyOn(HTMLInputElement.prototype, 'select');
    try {
      const first = byId('stock-line-quantity-0');
      const second = byId('stock-line-quantity-1');
      fireEvent.mouseDown(first);
      act(() => first.focus());
      fireEvent.mouseDown(second);
      act(() => second.focus());
      // Both arrived by mouse: neither is selected yet, both selects wait for the frame.
      expect(select).not.toHaveBeenCalled();
      await nextFrame();
      expect(select.mock.contexts).toEqual([second]);
    } finally {
      select.mockRestore();
    }
  });

  // %CK, %VAT, Số tấm, Dài, Rộng and Dày are type=number inputs: select() works on them but selectionStart does
  // not, so these tests watch select(). A value that is not selected gets the next digit appended (5 -> 57).
  describe('type=number cells', () => {
    const cubic = (overrides: Partial<StockLineFormValues> = {}) =>
      line({
        pricingMode: 'PerCubicMeter',
        sheetCount: 2,
        length: 3000,
        width: 100,
        thickness: 50,
        discountRate: 5,
        vatRate: 8,
        ...overrides,
      });

    // The six type=number cells: %CK and %VAT are one component, the four others another one.
    const cells: [label: string, field: string][] = [
      ['%CK', 'discount-rate'],
      ['%VAT', 'vat-rate'],
      ['Số tấm', 'sheet-count'],
      ['Dài', 'length'],
      ['Rộng', 'width'],
      ['Dày', 'thickness'],
    ];

    // The elements select() was called on by the time `action` returned. fireEvent runs in act(), which also renders
    // what the event changed, so this covers a select in the focus event and one in that render; no frame or timer runs.
    function selectedBy(action: () => void): unknown[] {
      const select = vi.spyOn(HTMLInputElement.prototype, 'select');
      try {
        action();
        return [...select.mock.contexts];
      } finally {
        select.mockRestore();
      }
    }

    it.each(cells)('ArrowDown into %s selects its value with no frame or timer', (_label, field) => {
      renderGrid({ lines: [cubic(), cubic({ _uiKey: 'b' })] });
      const from = byId(`stock-line-${field}-0`);
      act(() => from.focus());

      const selected = selectedBy(() => fireEvent.keyDown(from, { key: 'ArrowDown' }));

      expect(focused()).toBe(`stock-line-${field}-1`);
      expect(selected).toEqual([byId(`stock-line-${field}-1`)]);
    });

    it('Enter into Số tấm selects its value with no frame or timer', () => {
      renderGrid({ lines: [line({ pricingMode: 'PerSquareMeter', sheetCount: 2, length: 3000, width: 1200 })] });
      act(() => byId('stock-line-product-code-0').focus());

      const selected = selectedBy(() => enter('stock-line-product-code-0'));

      expect(focused()).toBe('stock-line-sheet-count-0');
      expect(selected).toEqual([byId('stock-line-sheet-count-0')]);
    });

    it('Enter into %CK selects its value with no frame or timer', () => {
      storePrefs({ hiddenColumns: [], enterStops: { PerUnit: ['quantity', 'unit-price', 'discount-rate'] } });
      renderGrid({ lines: [line({ pricingMode: 'PerUnit', discountRate: 5 })] });
      act(() => byId('stock-line-unit-price-0').focus());

      const selected = selectedBy(() => enter('stock-line-unit-price-0'));

      expect(focused()).toBe('stock-line-discount-rate-0');
      expect(selected).toEqual([byId('stock-line-discount-rate-0')]);
    });

    it('Ctrl+Delete lands on the same cell of the next line with its value selected', async () => {
      const { values } = renderGrid({
        lines: [cubic({ productCode: 'A' }), cubic({ _uiKey: 'b', productCode: 'B', length: 2500 })],
      });
      const from = byId('stock-line-length-0');
      const survivor = byId('stock-line-length-1');
      act(() => from.focus());

      const selected = selectedBy(() => fireEvent.keyDown(from, { key: 'Delete', ctrlKey: true }));

      expect(selected).toEqual([survivor]);
      await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['B']));
      expect(document.activeElement).toBe(survivor);
    });

    // jsdom has no Tab navigation: like Tab, a bare focus() arrives without a mouse press on the cell.
    it.each([
      ['%VAT', 'vat-rate'],
      ['Rộng', 'width'],
    ])('a focus on %s that no mouse press brought (Tab) selects its value', (_label, field) => {
      renderGrid({ lines: [cubic()] });
      const cell = byId(`stock-line-${field}-0`);

      expect(selectedBy(() => act(() => cell.focus()))).toEqual([cell]);
    });

    // Unlike Đơn giá / SL, these cells select nothing after a mouse press, in the focus event or later, so the caret
    // stays where the browser put it. jsdom cannot show the caret; the test shows that no select() follows.
    it.each(cells)('a mouse click on %s selects nothing, in the focus event or a frame later', async (_label, field) => {
      renderGrid({ lines: [cubic()] });
      const select = vi.spyOn(HTMLInputElement.prototype, 'select');
      try {
        const cell = byId(`stock-line-${field}-0`);
        fireEvent.mouseDown(cell);
        act(() => cell.focus());
        fireEvent.mouseUp(cell);
        fireEvent.click(cell);
        await new Promise((resolve) => requestAnimationFrame(() => resolve(null)));
        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(select).not.toHaveBeenCalled();
      } finally {
        select.mockRestore();
      }
    });

    // A press on the cell that already has the focus is followed by no focus event; it must not be taken for the
    // press of a later visit.
    it('a second click in the focused cell does not stop the next keyboard visit from selecting', () => {
      renderGrid({ lines: [cubic(), cubic({ _uiKey: 'b' })] });
      const cell = byId('stock-line-sheet-count-0');
      fireEvent.mouseDown(cell);
      act(() => cell.focus());
      fireEvent.mouseDown(cell);
      fireEvent.keyDown(cell, { key: 'ArrowDown' });
      expect(focused()).toBe('stock-line-sheet-count-1');

      const selected = selectedBy(() => fireEvent.keyDown(byId('stock-line-sheet-count-1'), { key: 'ArrowUp' }));

      expect(focused()).toBe('stock-line-sheet-count-0');
      expect(selected).toEqual([cell]);
    });

    // These cells select in the focus event and nowhere else. Typing renders the cell: a select on those renders
    // would make each digit replace the one typed before it.
    it.each([
      ['%CK', 'discount-rate'],
      ['Số tấm', 'sheet-count'],
    ])('typing in %s after a keyboard focus does not select its value again', (_label, field) => {
      renderGrid({ lines: [cubic(), cubic({ _uiKey: 'b' })] });
      const from = byId(`stock-line-${field}-0`);
      act(() => from.focus());
      const select = vi.spyOn(HTMLInputElement.prototype, 'select');
      try {
        fireEvent.keyDown(from, { key: 'ArrowDown' });
        const cell = byId(`stock-line-${field}-1`);
        expect(select.mock.contexts).toEqual([cell]);

        fireEvent.change(cell, { target: { value: '4' } });
        fireEvent.change(cell, { target: { value: '45' } });

        expect(select.mock.contexts).toEqual([cell]);
        expect(cell.value).toBe('45');
      } finally {
        select.mockRestore();
      }
    });

    // Alt-tab away and back: the window's blur leaves the cell the page's active element, and the focus the window
    // gives back when it returns comes with no mouse press. It is no keyboard visit: what the user was typing stays,
    // and the next digit goes after it (12 -> 125), not over it.
    it.each(cells)('the focus %s gets back when the window returns does not select what was typed', (_label, field) => {
      const { values } = renderGrid({ lines: [cubic(), cubic({ _uiKey: 'b' })] });
      const from = byId(`stock-line-${field}-0`);
      act(() => from.focus());
      fireEvent.keyDown(from, { key: 'ArrowDown' });
      const cell = byId(`stock-line-${field}-1`);
      fireEvent.change(cell, { target: { value: '12' } });

      // The window loses the focus: blur (and focusout, which fireEvent adds) on the cell, which stays the active
      // element. It comes back: focus and focusin.
      fireEvent.blur(cell);
      expect(document.activeElement).toBe(cell);
      const selected = selectedBy(() => fireEvent.focus(cell));
      fireEvent.change(cell, { target: { value: '125' } });

      expect(selected).toEqual([]);
      const key = { 'discount-rate': 'discountRate', 'vat-rate': 'vatRate', 'sheet-count': 'sheetCount' }[field] ?? field;
      expect(values().lines[1][key as keyof StockLineFormValues]).toBe(125);
    });

    // The window came back with a click on another cell, and this one never got its focus back: the next visit, which
    // comes from another element, is a keyboard visit like any other.
    it('a keyboard visit from another cell after the window went away selects', () => {
      renderGrid({ lines: [cubic(), cubic({ _uiKey: 'b' })] });
      const cell = byId('stock-line-discount-rate-0');
      const other = byId('stock-line-discount-rate-1');
      act(() => cell.focus());
      fireEvent.blur(cell);

      expect(selectedBy(() => fireEvent.focus(cell, { relatedTarget: other }))).toEqual([cell]);
    });

    // The window's return is used up by that one focus: the next keyboard visit selects as usual.
    it('a keyboard visit after the window has returned selects again', () => {
      renderGrid({ lines: [cubic(), cubic({ _uiKey: 'b' })] });
      const cell = byId('stock-line-discount-rate-0');
      act(() => cell.focus());
      fireEvent.blur(cell);
      fireEvent.focus(cell);
      fireEvent.keyDown(cell, { key: 'ArrowDown' });

      const selected = selectedBy(() => fireEvent.keyDown(byId('stock-line-discount-rate-1'), { key: 'ArrowUp' }));

      expect(focused()).toBe('stock-line-discount-rate-0');
      expect(selected).toEqual([cell]);
    });
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
      if ((child as unknown as Element).tagName === 'TR') resting.push(document.activeElement);
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

  it('a key pressed right after Ctrl+Delete is kept: nothing re-focuses the old row later', async () => {
    const { values } = renderGrid({
      lines: [line({ productCode: 'A' }), line({ _uiKey: 'b', productCode: 'B' }), line({ _uiKey: 'c', productCode: 'C' })],
    });
    fireEvent.keyDown(byId('stock-line-quantity-0'), { key: 'Delete', ctrlKey: true });
    // No wait: the user goes one row down at once (from B to C).
    fireEvent.keyDown(document.activeElement!, { key: 'ArrowDown' });
    const onC = document.activeElement;
    await waitFor(() => expect(values().lines.map((l) => l.productCode)).toEqual(['B', 'C']));
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(document.activeElement).toBe(onC);
    expect(focused()).toBe('stock-line-quantity-1');
  });

  // In the browser React renders what a key event changed in a microtask, but react-hook-form hands the result of
  // remove() to the watched lines only from an effect, and that render comes in a later task: the next key can arrive
  // in between. Outside act(), as in the browser, these tests type that key into the focused cell at that moment.
  describe('a key typed at once after a line is deleted', () => {
    const threeLines = () => [
      line({ _uiKey: 'a', productCode: 'A', pricingMode: 'PerCubicMeter', sheetCount: 2, length: 4002, width: 100, thickness: 50, discountRate: 5, note: 'ghi chu 1' }),
      line({ _uiKey: 'b', productCode: 'B', pricingMode: 'PerCubicMeter', sheetCount: 3, length: 4003, width: 120, thickness: 60, discountRate: 6, note: 'ghi chu 2' }),
      line({ _uiKey: 'c', productCode: 'C', pricingMode: 'PerCubicMeter', sheetCount: 4, length: 4004, width: 140, thickness: 70, discountRate: 7, note: 'ghi chu 3' }),
    ];
    const ownValues = (l: StockLineFormValues) => ({
      productCode: l.productCode,
      sheetCount: l.sheetCount,
      length: l.length,
      width: l.width,
      thickness: l.thickness,
      discountRate: l.discountRate,
      note: l.note,
    });

    // A key typed into the focused cell, built from the text the cell shows: it ends up after that text.
    function typeKey(key: string) {
      const cell = document.activeElement as HTMLInputElement;
      const setValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
      setValue.call(cell, cell.value + key);
      cell.dispatchEvent(new Event('input', { bubbles: true }));
    }

    it.each([
      ['%CK', 'discount-rate', 'discountRate', '9', 69],
      ['Dài', 'length', 'length', '9', 40039],
      ['Ghi chú', 'note', 'note', 'x', 'ghi chu 2x'],
    ] as const)('Ctrl+Delete on %s: the key starts from the value of the line that moved up', async (_label, cell, field, key, typed) => {
      showAllColumns();
      const lines = threeLines();
      const { values } = renderGrid({ lines });
      const from = byId(`stock-line-${cell}-0`);
      act(() => from.focus());

      await outsideAct(async () => {
        from.dispatchEvent(new KeyboardEvent('keydown', { key: 'Delete', ctrlKey: true, bubbles: true, cancelable: true }));
        await eventRendered();
        typeKey(key);
      });

      expect(document.activeElement).toBe(byId(`stock-line-${cell}-0`));
      expect(values().lines.map(ownValues)).toEqual([{ ...ownValues(lines[1]), [field]: typed }, ownValues(lines[2])]);
    });

    it('the delete button: the key starts from the code of the line that moved up', async () => {
      const lines = threeLines();
      const { values } = renderGrid({ lines });

      await outsideAct(async () => {
        screen.getByRole('button', { name: 'Xóa dòng 1' }).dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        await eventRendered();
        typeKey('x');
      });

      expect(document.activeElement).toBe(byId('stock-line-product-code-0'));
      expect(values().lines.map(ownValues)).toEqual([{ ...ownValues(lines[1]), productCode: 'Bx' }, ownValues(lines[2])]);
    });
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

  it('renders its toolbar into the given slot (on the tab row) instead of above the grid', () => {
    const slot = document.createElement('div');
    document.body.appendChild(slot);
    const formRef: { current: UseFormReturn<StockVoucherFormValues, unknown, StockVoucherFormParsed> | null } = { current: null };
    function Harness() {
      const form = useForm<StockVoucherFormValues, unknown, StockVoucherFormParsed>({
        defaultValues: { ...toFormDefaults(), warehouseId: WH1, lines: [line()] },
      });
      formRef.current = form;
      return <StockLineGrid form={form} type="In" warehouses={warehouses} computed={[]} stockAt={{}} readOnly={false} toolbarSlot={slot} />;
    }
    render(<Harness />);
    expect(within(slot).getByRole('button', { name: /Thêm dòng/ })).toBeInTheDocument();
    expect(within(slot).getByRole('button', { name: /Phím tắt/ })).toBeInTheDocument();
    expect(within(slot).getByRole('button', { name: /Cột|Cài đặt|tùy chọn/i })).toBeInTheDocument();
    expect(document.querySelectorAll('.stock-grid-toolbar')).toHaveLength(1);
    expect(slot.querySelector('.stock-grid-toolbar')).not.toBeNull();
    slot.remove();
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

  describe('product quick add', () => {
    function typeNewCode() {
      useProductSearchMock.mockImplementation((text: string) => ({
        data: [],
        isLoading: false,
        isError: false,
        isFetching: false,
        settledQuery: text,
      }));
      renderGrid({ type: 'In' });
      const input = byId('stock-line-product-code-0');
      act(() => input.focus());
      fireEvent.change(input, { target: { value: 'TL-50' } });
      return input;
    }

    it('adds a product from the code cell and lands on the quantity cell like a pick', async () => {
      const input = typeNewCode();
      await screen.findByRole('option', { name: /Thêm mới hàng hóa "TL-50"/ });

      fireEvent.keyDown(input, { key: 'Enter' });
      await screen.findByRole('dialog', { name: 'Thêm nhanh hàng hóa' });
      fireEvent.click(screen.getByRole('button', { name: 'fake lưu' }));

      await waitFor(() => expect(screen.getByText(quickAdded.name)).toBeInTheDocument());
      await waitFor(() => expect(byId('stock-line-quantity-0')).toHaveFocus());
    });

    it('shows no add row without products.create', async () => {
      permissions = [];
      typeNewCode();

      await screen.findByRole('listbox');
      expect(screen.queryByText(/Thêm mới hàng hóa/)).toBeNull();
    });
  });
});
