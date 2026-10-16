import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { createEmptyLine, LineItemsGrid } from './line-items-grid';
import type {
  QuotationFormParsed,
  QuotationFormValues,
  QuotationLineFormValues,
} from '@/features/quotations/schema';
import type { ProductSuggestion } from '@/features/products/types';
import { useAuthStore } from '@/stores/auth-store';

// `last`: the props of the last product cell rendered. `byId`: the props of each product cell, by its input id.
const cellProps = vi.hoisted(() => ({
  last: undefined as Record<string, unknown> | undefined,
  byId: {} as Record<string, Record<string, unknown>>,
}));

vi.mock('./product-typeahead-cell', () => ({
  ProductTypeaheadCell: (props: { value: string; onChange: (value: string) => void; inputId?: string }) => {
    cellProps.last = props;
    if (props.inputId) cellProps.byId[props.inputId] = props;
    return (
      <input
        id={props.inputId}
        aria-label="product-code"
        value={props.value}
        onChange={(e) => props.onChange((e.target as HTMLInputElement).value)}
      />
    );
  },
}));

const baseLines: QuotationLineFormValues[] = [
  {
    sortOrder: 0,
    productCode: 'A001',
    productName: 'Alpha',
    unitName: 'pcs',
    pricingMode: 'PerUnit',
    quantity: 2,
    unitPrice: 100,
    unitCost: 60,
  },
  {
    sortOrder: 1,
    productCode: 'B001',
    productName: 'Beta',
    unitName: 'pcs',
    pricingMode: 'PerUnit',
    quantity: 1,
    unitPrice: 50,
    unitCost: 30,
  },
];

// A line with a stable _uiKey, as every production line has (createEmptyLine, toFormDefaults). Without it the <tr>
// key falls back to field.id, which react-hook-form regenerates on every changed nested setValue.
function line(uiKey: string, overrides: Partial<QuotationLineFormValues> = {}): QuotationLineFormValues {
  return {
    _uiKey: uiKey,
    sortOrder: 0,
    productCode: `P-${uiKey}`,
    productName: `Name ${uiKey}`,
    unitName: 'pcs',
    pricingMode: 'PerUnit',
    quantity: 1,
    unitPrice: 100,
    ...overrides,
  };
}

const md = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
  line(uiKey, { pricingMode: 'PerLinearMeter', unitName: 'm', sheetCount: 4, length: 6000, quantity: 0, ...overrides });
// Its thickness is stored (from an earlier product) but unused.
const m2 = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
  line(uiKey, {
    pricingMode: 'PerSquareMeter',
    unitName: 'm²',
    sheetCount: 3,
    length: 2400,
    width: 1200,
    thickness: 5,
    quantity: 0,
    ...overrides,
  });
const m3 = (uiKey: string, overrides: Partial<QuotationLineFormValues> = {}) =>
  line(uiKey, {
    pricingMode: 'PerCubicMeter',
    unitName: 'm³',
    sheetCount: 2,
    length: 3000,
    width: 100,
    thickness: 50,
    quantity: 0,
    ...overrides,
  });

const sheetProduct: ProductSuggestion = {
  id: '33333333-3333-3333-3333-333333333333',
  code: 'TON01',
  name: 'Tôn lạnh',
  unitName: 'm²',
  pricingMode: 'PerSquareMeter',
  defaultPrice: 120_000,
  costPrice: 90_000,
  defaultTaxRate: 8,
  length: 2000,
  width: 1200,
  thickness: undefined,
  trackInventory: true,
  purchaseDiscountRate: 0,
  salesDiscountRate: 0,
  priceIncludesVat: false,
};

function setCostPermission(enabled: boolean) {
  useAuthStore.setState({
    accessToken: 'token',
    expiresAt: '2099-01-01T00:00:00Z',
    user: {
      id: '11111111-1111-1111-1111-111111111111',
      username: 'tester',
      email: 'tester@example.com',
      fullName: 'Tester',
      roles: [],
      permissions: enabled ? ['quotations.view_cost'] : [],
    },
  });
}

function renderGrid(lines: QuotationLineFormValues[] = baseLines) {
  const formRef: { current: UseFormReturn<QuotationFormValues, unknown, QuotationFormParsed> | null } = {
    current: null,
  };
  function Harness() {
    const form = useForm<QuotationFormValues, unknown, QuotationFormParsed>({
      defaultValues: {
        customerId: '11111111-1111-1111-1111-111111111111',
        customerName: '',
        quotationDate: '2026-05-13',
        deliveryAddress: '',
        deliveryRecipient: '',
        deliveryPhone: '',
        deliveryDate: '',
        deliveryNote: '',
        taxRate: 0,
        discount: 0,
        freight: 0,
        internalNote: '',
        lines,
      },
    });
    formRef.current = form;
    return <LineItemsGrid form={form} />;
  }

  const view = render(<Harness />);
  return { ...view, form: () => formRef.current! };
}

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

