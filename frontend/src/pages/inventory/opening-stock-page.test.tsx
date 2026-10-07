import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OpeningStockPage } from './opening-stock-page';
import type { OpeningStockGrid } from '@/features/opening-stock/types';
import type { ProductListItem, ProductSuggestion } from '@/features/products/types';

const toastMock = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...args: unknown[]) => toastMock(...args) }));

const WH1 = '11111111-1111-1111-1111-111111111111';
const WH2 = '22222222-2222-2222-2222-222222222222';
const WH_OFF = '66666666-6666-6666-6666-666666666666';
const P1 = '33333333-3333-3333-3333-333333333333';
const P2 = '44444444-4444-4444-4444-444444444444';
const P3 = '55555555-5555-5555-5555-555555555555';

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({
    data: [
      // Inactive first: it must be neither auto-picked nor offered.
      { id: WH_OFF, code: 'KHO00', name: 'Kho cũ', branchId: 'b', branchCode: 'CN01', branchName: 'CN 1', isActive: false },
      { id: WH1, code: 'KHO01', name: 'Kho chính', branchId: 'b', branchCode: 'CN01', branchName: 'CN 1', isActive: true },
      { id: WH2, code: 'KHO02', name: 'Kho phụ', branchId: 'b', branchCode: 'CN01', branchName: 'CN 1', isActive: true },
    ],
  }),
}));

// Per-warehouse query state; a missing entry means the GET is still in flight.
const grids: Record<string, { data?: OpeningStockGrid; error?: unknown }> = {};
const saveMock = vi.fn();
vi.mock('@/features/opening-stock/hooks', () => ({
  useOpeningStock: (warehouseId?: string) => {
    const state = warehouseId ? grids[warehouseId] : undefined;
    return {
      data: state?.data,
      isLoading: !state,
      isError: state?.error !== undefined,
      error: state?.error,
    };
  },
  useSaveOpeningStock: () => ({ mutateAsync: saveMock, isPending: false }),
}));

const suggestion = (over: Partial<ProductSuggestion>): ProductSuggestion => ({
  id: P2,
  code: 'GO01',
  name: 'Gỗ thông',
  unitName: 'Thanh',
  pricingMode: 'PerCubicMeter',
  defaultPrice: 1,
  costPrice: 1,
  defaultTaxRate: 0,
  length: undefined,
  width: undefined,
  thickness: undefined,
  trackInventory: true,
  purchaseDiscountRate: 0,
  salesDiscountRate: 0,
  priceIncludesVat: false,
  ...over,
});
const searchItems: { data: ProductSuggestion[] } = { data: [] };
const catalogItems: { items: ProductListItem[] } = { items: [] };
vi.mock('@/features/products/hooks', () => ({
  useProductSearch: () => ({ data: searchItems.data, isLoading: false, isError: false }),
  useProducts: () => ({
    data: { items: catalogItems.items, page: 1, pageSize: 20, totalItems: catalogItems.items.length, totalPages: 1 },
    isLoading: false,
  }),
  useProductGroups: () => ({ data: [] }),
  useProduct: () => ({ data: undefined, isLoading: false }),
}));

function apiError(status: number, code: string, details?: Record<string, string[]>) {
  return {
    isAxiosError: true,
    message: code,
    response: { status, data: { success: false, error: { code, message: code, details } } },
  };
}

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

const GRID: OpeningStockGrid = {
  warehouseId: WH1,
  openingDate: '2026-09-01',
  lines: [{ productId: P1, productCode: 'TON01', productName: 'Tôn lạnh', unitName: 'm²', quantity: 10, amount: 1_000_000 }],
};

