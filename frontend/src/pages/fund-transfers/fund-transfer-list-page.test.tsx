import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { FundTransferListPage } from './fund-transfer-list-page';
import { useAuthStore } from '@/stores/auth-store';
import type { Fund } from '@/features/funds/types';
import type {
  FundTransferListItem,
  FundTransferListParams,
  FundTransferListResult,
} from '@/features/fund-transfers/types';

const useFundTransfersMock = vi.fn();

vi.mock('@/features/fund-transfers/hooks', () => ({
  useFundTransfers: (params: FundTransferListParams) => useFundTransfersMock(params),
}));

const F1 = '11111111-1111-4111-8111-111111111111';
const F2 = '22222222-2222-4222-8222-222222222222';

const funds: Fund[] = [
  {
    id: F1,
    branchId: 'b1',
    code: 'QTM',
    name: 'Quỹ tiền mặt',
    kind: 'Cash',
    isDefault: true,
    openingAmount: 0,
    isActive: true,
  },
  {
    id: F2,
    branchId: 'b1',
    code: 'VCB',
    name: 'Vietcombank',
    kind: 'Bank',
    isDefault: true,
    openingAmount: 0,
    isActive: true,
  },
];

vi.mock('@/features/funds/hooks', () => ({ useFunds: () => ({ data: funds }) }));

const item: FundTransferListItem = {
  id: 't1',
  code: 'CQ00001',
  voucherAt: '2026-10-05T02:15:00Z',
  fromFundCode: 'QTM',
  fromFundName: 'Quỹ tiền mặt',
  toFundCode: 'VCB',
  toFundName: 'Vietcombank',
  amount: 2_500_000,
  description: 'Nộp tiền vào ngân hàng',
  status: 'Active',
  ownerUserId: 'u1',
  ownerName: 'Kế toán A',
};

function result(items: FundTransferListItem[] = [item]): FundTransferListResult {
  return {
    items,
    totalItems: items.length,
    page: 1,
    pageSize: 20,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    totalAmount: 7_000_000,
  };
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname + location.search}</div>;
}

function renderPage(entry = '/fund-transfers') {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          path="/fund-transfers"
          element={
            <>
              <FundTransferListPage />
              <LocationProbe />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

function lastParams(): FundTransferListParams {
  const calls = useFundTransfersMock.mock.calls;
  return calls[calls.length - 1][0];
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 't',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'u1', username: 'kt', email: 'k@example.com', fullName: 'Kế toán A', roles: [], permissions },
  });
}

describe('FundTransferListPage', () => {
  beforeEach(() => {
    useFundTransfersMock.mockReset();
    useFundTransfersMock.mockReturnValue({ data: result(), isLoading: false, isFetching: false, isError: false });
    setPermissions(['fund_transfers.view', 'fund_transfers.create']);
  });

  it('lists transfers with filters', async () => {
    renderPage('/fund-transfers?from=2026-10-01&to=2026-10-31');

    expect(screen.getByRole('heading', { name: 'Danh sách phiếu chuyển quỹ' })).toBeInTheDocument();
    for (const header of ['Số phiếu', 'Ngày', 'Quỹ đi', 'Quỹ đến', 'Số tiền', 'Diễn giải', 'Trạng thái', 'Người lập']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    }
    const row = screen.getByRole('row', { name: /CQ00001/ });
    expect(within(row).getByText('QTM — Quỹ tiền mặt')).toBeInTheDocument();
    expect(within(row).getByText('VCB — Vietcombank')).toBeInTheDocument();
    expect(within(row).getByText('2.500.000')).toBeInTheDocument();
    expect(within(row).getByText('Nộp tiền vào ngân hàng')).toBeInTheDocument();
    expect(within(row).getByText('Kế toán A')).toBeInTheDocument();
    // Footer total is the server's filtered total, not the page sum.
    expect(screen.getByRole('group', { name: 'Tổng kết phiếu' })).toHaveTextContent('7.000.000');
    expect(screen.getByRole('link', { name: /Tạo phiếu chuyển quỹ/ })).toHaveAttribute('href', '/fund-transfers/new');

    expect(lastParams()).toMatchObject({ from: '2026-10-01', to: '2026-10-31', status: 'Active', page: 1 });

    fireEvent.change(screen.getByLabelText('Quỹ'), { target: { value: F2 } });
    await waitFor(() => expect(lastParams()).toMatchObject({ fundId: F2 }));

    fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: 'All' } });
    await waitFor(() => expect(lastParams()).toMatchObject({ status: 'All', fundId: F2 }));

    fireEvent.change(screen.getByPlaceholderText('Tìm theo số phiếu / diễn giải...'), { target: { value: 'CQ0' } });
    await waitFor(() => expect(lastParams()).toMatchObject({ search: 'CQ0' }));
    expect(screen.getByTestId('location').textContent).toContain('fundId=' + F2);
  });

  it('hides create without fund_transfers.create', () => {
    setPermissions(['fund_transfers.view']);
    renderPage();

    expect(screen.queryByRole('link', { name: /Tạo phiếu chuyển quỹ/ })).not.toBeInTheDocument();
  });
});
