import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { CashVoucherListPage } from './cash-voucher-list-page';
import { useAuthStore } from '@/stores/auth-store';
import type {
  CashPartnerSearchItem,
  CashVoucherListItem,
  CashVoucherListParams,
  CashVoucherListResult,
} from '@/features/cash-vouchers/types';
import type { CashVoucherType } from '@/features/cash-common/types';

const useCashVouchersMock = vi.fn();
const partner: CashPartnerSearchItem = {
  id: '77777777-7777-4777-8777-777777777777',
  code: 'KH01',
  name: 'Công ty An Phát',
  status: 'Active',
  isCustomer: true,
  isSupplier: false,
};

vi.mock('@/features/cash-vouchers/hooks', () => ({
  useCashVouchers: (params: CashVoucherListParams) => useCashVouchersMock(params),
  useCashPartnerSearch: (_type: string, keyword: string) => ({
    data: keyword ? [partner] : [],
    isLoading: false,
    isFetching: false,
    isError: false,
  }),
}));

vi.mock('@/features/funds/hooks', () => ({
  useFunds: () => ({
    data: [
      { id: 'f1', branchId: 'b1', code: 'QTM', name: 'Quỹ tiền mặt', kind: 'Cash', isDefault: true, openingAmount: 0, isActive: true },
      { id: 'f2', branchId: 'b1', code: 'VCB', name: 'Vietcombank', kind: 'Bank', isDefault: true, openingAmount: 0, isActive: true },
    ],
  }),
}));

vi.mock('@/features/cash-reasons/hooks', () => ({
  useCashReasons: () => ({
    data: [
      { id: 'r1', code: 'TKH', name: 'Thu tiền khách hàng', type: 'Receipt', partnerType: 'Customer', debtEffect: 'ReceivableDecrease', isSystem: true, isAuto: false },
      { id: 'r2', code: 'TPX', name: 'Thu tiền theo phiếu xuất', type: 'Receipt', partnerType: 'Customer', debtEffect: 'ReceivableDecrease', isSystem: true, isAuto: true },
      { id: 'r3', code: 'TNCC', name: 'Trả tiền nhà cung cấp', type: 'Payment', partnerType: 'Supplier', debtEffect: 'PayableDecrease', isSystem: true, isAuto: false },
    ],
  }),
}));

const manual: CashVoucherListItem = {
  id: 'v1',
  type: 'Receipt',
  code: 'PT00001',
  voucherAt: '2026-10-05T02:15:00Z',
  fundCode: 'QTM',
  fundName: 'Quỹ tiền mặt',
  reasonName: 'Thu tiền khách hàng',
  partnerCode: 'KH01',
  partnerName: 'Công ty An Phát',
  personName: 'Nguyễn Văn A',
  description: 'Thu nợ tháng 9',
  total: 1_500_000,
  status: 'Active',
  ownerUserId: 'u1',
  ownerName: 'Kế toán A',
  isManaged: false,
};

const managed: CashVoucherListItem = {
  ...manual,
  id: 'v2',
  code: 'PT00002',
  reasonName: 'Thu tiền theo phiếu xuất',
  description: 'Thu tiền theo phiếu xuất PX00007',
  total: 800_000,
  ownerUserId: 'u2',
  ownerName: 'Thủ kho B',
  sourceStockVoucherId: 'sv7',
  sourceStockVoucherCode: 'PX00007',
  isManaged: true,
};

function result(items: CashVoucherListItem[], totalAmount: number): CashVoucherListResult {
  return {
    items,
    totalItems: items.length,
    page: 1,
    pageSize: 20,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    totalAmount,
  };
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname + location.search}</div>;
}

function renderPage(type: CashVoucherType, entry: string) {
  const base = type === 'Receipt' ? '/receipts' : '/payments';
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          path={base}
          element={
            <>
              <CashVoucherListPage type={type} />
              <LocationProbe />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

function lastParams(): CashVoucherListParams {
  const calls = useCashVouchersMock.mock.calls;
  return calls[calls.length - 1][0];
}

function currentSearch() {
  return new URLSearchParams((screen.getByTestId('location').textContent ?? '').split('?')[1]);
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 't',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'u1', username: 'kt', email: 'kt@example.com', fullName: 'Kế toán A', roles: [], permissions },
  });
}

