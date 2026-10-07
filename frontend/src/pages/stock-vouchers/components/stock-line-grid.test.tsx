import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { StockLineGrid } from './stock-line-grid';
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
  return () => formRef.current!.getValues();
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
    useProductSearchMock.mockReset();
    useProductSearchMock.mockReturnValue({ data: [sheet], isLoading: false, isError: false });
    useProductsMock.mockReset();
    useProductsMock.mockReturnValue({ data: { items: [listItem], totalPages: 1 }, isLoading: false });
  });

  it('selecting a product on a stock-in fills cost price, purchase discount, VAT and dimensions', async () => {
    const values = renderGrid({ type: 'In' });
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
    const values = renderGrid({ type: 'Out' });
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
    const values = renderGrid({
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
    const values = renderGrid({ type: 'In' });
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
    const values = renderGrid({ lines: [line({ vatRate: 10 })] });
    const vat = byId('stock-line-vat-rate-0');

    fireEvent.change(vat, { target: { value: '' } });
    expect(vat).toHaveValue(null);
    expect(values().lines[0].vatRate).toBe(0);

    fireEvent.blur(vat);
    expect(vat).toHaveValue(0);
  });
});