const focused = () => document.activeElement?.id;

function headers() {
  return screen.getAllByRole('columnheader').map((th) => th.textContent);
}

// Presses Enter (Shift+Enter with `shift`) in a cell, focusing it first.
function enter(id: string, shift = false) {
  const el = byId(id);
  act(() => el.focus());
  fireEvent.keyDown(el, { key: 'Enter', shiftKey: shift });
}

// Picks a product in a row's code cell, as the real typeahead's commit() does.
function pick(rowIndex: number, product: ProductSuggestion) {
  const onSelect = cellProps.byId[`line-product-code-${rowIndex}`]?.onSelect as
    | ((s: ProductSuggestion) => void)
    | undefined;
  if (!onSelect) throw new Error(`no product cell in row ${rowIndex}`);
  act(() => onSelect(product));
}

const DIMENSION_HEADERS = ['Số tấm', 'Dài', 'Rộng', 'Dày'];
const shownDimensionHeaders = () => headers().filter((h) => DIMENSION_HEADERS.includes(h ?? ''));

describe('LineItemsGrid', () => {
  beforeEach(() => {
    setCostPermission(false);
    cellProps.byId = {};
  });

  it('gives each code cell its id and keeps the props of each cell', () => {
    const { form } = renderGrid([line('a'), line('b')]);

    expect(byId('line-product-code-1')).toHaveValue('P-b');
    expect(cellProps.byId['line-product-code-1']?.nextFocusId).toBe('line-name-1');
    expect(form().getValues('lines.1._uiKey')).toBe('b');
  });

  it('turns on product quick add for the code cell and returns to the name cell', () => {
    renderGrid([baseLines[0]]);

    expect(cellProps.last?.allowQuickAdd).toBe(true);
    expect(cellProps.last?.nextFocusId).toBe('line-name-0');
  });

  it('renders existing rows and subtotal footer', () => {
    renderGrid();

    expect(screen.getByDisplayValue('Alpha')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Beta')).toBeInTheDocument();
    expect(screen.getByText(/Tổng thành tiền bán:\s*250\s*₫/)).toBeInTheDocument();
    expect(screen.queryByText(/Tổng thành tiền nhập:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Tổng lợi nhuận:/)).not.toBeInTheDocument();
  });

  it('formats quantity values with comma groups and dot decimals', () => {
    renderGrid([
      { ...baseLines[0], quantity: 2 },
      { ...baseLines[1], quantity: 44545.45 },
    ]);

    expect(screen.getByDisplayValue('2')).toBeInTheDocument();
    expect(screen.getByDisplayValue('44,545.45')).toBeInTheDocument();
  });

  it('keeps dot decimal text while editing quantity', async () => {
    const user = userEvent.setup();
    renderGrid([{ ...baseLines[0], quantity: 1 }]);
    const quantityInput = screen.getByLabelText('Số lượng');

    await user.click(quantityInput);
    await user.clear(quantityInput);
    await user.type(quantityInput, '44545.45');
    expect(quantityInput).toHaveValue('44545.45');

    await user.tab();
    await waitFor(() => {
      expect(screen.getByLabelText('Số lượng')).toHaveValue('44,545.45');
    });
  });

  it('renders sales amount header and hides cost columns without cost permission', () => {
    renderGrid();

    expect(screen.getByRole('columnheader', { name: 'Đơn giá bán' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Thành tiền bán' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Đơn giá nhập' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Thành tiền nhập' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Lợi nhuận' })).not.toBeInTheDocument();
  });

  it('renders cost and profit columns with cost permission', () => {
    setCostPermission(true);
    renderGrid();

    expect(screen.getByRole('columnheader', { name: 'Đơn giá nhập' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Thành tiền nhập' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Lợi nhuận' })).toBeInTheDocument();
    expect(screen.getAllByLabelText('Đơn giá nhập')).toHaveLength(2);
    expect(screen.getByText(/Tổng thành tiền nhập:\s*150\s*₫/)).toBeInTheDocument();
    expect(screen.getByText(/Tổng lợi nhuận:\s*100\s*₫/)).toBeInTheDocument();
  });

  it('adds a row when clicking add line', async () => {
    renderGrid([baseLines[0]]);

    fireEvent.click(screen.getByRole('button', { name: /thêm dòng/i }));

    await waitFor(() => {
      expect(screen.getAllByLabelText('product-code')).toHaveLength(2);
    });
  });

  it('removes a single row with row trash button', async () => {
    renderGrid();

    fireEvent.click(screen.getAllByRole('button', { name: /xóa dòng/i })[0]);

    await waitFor(() => {
      expect(screen.queryByDisplayValue('Alpha')).not.toBeInTheDocument();
      expect(screen.getByDisplayValue('Beta')).toBeInTheDocument();
    });
  });

  it('clears all rows after confirm dialog confirmation', async () => {
    renderGrid();

    fireEvent.click(screen.getByRole('button', { name: /xóa tất cả dòng/i }));
    expect(screen.getByText('Xóa tất cả dòng?')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Xóa' }));

    await waitFor(() => {
      expect(screen.getByText(/chưa có dòng nào/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /xóa tất cả dòng/i })).toBeDisabled();
    });
  });

  it('an Enter that commits an IME composition does not move the focus', () => {
    renderGrid();
    const unit = document.getElementById('line-unit-0') as HTMLElement;
    unit.focus();
    fireEvent.keyDown(unit, { key: 'Enter', isComposing: true, keyCode: 229 });
    expect(document.activeElement).toBe(unit);
    fireEvent.keyDown(unit, { key: 'Enter' });
    expect(document.activeElement).not.toBe(unit);
  });

  it('removes active row with Ctrl+Delete', async () => {
    renderGrid();
    const betaInput = screen.getByDisplayValue('Beta');

    fireEvent.focus(betaInput);
    fireEvent.keyDown(betaInput, { key: 'Delete', ctrlKey: true });

    await waitFor(() => {
      expect(screen.getByDisplayValue('Alpha')).toBeInTheDocument();
      expect(screen.queryByDisplayValue('Beta')).not.toBeInTheDocument();
    });
  });

  it('shows the dimensions as four columns and walks them from Số tấm to Dày', () => {
    renderGrid([m3('a')]);

    expect(headers()).toEqual([
      '#', 'Mã hàng', 'Tên hàng', 'ĐVT', 'Số tấm', 'Dài', 'Rộng', 'Dày', 'SL', 'Đơn giá bán', 'Thành tiền bán', '',
    ]);
    for (const id of ['line-sheet-count-0', 'line-length-0', 'line-width-0', 'line-thickness-0']) {
      expect(byId(id)).toHaveClass('cell-input', 'cell-number', 'tabular-nums');
      expect(byId(id).closest('td')).toHaveClass('cell-number');
      expect(byId(id)).not.toHaveAttribute('placeholder');
    }

    enter('line-unit-0');
    expect(focused()).toBe('line-sheet-count-0');
    enter('line-sheet-count-0');
    expect(focused()).toBe('line-length-0');
    enter('line-length-0');
    expect(focused()).toBe('line-width-0');
    enter('line-width-0');
    expect(focused()).toBe('line-thickness-0');
    enter('line-thickness-0');
    expect(focused()).toBe('line-unit-price-0');
  });

  describe('dimension columns follow the highest pricing mode', () => {
    it('shows none for a blank line or unit lines, also while a code is typed', () => {
      renderGrid([createEmptyLine(0), line('b')]);
      // The exact row: main has only the one combined dimension header, none of the four.
      expect(headers()).toEqual(['#', 'Mã hàng', 'Tên hàng', 'ĐVT', 'SL', 'Đơn giá bán', 'Thành tiền bán', '']);
      for (const id of ['line-sheet-count-0', 'line-length-0', 'line-width-0', 'line-thickness-0']) {
        expect(document.getElementById(id)).toBeNull();
      }

      fireEvent.change(byId('line-product-code-1'), { target: { value: 'TON' } });
      expect(shownDimensionHeaders()).toEqual([]);
    });

    it('shows Số tấm, Dài and Rộng for an md line next to an m² line', () => {
      renderGrid([md('a'), m2('b')]);
      expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
    });

    it('spans the empty row over the shown columns', () => {
      renderGrid([]);
      expect(screen.getByText(/Chưa có dòng nào/).closest('td')).toHaveAttribute('colspan', '8');
    });

    it('spans the empty row over the cost columns too with quotations.view_cost', () => {
      setCostPermission(true);
      renderGrid([]);
      expect(screen.getByText(/Chưa có dòng nào/).closest('td')).toHaveAttribute('colspan', '11');
    });

    it.each([
      ['unit lines', false, [line('a')], '780px'],
      ['unit lines with view_cost', true, [line('a')], '1136px'],
      ['an m³ line', false, [m3('a')], '1066px'],
      ['an m³ line with view_cost', true, [m3('a')], '1422px'],
    ])('sizes the table to its columns plus 200px for Tên hàng: %s', (_name, cost, lines, minWidth) => {
      setCostPermission(cost);
      const { container } = renderGrid(lines);
      expect((container.querySelector('table.accounting-grid') as HTMLTableElement).style.minWidth).toBe(minWidth);
    });

    it('shows the m² columns at once when a unit-only quotation picks an m² product, and Enter walks them', () => {
      renderGrid([line('a')]);
      expect(shownDimensionHeaders()).toEqual([]);

      pick(0, sheetProduct);

      expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
      enter('line-name-0');
      expect(focused()).toBe('line-unit-0');
      enter('line-unit-0');
      expect(focused()).toBe('line-sheet-count-0');
      enter('line-sheet-count-0');
      expect(focused()).toBe('line-length-0');
      enter('line-length-0');
      expect(focused()).toBe('line-width-0');
      enter('line-width-0');
      expect(focused()).toBe('line-unit-price-0');
    });

    it('hides Dày at once when the only m³ line is re-picked as an m² product, and keeps its value', () => {
      const { form } = renderGrid([m3('a')]);
      fireEvent.change(byId('line-thickness-0'), { target: { value: '8' } });
      fireEvent.change(byId('line-product-code-0'), { target: { value: 'TON' } });
      expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng', 'Dày']);

      pick(0, sheetProduct);

      expect(shownDimensionHeaders()).toEqual(['Số tấm', 'Dài', 'Rộng']);
      expect(document.getElementById('line-thickness-0')).toBeNull();
      expect(form().getValues('lines.0.thickness')).toBe(8);
    });
  });

  it('shows "—" in a cell the row does not use, keeps its id and keeps the stored value', () => {
    const { form } = renderGrid([
      m3('a'),
      m2('b'),
      line('c', { sheetCount: 1, length: 2000, width: 1000, thickness: 3 }),
    ]);

    for (const id of ['line-thickness-1', 'line-sheet-count-2', 'line-length-2', 'line-width-2', 'line-thickness-2']) {
      expect(byId(id)).toBeDisabled();
      expect(byId(id)).toHaveValue(null);
      expect(byId(id)).toHaveAttribute('placeholder', '—');
    }
    expect(byId('line-thickness-0')).toHaveValue(50);
    expect(byId('line-thickness-0')).not.toHaveAttribute('placeholder');
    expect(byId('line-width-1')).toHaveValue(1200);
    expect(form().getValues('lines.1.thickness')).toBe(5);
    expect(form().getValues('lines.2')).toMatchObject({ sheetCount: 1, length: 2000, width: 1000, thickness: 3 });
  });

  // An old draft or corrupt data: a missing or unknown mode counts as PerUnit (cells and Enter path).
  it.each([
    ['without a pricing mode', undefined],
    ['with an unknown pricing mode', 'Legacy'],
  ])('treats a line %s as a unit line', (_name, mode) => {
    renderGrid([m3('a'), { ...line('b', { sheetCount: 2, length: 3000 }), pricingMode: mode } as QuotationLineFormValues]);

    for (const f of ['sheet-count', 'length', 'width', 'thickness']) {
      expect(byId(`line-${f}-1`)).toBeDisabled();
      expect(byId(`line-${f}-1`)).toHaveAttribute('placeholder', '—');
    }
    expect(byId('line-quantity-1')).toBeEnabled();
    enter('line-unit-1');
    expect(focused()).toBe('line-quantity-1');
  });

  describe('Enter without quotations.view_cost (no Đơn giá nhập cell)', () => {
    it("goes from Đơn giá bán to the next row's Mã hàng", () => {
      renderGrid([line('a'), line('b')]);
      enter('line-unit-price-0');
      expect(focused()).toBe('line-product-code-1');
    });

    it("adds a row from the last row's Đơn giá bán and focuses its Mã hàng", async () => {
      renderGrid([line('a'), line('b')]);
      enter('line-unit-price-1');
      await waitFor(() => expect(focused()).toBe('line-product-code-2'));
    });

    it("goes back from Mã hàng to the previous row's Đơn giá bán with Shift+Enter", () => {
      renderGrid([line('a'), line('b')]);
      enter('line-product-code-1', true);
      expect(focused()).toBe('line-unit-price-0');
    });
  });

  it("goes from Đơn giá nhập to the next row's Mã hàng with quotations.view_cost", () => {
    setCostPermission(true);
    renderGrid([line('a'), line('b')]);
    enter('line-unit-cost-0');
    expect(focused()).toBe('line-product-code-1');
  });
});