describe('CashVoucherListPage', () => {
  beforeEach(() => {
    localStorage.clear();
    useCashVouchersMock.mockReset();
    useCashVouchersMock.mockReturnValue({
      data: result([manual, managed], 2_300_000),
      isLoading: false,
      isFetching: false,
      isError: false,
    });
    setPermissions(['receipts.view', 'receipts.create', 'payments.view', 'payments.create']);
  });

  it('reads filters from the url and sends them', async () => {
    renderPage(
      'Receipt',
      '/receipts?q=PT&page=2&size=50&from=2026-10-01&to=2026-10-31&fundId=f1&reasonId=r1' +
        `&partnerId=${partner.id}&partnerName=C%C3%B4ng%20ty%20An%20Ph%C3%A1t&status=All&owners=u1,u2`,
    );

    expect(lastParams()).toEqual({
      type: 'Receipt',
      page: 2,
      pageSize: 50,
      search: 'PT',
      from: '2026-10-01',
      to: '2026-10-31',
      fundId: 'f1',
      reasonId: 'r1',
      partnerId: partner.id,
      status: 'All',
      ownerIds: ['u1', 'u2'],
    });
    expect(screen.getByLabelText('Quỹ')).toHaveValue('f1');
    expect(screen.getByLabelText('Lý do')).toHaveValue('r1');
    expect(screen.getByLabelText('Trạng thái')).toHaveValue('All');
    // The URL keeps the partner name only; the filter shows it.
    expect(screen.getByRole('combobox', { name: 'Đối tượng' })).toHaveValue('Công ty An Phát');

    // The footer shows the server total of the whole filtered set.
    const footer = screen.getByRole('group', { name: 'Tổng kết phiếu' });
    expect(footer).toHaveTextContent('2.300.000');
    expect(footer).toHaveTextContent('gồm cả phiếu đã hủy');

    // A filter change keeps the other filters and goes back to page 1.
    fireEvent.change(screen.getByLabelText('Quỹ'), { target: { value: 'f2' } });
    await waitFor(() => expect(lastParams()).toMatchObject({ page: 1, fundId: 'f2', reasonId: 'r1' }));
    expect(currentSearch().get('fundId')).toBe('f2');
    expect(currentSearch().has('page')).toBe(false);
  });

  it('defaults to active vouchers and filters by partner through the cash partner search', async () => {
    renderPage('Receipt', '/receipts');
    expect(lastParams()).toMatchObject({ type: 'Receipt', page: 1, pageSize: 20, status: 'Active' });
    expect(lastParams().ownerIds).toBeUndefined();

    fireEvent.change(screen.getByPlaceholderText('Lọc theo đối tượng...'), { target: { value: 'KH' } });
    const listbox = await screen.findByRole('listbox');
    fireEvent.mouseDown(within(listbox).getByText('KH01'));

    await waitFor(() => expect(lastParams().partnerId).toBe(partner.id));
    expect(currentSearch().get('partnerName')).toBe('Công ty An Phát');
  });

  it('managed rows link to the stock voucher', () => {
    renderPage('Receipt', '/receipts');

    const managedRow = screen.getByText('PT00002').closest('tr')!;
    const pill = within(managedRow).getByRole('link', { name: /Tự động/ });
    expect(pill).toHaveAttribute('href', '/stock-out/sv7');
    expect(pill).toHaveAttribute('title', expect.stringContaining('PX00007'));

    const manualRow = screen.getByText('PT00001').closest('tr')!;
    expect(within(manualRow).queryByText(/Tự động/)).not.toBeInTheDocument();
    expect(within(manualRow).getByRole('link', { name: 'PT00001' })).toHaveAttribute('href', '/receipts/v1');
    expect(within(manualRow).getByText('Quỹ tiền mặt')).toBeInTheDocument();
    expect(within(manualRow).getByText('Nguyễn Văn A')).toBeInTheDocument();
    expect(within(manualRow).getByText('Thu nợ tháng 9')).toBeInTheDocument();
    expect(within(manualRow).getByText('1.500.000')).toBeInTheDocument();
    expect(within(manualRow).getByText('Kế toán A')).toBeInTheDocument();
  });

  it('payment labels', () => {
    useCashVouchersMock.mockReturnValue({
      data: result(
        [
          {
            ...managed,
            id: 'v9',
            type: 'Payment',
            code: 'PC00009',
            sourceStockVoucherId: 'sv9',
            sourceStockVoucherCode: 'PN00009',
          },
        ],
        800_000,
      ),
      isLoading: false,
      isFetching: false,
      isError: false,
    });
    renderPage('Payment', '/payments');

    expect(screen.getByRole('heading', { name: 'Danh sách phiếu chi' })).toBeInTheDocument();
    expect(lastParams().type).toBe('Payment');
    expect(screen.getByRole('columnheader', { name: 'Người nhận' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Người nộp' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'PC00009' })).toHaveAttribute('href', '/payments/v9');
    expect(screen.getByRole('link', { name: /Tự động/ })).toHaveAttribute('href', '/stock-in/sv9');
    expect(screen.getByRole('link', { name: /Thêm/ })).toHaveAttribute('href', '/payments/new');
  });

  it('create button follows permission', () => {
    const { unmount } = renderPage('Receipt', '/receipts');
    expect(screen.getByRole('heading', { name: 'Danh sách phiếu thu' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Người nộp' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Thêm/ })).toHaveAttribute('href', '/receipts/new');
    unmount();

    setPermissions(['receipts.view', 'payments.create']);
    renderPage('Receipt', '/receipts');
    expect(screen.queryByRole('link', { name: /Thêm/ })).not.toBeInTheDocument();
  });
});