describe('OpeningStockPage', () => {
  beforeEach(() => {
    toastMock.mockReset();
    saveMock.mockReset();
    for (const key of Object.keys(grids)) delete grids[key];
    grids[WH1] = { data: GRID };
    searchItems.data = [];
    catalogItems.items = [];
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('loads the grid, adds a tracked product, rejects an untracked one and saves the payload', async () => {
    saveMock.mockResolvedValue(GRID);
    render(<OpeningStockPage />);

    expect(byId('opening-date')).toHaveValue('2026-09-01');
    expect(byId('opening-product-code-0')).toHaveValue('TON01');
    expect(screen.getByText('100.000')).toBeInTheDocument(); // read-only unit value = amount / quantity (vi-VN)

    fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));
    searchItems.data = [
      suggestion({ id: P3, code: 'DV01', name: 'Dịch vụ', unitName: 'Lần', pricingMode: 'PerUnit', trackInventory: false }),
      suggestion({}),
    ];
    fireEvent.change(byId('opening-product-code-1'), { target: { value: 'D' } });
    fireEvent.mouseDown(await screen.findByText('Dịch vụ'));
    expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ title: 'Hàng không theo dõi tồn kho' }));
    expect(byId('opening-product-code-1')).toHaveValue('D');

    fireEvent.change(byId('opening-product-code-1'), { target: { value: 'G' } });
    fireEvent.mouseDown(await screen.findByText('Gỗ thông'));
    await waitFor(() => expect(byId('opening-product-code-1')).toHaveValue('GO01'));
    expect(screen.getByText('m³')).toBeInTheDocument();
    fireEvent.change(byId('opening-quantity-1'), { target: { value: '2.5' } });
    fireEvent.change(byId('opening-amount-1'), { target: { value: '500.000' } }); // vi-VN grouping

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock).toHaveBeenCalledWith({
      warehouseId: WH1,
      openingDate: '2026-09-01',
      acknowledgeNegativeStock: false,
      lines: [
        { productId: P1, quantity: 10, amount: 1_000_000 },
        { productId: P2, quantity: 2.5, amount: 500_000 },
      ],
    });
  });

  it('rejects an untracked product picked from the catalog dialog', async () => {
    catalogItems.items = [
      { id: P3, code: 'DV01', name: 'Dịch vụ', unitName: 'Lần', pricingMode: 'PerUnit', trackInventory: false } as ProductListItem,
    ];
    render(<OpeningStockPage />);
    fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));

    fireEvent.change(byId('opening-product-code-1'), { target: { value: 'DV' } });
    fireEvent.mouseDown(await screen.findByText('Xem danh mục đầy đủ'));
    fireEvent.doubleClick(await screen.findByText('Dịch vụ'));

    await waitFor(() =>
      expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ title: 'Hàng không theo dõi tồn kho' })),
    );
    expect(byId('opening-product-code-1')).not.toHaveValue('DV01');
  });

  it('a product already in the grid is rejected', async () => {
    render(<OpeningStockPage />);
    fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));
    searchItems.data = [suggestion({ id: P1, code: 'TON01', name: 'Tôn lạnh 2', pricingMode: 'PerSquareMeter' })];

    fireEvent.change(byId('opening-product-code-1'), { target: { value: 'TON' } });
    fireEvent.mouseDown(await screen.findByText('Tôn lạnh 2'));

    expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ title: 'Hàng đã có trong danh sách' }));
  });

  it('default opening date is the first day of the current VN month', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 1, 6, 0)); // 2026-10-01 06:00 local (VN) = 2026-09-30 23:00 UTC
    grids[WH1] = { data: { warehouseId: WH1, openingDate: null, lines: [] } };

    render(<OpeningStockPage />);

    expect(byId('opening-date')).toHaveValue('2026-10-01');
  });

  it('negative stock warning resends with acknowledgement', async () => {
    saveMock
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', { 'TON01@KHO01': ['Âm 5 m² tại 05/10/2026 08:00'] }))
      .mockResolvedValueOnce(GRID);
    render(<OpeningStockPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    // The dialog is shared with the voucher form; its title is not asserted here.
    expect(await screen.findByText('TON01 @ KHO01')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(2));
    expect(saveMock.mock.calls[1][0]).toMatchObject({ acknowledgeNegativeStock: true });
    await waitFor(() => expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' })));
  });

  it('switching to a warehouse still loading hides the previous lines and disables Lưu', async () => {
    const user = userEvent.setup();
    saveMock.mockResolvedValue(GRID);
    const { rerender } = render(<OpeningStockPage />);
    expect(byId('opening-product-code-0')).toHaveValue('TON01');

    await user.click(screen.getByRole('combobox', { name: 'Kho' }));
    expect(screen.queryByRole('option', { name: /KHO00/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', { name: 'KHO02 — Kho phụ' }));

    expect(screen.queryByDisplayValue('TON01')).not.toBeInTheDocument();
    expect(screen.getByText('Đang tải…')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Thêm dòng/ })).toBeDisabled();

    grids[WH2] = {
      data: {
        warehouseId: WH2,
        openingDate: '2026-08-01',
        lines: [{ productId: P2, productCode: 'GO01', productName: 'Gỗ thông', unitName: 'm³', quantity: 1, amount: 200 }],
      },
    };
    rerender(<OpeningStockPage />);

    await waitFor(() => expect(byId('opening-product-code-0')).toHaveValue('GO01'));
    expect(byId('opening-date')).toHaveValue('2026-08-01');
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock.mock.calls[0][0]).toMatchObject({
      warehouseId: WH2,
      lines: [{ productId: P2, quantity: 1, amount: 200 }],
    });
  });

  it('a failed load of the new warehouse shows the error and keeps Lưu disabled', async () => {
    const user = userEvent.setup();
    grids[WH2] = { error: new Error('Không tải được tồn đầu kỳ') };
    render(<OpeningStockPage />);

    await user.click(screen.getByRole('combobox', { name: 'Kho' }));
    await user.click(screen.getByRole('option', { name: 'KHO02 — Kho phụ' }));

    expect(screen.getByText('Không tải được tồn đầu kỳ')).toBeInTheDocument();
    expect(screen.queryByDisplayValue('TON01')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled();
  });

  it('amounts accept vi-VN grouping and unparsable values block the save', async () => {
    saveMock.mockResolvedValue(GRID);
    render(<OpeningStockPage />);

    fireEvent.change(byId('opening-amount-0'), { target: { value: '1.500.000' } });
    expect(byId('opening-amount-0')).toHaveValue('1.500.000');
    expect(screen.getByText('150.000')).toBeInTheDocument(); // unit value 1,500,000 / 10

    fireEvent.change(byId('opening-quantity-0'), { target: { value: '1,5' } });
    expect(screen.getByText('Số lượng không hợp lệ')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    expect(saveMock).not.toHaveBeenCalled();
    expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ title: 'Số liệu không hợp lệ' }));

    fireEvent.change(byId('opening-quantity-0'), { target: { value: '12.5' } });
    expect(screen.queryByText('Số lượng không hợp lệ')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(1));
    expect(saveMock.mock.calls[0][0]).toMatchObject({
      warehouseId: WH1,
      lines: [{ productId: P1, quantity: 12.5, amount: 1_500_000 }],
    });
  });
});
