import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { StockCardPage } from './stock-card-page';
import type { StockCard, StockCardParams } from '@/features/inventory-reports/types';

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({ data: [{ id: 'w1', code: 'KHO01', name: 'Kho chính', isActive: true }] }),
}));
vi.mock('@/features/products/hooks', () => ({
  useProductSearch: () => ({ data: [], isLoading: false, isError: false }),
  useProducts: () => ({ data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }, isLoading: false }),
  useProductGroups: () => ({ data: [] }),
}));

const cardState: { data: StockCard | undefined } = { data: undefined };
const useStockCardMock = vi.fn((params: StockCardParams) => {
  void params;
  return { data: cardState.data, isLoading: false };
});
vi.mock('@/features/inventory-reports/hooks', () => ({
  useStockCard: (params: StockCardParams) => useStockCardMock(params),
}));

const card = (over: Partial<StockCard> = {}): StockCard => ({
  productId: 'p1',
  productCode: 'TON01',
  productName: 'Tôn lạnh',
  unitName: 'm²',
  from: '2026-10-01',
  to: '2026-10-31',
  isProvisional: true,
  canViewCost: true,
  valuesAtScopeOnly: false,
  openingQty: 5,
  openingValue: 500_000,
  inQty: 10,
  outQty: 4,
  inValue: 1_100_000,
  outValue: 440_000,
  closingQty: 11,
  closingValue: 1_160_000,
  rows: [
    {
      postedAt: '2026-10-01T17:00:00Z', sourceType: 'Opening', sourceId: 'w1', sourceCode: 'TDK',
      reasonName: 'Tồn đầu kỳ', partnerName: null, warehouseCode: 'KHO01',
      qtyIn: 0, qtyOut: 0, unitCost: null, inValue: null, costAmount: null, runningQty: 5, runningValue: 500_000,
    },
    {
      postedAt: '2026-10-02T02:00:00Z', sourceType: 'StockIn', sourceId: 'v1', sourceCode: 'PN00001',
      reasonName: 'Nhập mua hàng', partnerName: 'NCC A', warehouseCode: 'KHO01',
      qtyIn: 10, qtyOut: 0, unitCost: 110_000, inValue: 1_100_000, costAmount: null, runningQty: 15, runningValue: 1_600_000,
    },
    {
      postedAt: '2026-10-05T02:00:00Z', sourceType: 'StockOut', sourceId: 'v2', sourceCode: 'PX00001',
      reasonName: 'Xuất bán hàng', partnerName: 'KH B', warehouseCode: 'KHO01',
      qtyIn: 0, qtyOut: 4, unitCost: 110_000, inValue: null, costAmount: 440_000, runningQty: 11, runningValue: 1_160_000,
    },
  ],
  ...over,
});

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <StockCardPage />
    </MemoryRouter>,
  );
}

describe('StockCardPage', () => {
  beforeEach(() => useStockCardMock.mockClear());

  it('reads product from the URL and renders opening, rows (voucher codes linked by type, opening rows unlinked) and closing', () => {
    cardState.data = card();
    renderAt('/inventory/stock-card?productId=p1&warehouseId=w1');

    expect(useStockCardMock).toHaveBeenLastCalledWith(expect.objectContaining({ productId: 'p1', warehouseId: 'w1' }));
    expect(screen.getByText('Giá vốn tạm tính')).toBeInTheDocument();

    const summary = screen.getByTestId('stock-card-summary');
    expect(within(summary).getByText('Tồn đầu')).toBeInTheDocument();
    expect(within(summary).getByText('11 m²')).toBeInTheDocument();
    expect(within(summary).getByText('1,160,000')).toBeInTheDocument();

    expect(screen.getByRole('link', { name: 'PN00001' })).toHaveAttribute('href', '/stock-in/v1');
    expect(screen.getByRole('link', { name: 'PX00001' })).toHaveAttribute('href', '/stock-out/v2');
    expect(screen.queryByRole('link', { name: /TDK|Tồn đầu kỳ/ })).not.toBeInTheDocument();
    expect(screen.getAllByText('Tồn đầu kỳ').length).toBeGreaterThan(0);
    expect(screen.getByRole('columnheader', { name: 'Giá trị tồn' })).toBeInTheDocument();
  });

  it('value columns hidden without view cost', () => {
    cardState.data = card({ canViewCost: false, openingValue: null, closingValue: null, inValue: null, outValue: null });
    renderAt('/inventory/stock-card?productId=p1');

    expect(screen.queryByRole('columnheader', { name: 'Đơn giá' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Giá trị tồn' })).not.toBeInTheDocument();
    expect(screen.queryByText('Giá vốn tạm tính')).not.toBeInTheDocument();
  });

  it('values-at-scope-only hides running values and shows the note', () => {
    cardState.data = card({ valuesAtScopeOnly: true, openingValue: null, closingValue: null });
    renderAt('/inventory/stock-card?productId=p1&warehouseId=w1');

    expect(screen.getByRole('columnheader', { name: 'Đơn giá' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Giá trị tồn' })).not.toBeInTheDocument();
    expect(screen.getByText('Giá trị tồn tính theo chi nhánh — bỏ lọc kho để xem')).toBeInTheDocument();
  });

  it('asks for a product when none is selected', () => {
    cardState.data = undefined;
    renderAt('/inventory/stock-card');

    expect(useStockCardMock).toHaveBeenLastCalledWith(expect.objectContaining({ productId: '' }));
    expect(screen.getByText('Chọn hàng hóa để xem thẻ kho')).toBeInTheDocument();
  });
});
