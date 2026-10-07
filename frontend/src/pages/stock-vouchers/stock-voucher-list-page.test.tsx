import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation, useParams } from 'react-router-dom';
import { StockVoucherListPage } from './stock-voucher-list-page';
import { stockVouchersApi } from '@/features/stock-vouchers/api';
import { apiGet } from '@/lib/api-client';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import type {
  PartnerSearchItem,
  StockVoucherListItem,
  StockVoucherListParams,
  StockVoucherListResult,
} from '@/features/stock-vouchers/types';

vi.mock('@/lib/api-client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/api-client')>();
  return { ...actual, apiGet: vi.fn().mockResolvedValue({}) };
});

const useStockVouchersMock = vi.fn();
const partner: PartnerSearchItem = {
  id: '77777777-7777-4777-8777-777777777777',
  code: 'NCC01',
  name: 'Công ty Thép Việt',
  status: 'Active',
  isCustomer: false,
  isSupplier: true,
};

vi.mock('@/features/stock-vouchers/hooks', () => ({
  useStockVouchers: (params: StockVoucherListParams) => useStockVouchersMock(params),
  useStockVoucherOwners: () => ({ data: [{ id: 'u1', fullName: 'Thủ kho A' }] }),
  usePartnerSearch: (_type: string, keyword: string) => ({
    data: keyword ? [partner] : [],
    isLoading: false,
    isError: false,
  }),
}));

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({
    data: [
      { id: 'w1', code: 'KHO01', name: 'Kho chính', branchId: 'b1', branchCode: 'CN01', branchName: 'CN 1', isActive: true },
    ],
  }),
}));

vi.mock('@/features/stock-reasons/hooks', () => ({
  useStockReasons: () => ({
    data: [
      { id: 'r1', code: 'NMH', name: 'Nhập mua hàng', direction: 'In', partnerType: 'Supplier', isSystem: true },
      { id: 'r2', code: 'XBH', name: 'Xuất bán hàng', direction: 'Out', partnerType: 'Customer', isSystem: true },
    ],
  }),
}));

const item: StockVoucherListItem = {
  id: 'v1',
  type: 'In',
  code: 'PN00001',
  voucherAt: '2026-10-05T02:15:00Z',
  warehouseName: 'Kho chính',
  partnerName: 'Công ty Thép Việt',
  reasonName: 'Nhập mua hàng',
  paymentMethodName: 'Tiền mặt',
  goodsAmount: 1_000_000,
  discountTotal: 50_000,
  vatTotal: 95_000,
  freight: 30_000,
  total: 1_075_000,
  paidAmount: 500_000,
  status: 'Active',
  ownerUserId: 'u1',
  ownerName: 'Thủ kho A',
  createdAt: '2026-10-05T02:15:00Z',
};

function result(items: StockVoucherListItem[] = [item]): StockVoucherListResult {
  return {
    items,
    totalItems: items.length,
    page: 1,
    pageSize: 20,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    aggregates: {
      goodsAmount: 1_000_000,
      discountTotal: 50_000,
      vatTotal: 95_000,
      freight: 30_000,
      total: 1_075_000,
      paidAmount: 500_000,
    },
  } as StockVoucherListResult;
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname + location.search}</div>;
}

function DetailProbe() {
  const { id } = useParams();
  return <div>Chi tiết {id}</div>;
}

function renderPage(type: 'In' | 'Out', entry: string) {
  const base = type === 'In' ? '/stock-in' : '/stock-out';
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          path={base}
          element={
            <>
              <StockVoucherListPage type={type} />
              <LocationProbe />
            </>
          }
        />
        <Route path={`${base}/:id`} element={<DetailProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

function lastParams(): StockVoucherListParams {
  const calls = useStockVouchersMock.mock.calls;
  return calls[calls.length - 1][0];
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 't',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'u1', username: 'kho', email: 'k@example.com', fullName: 'Thủ kho A', roles: [], permissions },
  });
}

