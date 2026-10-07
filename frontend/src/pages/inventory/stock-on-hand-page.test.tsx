import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { StockOnHandPage } from './stock-on-hand-page';
import type { StockOnHandParams, StockOnHandReport } from '@/features/inventory-reports/types';

const navigateMock = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({ data: [{ id: 'w1', code: 'KHO01', name: 'Kho chính', isActive: true }] }),
}));
vi.mock('@/features/products/hooks', () => ({
  useProductGroups: () => ({ data: [{ id: 'g1', code: 'EPS', name: 'Xốp EPS' }] }),
}));

const reportState: { data: StockOnHandReport | undefined; error?: Error } = { data: undefined };
const useStockOnHandMock = vi.fn((params: StockOnHandParams) => {
  void params;
  return { data: reportState.data, isLoading: false, isError: !!reportState.error, error: reportState.error };
});
vi.mock('@/features/inventory-reports/hooks', () => ({
  useStockOnHand: (params: StockOnHandParams) => useStockOnHandMock(params),
}));

const report = (over: Partial<StockOnHandReport> = {}): StockOnHandReport => ({
  at: '2026-10-07T03:00:00Z',
  isProvisional: true,
  canViewCost: true,
  totalValue: 1_250_000,
  rows: [
    {
      productId: 'p1',
      productCode: 'TON01',
      productName: 'Tôn lạnh',
      productGroupName: 'Tôn',
      unitName: 'm²',
      warehouseId: 'w1',
      warehouseCode: 'KHO01',
      warehouseName: 'Kho chính',
      quantity: 12.5,
      value: 1_250_000,
    },
  ],
  ...over,
});

describe('StockOnHandPage', () => {
  beforeEach(() => {
    navigateMock.mockReset();
    useStockOnHandMock.mockClear();
    reportState.error = undefined;
  });

  it('renders rows, provisional badge and opens the stock card on row click', () => {
    reportState.data = report();
    render(<StockOnHandPage />);

    expect(screen.getByText('TON01')).toBeInTheDocument();
    expect(screen.getByText('12.5')).toBeInTheDocument();
    expect(screen.getByText('Giá trị tạm tính')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Giá trị' })).toBeInTheDocument();
    expect(screen.getByText('Tổng giá trị')).toBeInTheDocument();
    expect(screen.getAllByText('1.250.000')).toHaveLength(2); // vi-VN grouping

    fireEvent.click(screen.getByText('Tôn lạnh'));
    expect(navigateMock).toHaveBeenCalledWith('/inventory/stock-card?productId=p1&warehouseId=w1');
  });

  it('sends the point in time as an ISO instant and the search text', async () => {
    reportState.data = report();
    render(<StockOnHandPage />);

    fireEvent.change(screen.getByLabelText('Thời điểm'), { target: { value: '2026-10-05T08:30' } });
    fireEvent.change(screen.getByLabelText('Tìm kiếm'), { target: { value: 'TON' } });

    await waitFor(() =>
      expect(useStockOnHandMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ at: new Date('2026-10-05T08:30').toISOString(), search: 'TON' }),
      ),
    );
  });

  it('hides value column and total without view cost', () => {
    reportState.data = report({
      canViewCost: false,
      isProvisional: false,
      totalValue: null,
      rows: report().rows.map((r) => ({ ...r, value: null })),
    });
    render(<StockOnHandPage />);

    expect(screen.queryByRole('columnheader', { name: 'Giá trị' })).not.toBeInTheDocument();
    expect(screen.queryByText('Tổng giá trị')).not.toBeInTheDocument();
    expect(screen.queryByText('Giá trị tạm tính')).not.toBeInTheDocument();
  });

  it('shows the request error instead of the empty state', () => {
    reportState.data = undefined;
    reportState.error = new Error('Máy chủ lỗi');
    render(<StockOnHandPage />);

    expect(screen.getByText('Máy chủ lỗi')).toBeInTheDocument();
    expect(screen.queryByText('Không có hàng tồn')).not.toBeInTheDocument();
  });
});
