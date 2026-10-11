import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { DebtOffsetListPage } from './debt-offset-list-page';
import { useAuthStore } from '@/stores/auth-store';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import type {
  DebtOffsetListItem,
  DebtOffsetListParams,
  DebtOffsetListResult,
} from '@/features/debt-offsets/types';

const mocks = vi.hoisted(() => ({ list: vi.fn(), partnerSearch: vi.fn() }));

vi.mock('@/features/debt-offsets/hooks', () => ({
  useDebtOffsets: (params: DebtOffsetListParams) => mocks.list(params),
}));

vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (...args: unknown[]) => mocks.partnerSearch(...args),
}));

const P1 = '11111111-1111-4111-8111-111111111111';

const partner: DebtPartnerSearchItem = {
  id: P1,
  code: 'DT01',
  name: 'Công ty Hai Chiều',
  status: 'Active',
  isCustomer: true,
  isSupplier: true,
};

const item: DebtOffsetListItem = {
  id: 'o1',
  code: 'BT00001',
  voucherAt: '2026-10-05T02:15:00Z',
  partnerCode: 'DT01',
  partnerName: 'Công ty Hai Chiều',
  amount: 1_200_000,
  note: 'Bù trừ tháng 10',
  status: 'Active',
  ownerName: 'Kế toán A',
};

function result(items: DebtOffsetListItem[] = [item]): DebtOffsetListResult {
  return {
    items,
    totalItems: items.length,
    page: 1,
    pageSize: 20,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    totalAmount: 4_500_000,
  };
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.pathname + location.search}</div>;
}

function renderPage(entry = '/debt/offsets') {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          path="/debt/offsets"
          element={
            <>
              <DebtOffsetListPage />
              <LocationProbe />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

function lastParams(): DebtOffsetListParams {
  const calls = mocks.list.mock.calls;
  return calls[calls.length - 1][0];
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 't',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'u1', username: 'kt', email: 'k@example.com', fullName: 'Kế toán A', roles: [], permissions },
  });
}

describe('DebtOffsetListPage', () => {
  beforeEach(() => {
    mocks.list.mockReset().mockReturnValue({ data: result(), isLoading: false, isFetching: false, isError: false });
    mocks.partnerSearch.mockReset().mockReturnValue({ data: [partner], isLoading: false, isError: false });
    setPermissions(['debt_offset.view', 'debt_offset.create']);
  });

  it('lists offsets', async () => {
    renderPage('/debt/offsets?from=2026-10-01&to=2026-10-31');

    expect(screen.getByRole('heading', { name: 'Danh sách phiếu bù trừ công nợ' })).toBeInTheDocument();
    for (const header of ['Số phiếu', 'Ngày', 'Đối tượng', 'Số tiền', 'Ghi chú', 'Trạng thái', 'Người lập']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    }
    const row = screen.getByRole('row', { name: /BT00001/ });
    expect(within(row).getByRole('link', { name: 'BT00001' })).toHaveAttribute('href', '/debt/offsets/o1');
    expect(within(row).getByText('DT01 - Công ty Hai Chiều')).toBeInTheDocument();
    expect(within(row).getByText('1.200.000')).toBeInTheDocument();
    expect(within(row).getByText('Bù trừ tháng 10')).toBeInTheDocument();
    expect(within(row).getByText('Kế toán A')).toBeInTheDocument();
    // Footer total is the server's filtered total, not the page sum.
    expect(screen.getByRole('group', { name: 'Tổng kết phiếu' })).toHaveTextContent('4.500.000');
    expect(screen.getByRole('link', { name: /Tạo phiếu bù trừ/ })).toHaveAttribute('href', '/debt/offsets/new');
    expect(lastParams()).toMatchObject({ from: '2026-10-01', to: '2026-10-31', status: 'Active', page: 1 });

    fireEvent.change(screen.getByLabelText('Trạng thái'), { target: { value: 'All' } });
    await waitFor(() => expect(lastParams()).toMatchObject({ status: 'All' }));

    // Partners that are both customer and supplier.
    const user = userEvent.setup();
    await user.click(screen.getByRole('combobox', { name: 'Đối tượng' }));
    await user.click(await screen.findByRole('option', { name: /DT01/ }));
    await waitFor(() => expect(lastParams()).toMatchObject({ partnerId: P1, status: 'All' }));
    expect(mocks.partnerSearch).toHaveBeenLastCalledWith(undefined, expect.any(String), {
      bothRoles: true,
      includeInactive: true,
    });
    expect(screen.getByTestId('location').textContent).toContain('partnerId=' + P1);

    await user.click(screen.getByRole('button', { name: 'Bỏ chọn đối tượng' }));
    await waitFor(() => expect(lastParams().partnerId).toBeUndefined());
  });

  it('hides create without debt_offset.create', () => {
    setPermissions(['debt_offset.view']);
    renderPage();

    expect(screen.queryByRole('link', { name: /Tạo phiếu bù trừ/ })).not.toBeInTheDocument();
  });
});