describe('StockVoucherListPage', () => {
  beforeEach(() => {
    localStorage.clear();
    useStockVouchersMock.mockReset();
    useStockVouchersMock.mockReturnValue({ data: result(), isLoading: false, isFetching: false, isError: false });
    useUiStore.setState({ stockVoucherStatusFilter: { In: null, Out: null } });
    vi.mocked(apiGet).mockClear();
    setPermissions(['stock_in.view', 'stock_in.create', 'stock_out.view']);
  });

  it('renders rows and footer aggregates', () => {
    renderPage('In', '/stock-in');

    expect(screen.getByRole('heading', { name: 'Danh sách phiếu nhập kho' })).toBeInTheDocument();
    expect(screen.getByText('PN00001')).toBeInTheDocument();
    expect(screen.getByText('Nhập mua hàng', { selector: 'span' })).toBeInTheDocument();
    const footer = screen.getByRole('group', { name: 'Tổng kết phiếu' });
    expect(footer).toHaveTextContent('1.000.000');
    expect(footer).toHaveTextContent('1.075.000');
    expect(footer).toHaveTextContent('500.000');
    expect(screen.getByRole('link', { name: /Tạo phiếu nhập kho/ })).toHaveAttribute('href', '/stock-in/new');
  });

  it('passes filters from the URL (or the persisted status when the URL has none) to useStockVouchers', () => {
    const { unmount } = renderPage('In', '/stock-in?from=2026-10-01&to=2026-10-31&status=Cancelled');

    expect(lastParams()).toMatchObject({
      type: 'In',
      page: 1,
      pageSize: 20,
      from: '2026-10-01',
      to: '2026-10-31',
      status: 'Cancelled',
    });
    unmount();

    useUiStore.setState({ stockVoucherStatusFilter: { In: 'Cancelled', Out: null } });
    renderPage('In', '/stock-in');
    expect(lastParams().status).toBe('Cancelled');

    useStockVouchersMock.mockClear();
    useUiStore.setState({ stockVoucherStatusFilter: { In: null, Out: null } });
    expect(lastParamsAfterRerender('Out')).toBe('Active');
  });

  it('selecting a partner filter writes partnerId to the URL and passes it to useStockVouchers', async () => {
    renderPage('In', '/stock-in');

    fireEvent.change(screen.getByPlaceholderText('Lọc theo đối tượng...'), { target: { value: 'NCC' } });
    const listbox = await screen.findByRole('listbox');
    fireEvent.mouseDown(within(listbox).getByText('NCC01'));

    await waitFor(() => expect(lastParams().partnerId).toBe(partner.id));
    expect(screen.getByTestId('location').textContent).toContain(`partnerId=${partner.id}`);
    const search = (screen.getByTestId('location').textContent ?? '').split('?')[1];
    expect(new URLSearchParams(search).get('partnerName')).toBe('Công ty Thép Việt');
  });

  it('stock-out list uses stock-out labels and path, and hides create without stock_out.create', () => {
    useStockVouchersMock.mockReturnValue({
      data: result([{ ...item, id: 'v9', type: 'Out', code: 'PX00009' }]),
      isLoading: false,
      isFetching: false,
      isError: false,
    });
    renderPage('Out', '/stock-out');

    expect(screen.getByRole('heading', { name: 'Danh sách phiếu xuất kho' })).toBeInTheDocument();
    expect(lastParams().type).toBe('Out');
    expect(screen.queryByRole('link', { name: /Tạo phiếu xuất kho/ })).not.toBeInTheDocument();

    fireEvent.click(screen.getByText('PX00009'));
    expect(screen.getByText('Chi tiết v9')).toBeInTheDocument();
  });

  it('status all sends no status filter', async () => {
    renderPage('In', '/stock-in?status=all');

    const params = lastParams();
    expect(params.status).toBe('all');
    expect(screen.getByRole('group', { name: 'Tổng kết phiếu' })).toHaveTextContent('không gồm phiếu đã hủy');

    await stockVouchersApi.list(params);
    const sent = vi.mocked(apiGet).mock.calls[0][1] as Record<string, unknown>;
    expect(sent).not.toHaveProperty('status');
    expect(sent.type).toBe('In');
  });

  it('changing the status filter persists it for the type', () => {
    renderPage('In', '/stock-in');

    fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: 'all' } });

    expect(useUiStore.getState().stockVoucherStatusFilter).toEqual({ In: 'all', Out: null });
    expect(screen.getByTestId('location').textContent).toContain('status=all');
    expect(lastParams().status).toBe('all');
  });
});

function lastParamsAfterRerender(type: 'In' | 'Out') {
  renderPage(type, type === 'In' ? '/stock-in' : '/stock-out');
  return lastParams().status;
}
