import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import { DebtBySalespersonPage } from './debt-by-salesperson-page';
import { formatSignedBalance } from './document-link';
import type { DebtBySalespersonParams, DebtBySalespersonReport } from '@/features/cash-reports/types';

const reportState: { data: DebtBySalespersonReport | undefined } = { data: undefined };
const useDebtBySalespersonMock = vi.fn((params: DebtBySalespersonParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useDebtBySalesperson: (params: DebtBySalespersonParams, options?: { enabled?: boolean }) =>
    useDebtBySalespersonMock(params, options),
}));

const useDebtPartnerSearchMock = vi.fn((side: string, keyword: string, options?: { includeInactive?: boolean }) => {
  void side;
  void keyword;
  void options;
  return { data: [], isLoading: false, isError: false };
});
vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (side: string, keyword: string, options?: { includeInactive?: boolean }) =>
    useDebtPartnerSearchMock(side, keyword, options),
}));

const report = (over: Partial<DebtBySalespersonReport> = {}): DebtBySalespersonReport => ({
  side: 'Receivable',
  from: '2026-10-01',
  to: '2026-10-31',
  rows: [
    {
      ownerUserId: 'u1', ownerName: 'Nguyễn Văn A', openingOpen: 1_000_000, increase: 2_000_000, settled: 500_000,
      closingOpen: 2_500_000, closingOverdue: 700_000,
    },
    {
      ownerUserId: null, ownerName: null, openingOpen: 100_000, increase: 200_000, settled: 50_000,
      closingOpen: 250_000, closingOverdue: 0,
    },
  ],
  totals: {
    openingOpen: 1_100_000, increase: 2_200_000, settled: 550_000, closingOpen: 2_750_000, closingOverdue: 700_000,
  },
  unallocated: { opening: 30_000, closing: 40_000 },
  openingBalance: 1_070_000,
  closingBalance: -2_710_000,
  ...over,
});

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.search}</div>;
}

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <DebtBySalespersonPage />
      <LocationProbe />
    </MemoryRouter>,
  );
}

