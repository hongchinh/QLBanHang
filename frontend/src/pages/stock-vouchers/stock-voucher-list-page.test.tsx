import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { createPortal } from 'react-dom';
import { MemoryRouter, Route, Routes, useLocation, useParams } from 'react-router-dom';
import { StockVoucherListPage } from './stock-voucher-list-page';
import { stockVouchersApi } from '@/features/stock-vouchers/api';
import { apiGet } from '@/lib/api-client';
import type * as ApiClient from '@/lib/api-client';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import type {
  PartnerSearchItem,
  StockVoucherListItem,
  StockVoucherListParams,
  StockVoucherListResult,
} from '@/features/stock-vouchers/types';

vi.mock('@/lib/api-client', async (importOriginal) => {
  const actual = await importOriginal<typeof ApiClient>();
  return { ...actual, apiGet: vi.fn().mockResolvedValue({}) };
});

const useStockVouchersMock = vi.fn();
const printCalls: Array<Record<string, unknown>> = [];

vi.mock('@/components/voucher-print/voucher-print-button', () => ({
  VoucherPrintButton: (props: { variant?: string; vouchers: Array<{ id: string; code: string }> }) => {
    printCalls.push(props as unknown as Record<string, unknown>);
    if (props.variant === 'icon') {
      return (
        <>
          <span data-testid={`print-icon-${props.vouchers[0].code}`} />
          {createPortal(<button>portal-{props.vouchers[0].code}</button>, document.body)}
        </>
      );
    }
    return <span data-testid="print-bar">{props.vouchers.map((v) => v.code).join(',')}</span>;
  },
}));
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

  it('on page 2, changing a filter keeps the filter and goes back to page 1', async () => {
    renderPage('In', '/stock-in?page=2&reasonId=r1');
    expect(lastParams()).toMatchObject({ page: 2, reasonId: 'r1' });

    fireEvent.change(screen.getByLabelText('Kho'), { target: { value: 'w1' } });

    await waitFor(() => expect(lastParams()).toMatchObject({ page: 1, warehouseId: 'w1', reasonId: 'r1' }));
    const search = new URLSearchParams((screen.getByTestId('location').textContent ?? '').split('?')[1]);
    expect(search.get('warehouseId')).toBe('w1');
    expect(search.get('reasonId')).toBe('r1');
    expect(search.has('page')).toBe(false);
  });

  it('on page 2, typing a search keeps the first keystroke and goes back to page 1', async () => {
    renderPage('In', '/stock-in?page=2');

    fireEvent.change(screen.getByPlaceholderText('Tìm theo số phiếu / đối tượng...'), { target: { value: 'P' } });

    await waitFor(() => expect(lastParams().page).toBe(1));
    const search = new URLSearchParams((screen.getByTestId('location').textContent ?? '').split('?')[1]);
    expect(search.get('q')).toBe('P');
    expect(search.has('page')).toBe(false);
  });

  it('changing the status filter persists it for the type', () => {
    renderPage('In', '/stock-in');

    fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: 'all' } });

    expect(useUiStore.getState().stockVoucherStatusFilter).toEqual({ In: 'all', Out: null });
    expect(screen.getByTestId('location').textContent).toContain('status=all');
    expect(lastParams().status).toBe('all');
  });

  describe('selection and print', () => {
    const second: StockVoucherListItem = { ...item, id: 'v2', code: 'PN00002' };
    const third: StockVoucherListItem = { ...item, id: 'v3', code: 'PN00003' };

    function useThree(hasNextPage = false) {
      useStockVouchersMock.mockReturnValue({
        data: { ...result([item, second, third]), hasNextPage, totalPages: hasNextPage ? 2 : 1 },
        isLoading: false,
        isFetching: false,
        isError: false,
      });
    }

    function lastCall(variant: 'icon' | 'button', code?: string) {
      const matches = printCalls.filter(
        (c) =>
          (variant === 'icon' ? c.variant === 'icon' : c.variant === undefined) &&
          (!code || (c.vouchers as Array<{ code: string }>)[0].code === code),
      );
      return matches[matches.length - 1];
    }

    beforeEach(() => {
      printCalls.length = 0;
      useThree();
    });

    it('gives every row a checkbox and an icon print button', () => {
      renderPage('In', '/stock-in');
      for (const v of [item, second, third]) {
        expect(screen.getByRole('checkbox', { name: `Chọn ${v.code}` })).toBeInTheDocument();
        expect(lastCall('icon', v.code)).toMatchObject({
          variant: 'icon',
          kind: 'stock',
          type: 'In',
          vouchers: [{ id: v.id, code: v.code }],
        });
      }
    });

    it('shows the bar with the selected vouchers in page order', () => {
      renderPage('In', '/stock-in');
      expect(screen.queryByRole('region', { name: 'Phiếu đã chọn' })).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn PN00003' }));
      fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn PN00001' }));
      expect(screen.getByRole('region', { name: 'Phiếu đã chọn' })).toHaveTextContent('Đã chọn 2 phiếu');
      expect(lastCall('button')).toMatchObject({
        kind: 'stock',
        type: 'In',
        vouchers: [
          { id: 'v1', code: 'PN00001' },
          { id: 'v3', code: 'PN00003' },
        ],
      });
    });

    it('header checkbox selects all and is indeterminate after unticking one', () => {
      renderPage('In', '/stock-in');
      const getAll = () =>
        screen.getByRole('checkbox', { name: 'Chọn tất cả phiếu trong trang' }) as HTMLInputElement;
      fireEvent.click(getAll());
      expect(screen.getByRole('region', { name: 'Phiếu đã chọn' })).toHaveTextContent('Đã chọn 3 phiếu');
      expect(getAll().checked).toBe(true);
      fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn PN00002' }));
      expect(getAll().checked).toBe(false);
      expect(getAll().indeterminate).toBe(true);
      expect(screen.getByRole('region', { name: 'Phiếu đã chọn' })).toHaveTextContent('Đã chọn 2 phiếu');
    });

    it('ticking and portal clicks do not navigate', () => {
      renderPage('In', '/stock-in');
      fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn PN00001' }));
      expect(screen.getByTestId('location')).toHaveTextContent(/^\/stock-in$/);

      fireEvent.click(screen.getByText('portal-PN00001'));
      expect(screen.getByTestId('location')).toHaveTextContent(/^\/stock-in$/);
    });

    it('navigates when the row own cell is clicked', () => {
      renderPage('In', '/stock-in');
      fireEvent.click(screen.getAllByText('Kho chính', { selector: 'span' })[0]);
      expect(screen.getByText('Chi tiết v1')).toBeInTheDocument();
    });

    it('keeps the checkbox mounted and focused while selecting', () => {
      renderPage('In', '/stock-in');
      const box = screen.getByRole('checkbox', { name: 'Chọn PN00001' });
      box.focus();
      fireEvent.click(box);
      expect(box.isConnected).toBe(true);
      expect(document.activeElement).toBe(box);
    });

    it('Bỏ chọn hides the bar', () => {
      renderPage('In', '/stock-in');
      fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn PN00001' }));
      fireEvent.click(screen.getByRole('button', { name: 'Bỏ chọn' }));
      expect(screen.queryByRole('region', { name: 'Phiếu đã chọn' })).not.toBeInTheDocument();
    });

    it('clears the selection on next page', () => {
      useThree(true);
      renderPage('In', '/stock-in');
      fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn PN00001' }));
      expect(screen.getByRole('region', { name: 'Phiếu đã chọn' })).toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Sau' }));
      expect(screen.queryByRole('region', { name: 'Phiếu đã chọn' })).not.toBeInTheDocument();
    });

    it('clears the selection when the status filter changes', () => {
      renderPage('In', '/stock-in');
      fireEvent.click(screen.getByRole('checkbox', { name: 'Chọn PN00001' }));
      expect(screen.getByRole('region', { name: 'Phiếu đã chọn' })).toBeInTheDocument();
      fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: 'all' } });
      expect(screen.queryByRole('region', { name: 'Phiếu đã chọn' })).not.toBeInTheDocument();
    });
  });
});

function lastParamsAfterRerender(type: 'In' | 'Out') {
  renderPage(type, type === 'In' ? '/stock-in' : '/stock-out');
  return lastParams().status;
}
