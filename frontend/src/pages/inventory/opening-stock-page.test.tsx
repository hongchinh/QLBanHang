import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { OpeningStockPage } from './opening-stock-page';
import type { OpeningStockGrid } from '@/features/opening-stock/types';
import type { ProductListItem, ProductSuggestion } from '@/features/products/types';

const toastMock = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...args: unknown[]) => toastMock(...args) }));

const WH1 = '11111111-1111-1111-1111-111111111111';
const P1 = '33333333-3333-3333-3333-333333333333';
const P2 = '44444444-4444-4444-4444-444444444444';
const P3 = '55555555-5555-5555-5555-555555555555';

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({
    data: [{ id: WH1, code: 'KHO01', name: 'Kho chính', branchId: 'b', branchCode: 'CN01', branchName: 'CN 1', isActive: true }],
  }),
}));

const gridState: { data: OpeningStockGrid | undefined } = { data: undefined };
const saveMock = vi.fn();
vi.mock('@/features/opening-stock/hooks', () => ({
  useOpeningStock: () => ({ data: gridState.data, isLoading: false }),
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
    gridState.data = GRID;
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
    expect(screen.getByText('100,000')).toBeInTheDocument(); // read-only unit value = amount / quantity

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
    fireEvent.change(byId('opening-amount-1'), { target: { value: '500000' } });

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
    gridState.data = { warehouseId: WH1, openingDate: null, lines: [] };

    render(<OpeningStockPage />);

    expect(byId('opening-date')).toHaveValue('2026-10-01');
  });

  it('negative stock warning resends with acknowledgement', async () => {
    saveMock
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', { 'TON01@KHO01': ['Âm 5 m² tại 05/10/2026 08:00'] }))
      .mockResolvedValueOnce(GRID);
    render(<OpeningStockPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    expect(await screen.findByText('Cảnh báo xuất âm kho')).toBeInTheDocument();
    expect(screen.getByText('TON01 @ KHO01')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(saveMock).toHaveBeenCalledTimes(2));
    expect(saveMock.mock.calls[1][0]).toMatchObject({ acknowledgeNegativeStock: true });
    await waitFor(() => expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' })));
  });
});