describe('DebtBySalespersonPage', () => {
  beforeEach(() => {
    useDebtBySalespersonMock.mockClear();
    useDebtPartnerSearchMock.mockClear();
    reportState.data = report();
    setPermissions(['reports.debt']);
  });

  it('defaults to receivable and the current month', () => {
    renderAt('/reports/debt-by-salesperson');

    expect(useDebtBySalespersonMock).toHaveBeenLastCalledWith(
      { side: 'Receivable', from: firstDayOfMonthYmd(), to: todayYmd(), partnerId: undefined },
      { enabled: true },
    );
  });

  it('renders the rows, "Không xác định" for a null owner and a red overdue amount', () => {
    renderAt('/reports/debt-by-salesperson');

    const first = screen.getByText('Nguyễn Văn A').closest('tr')!;
    expect(first).toHaveTextContent('1.000.000');
    expect(first).toHaveTextContent('2.500.000');
    const overdue = screen.getAllByText('700.000').find((el) => el.tagName === 'TD' && el.closest('tbody'))!;
    expect(overdue).toHaveClass('text-destructive');
    const unknown = screen.getByText('Không xác định').closest('tr')!;
    expect(unknown).toHaveTextContent('250.000');
    expect(screen.getByText('Đã thu / đối trừ')).toBeInTheDocument();
    expect(screen.getByText('Theo người lập chứng từ.')).toBeInTheDocument();
  });

  it('shows the Cộng, Chưa đối trừ and Số dư công nợ rows', () => {
    renderAt('/reports/debt-by-salesperson');

    const total = screen.getByText('Cộng').closest('tr')!;
    expect(total).toHaveTextContent('1.100.000');
    expect(total).toHaveTextContent('2.200.000');
    expect(total).toHaveTextContent('550.000');
    expect(total).toHaveTextContent('2.750.000');

    const unallocated = screen.getByText('Chưa đối trừ (khách trả trước)').closest('tr')!;
    expect(unallocated).toHaveTextContent('-30.000');
    expect(unallocated).toHaveTextContent('-40.000');

    const balance = screen.getByText('Số dư công nợ').closest('tr')!;
    expect(balance).toHaveTextContent(formatSignedBalance('Receivable', 1_070_000));
    expect(balance).toHaveTextContent(formatSignedBalance('Receivable', -2_710_000));
  });

  it('from after to shows the error and disables the request', () => {
    renderAt('/reports/debt-by-salesperson?from=2026-10-31&to=2026-10-01');

    expect(screen.getByText('Từ ngày không được sau Đến ngày')).toBeInTheDocument();
    expect(useDebtBySalespersonMock).toHaveBeenLastCalledWith(expect.anything(), { enabled: false });
  });

  it('uses the payable wording for the Payable side', () => {
    reportState.data = report({ side: 'Payable' });
    renderAt('/reports/debt-by-salesperson?side=Payable');

    expect(screen.getByText('Đã trả / đối trừ')).toBeInTheDocument();
    expect(screen.getByText('Chưa đối trừ (ứng trước NCC)')).toBeInTheDocument();
    expect(screen.queryByText('Đã thu / đối trừ')).toBeNull();
    expect(screen.queryByText('Chưa đối trừ (khách trả trước)')).toBeNull();
  });

  it('passes the partner from the url and the clear entry removes it', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-by-salesperson?partnerId=p1');

    expect(useDebtBySalespersonMock).toHaveBeenLastCalledWith(expect.objectContaining({ partnerId: 'p1' }), {
      enabled: true,
    });
    await user.click(screen.getByRole('button', { name: 'Tất cả đối tượng' }));

    expect(screen.getByTestId('location')).not.toHaveTextContent('partnerId');
  });

  it('changing Phía removes partnerId from the url', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-by-salesperson?partnerId=p1');

    await user.click(screen.getByRole('combobox', { name: 'Phía' }));
    await user.click(await screen.findByRole('option', { name: 'Phải trả' }));

    const location = screen.getByTestId('location');
    expect(location).toHaveTextContent('side=Payable');
    expect(location).not.toHaveTextContent('partnerId');
  });

  it('changing Từ ngày updates the url', () => {
    renderAt('/reports/debt-by-salesperson');

    fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '2026-09-01' } });

    expect(screen.getByTestId('location')).toHaveTextContent('from=2026-09-01');
  });

  const zeroTotals = { openingOpen: 0, increase: 0, settled: 0, closingOpen: 0, closingOverdue: 0 };

  it('shows the empty message and no summary rows when everything is zero', () => {
    reportState.data = report({
      rows: [], totals: zeroTotals, unallocated: { opening: 0, closing: 0 }, openingBalance: 0, closingBalance: 0,
    });
    renderAt('/reports/debt-by-salesperson');

    expect(screen.getByText('Không có công nợ trong kỳ này')).toBeInTheDocument();
    expect(screen.queryByText('Cộng')).toBeNull();
    expect(screen.queryByText('Số dư công nợ')).toBeNull();
  });

  it('still shows the summary rows when there are no rows but an unallocated amount exists', () => {
    reportState.data = report({
      rows: [], totals: zeroTotals, unallocated: { opening: 5_000_000, closing: 5_000_000 },
      openingBalance: -5_000_000, closingBalance: -5_000_000,
    });
    renderAt('/reports/debt-by-salesperson');

    expect(screen.queryByText('Không có công nợ trong kỳ này')).toBeNull();
    expect(screen.getByText('Cộng')).toBeInTheDocument();
    expect(screen.getByText('Chưa đối trừ (khách trả trước)').closest('tr')).toHaveTextContent('-5.000.000');
    expect(screen.getByText('Số dư công nợ')).toBeInTheDocument();
  });

  it('shows 0 and not -0 in the unallocated row when it is zero', () => {
    reportState.data = report({ unallocated: { opening: 0, closing: 0 } });
    renderAt('/reports/debt-by-salesperson');

    const cells = screen.getByText('Chưa đối trừ (khách trả trước)').closest('tr')!.querySelectorAll('td');
    expect(cells[1]).toHaveTextContent(/^0$/);
    expect(cells[cells.length - 2]).toHaveTextContent(/^0$/);
    expect(screen.getByText('Chưa đối trừ (khách trả trước)').closest('tr')!.textContent).not.toContain('-0');
  });
});
