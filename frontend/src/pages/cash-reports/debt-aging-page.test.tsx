import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Link, MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { todayYmd } from '@/lib/vn-datetime';
import { DebtAgingPage } from './debt-aging-page';
import type { DebtAgingParams, DebtAgingReport } from '@/features/cash-reports/types';

const reportState: { data: DebtAgingReport | undefined } = { data: undefined };
const useDebtAgingMock = vi.fn((params: DebtAgingParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useDebtAging: (params: DebtAgingParams, options?: { enabled?: boolean }) => useDebtAgingMock(params, options),
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

const ERROR_TEXT = 'Mốc không hợp lệ: các số nguyên dương tăng dần, cách nhau bởi dấu phẩy, tối đa 6 mốc';

const report = (over: Partial<DebtAgingReport> = {}): DebtAgingReport => ({
  side: 'Receivable',
  at: '2026-10-31',
  buckets: [
    { from: 0, to: 30 },
    { from: 31, to: 60 },
    { from: 61, to: 90 },
    { from: 91, to: null },
  ],
  rows: [
    {
      partnerId: 'p1', partnerCode: 'KH01', partnerName: 'Công ty A',
      amounts: [100_000, 200_000, 300_000, 400_000], totalOpen: 1_000_000, unallocated: 50_000, balance: 950,
    },
  ],
  totals: {
    amounts: [111_111, 222_222, 333_333, 444_444], totalOpen: 1_111_110, unallocated: 55_000, balance: 1_055_110,
  },
  ...over,
});

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function LocationDisplay() {
  const loc = useLocation();
  return <div data-testid="location">{loc.pathname + loc.search}</div>;
}

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <DebtAgingPage />
      <LocationDisplay />
    </MemoryRouter>,
  );
}

describe('DebtAgingPage', () => {
  beforeEach(() => {
    useDebtAgingMock.mockClear();
    useDebtPartnerSearchMock.mockClear();
    reportState.data = report();
    setPermissions(['reports.debt']);
  });

  it('defaults to the standard buckets and labels the columns from the response', () => {
    renderAt('/reports/debt-aging');

    expect(useDebtAgingMock).toHaveBeenLastCalledWith(
      { side: 'Receivable', at: todayYmd(), partnerId: undefined, buckets: '30,60,90' },
      { enabled: true },
    );
    for (const label of ['0–30', '31–60', '61–90', '> 90']) {
      expect(screen.getByRole('columnheader', { name: label })).toBeInTheDocument();
    }
  });

  it('typing valid buckets and blurring writes them to the url', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-aging');

    const input = screen.getByLabelText('Mốc tuổi nợ');
    await user.clear(input);
    await user.type(input, '15, 45');
    await user.tab();

    expect(screen.getByTestId('location')).toHaveTextContent('buckets=15%2C45');
    expect(useDebtAgingMock).toHaveBeenLastCalledWith(expect.objectContaining({ buckets: '15,45' }), { enabled: true });
  });

  it('invalid typed buckets show the error and keep the previous url value', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-aging?buckets=20,40');

    const input = screen.getByLabelText('Mốc tuổi nợ');
    await user.clear(input);
    await user.type(input, '45,15');
    await user.tab();

    expect(screen.getByText(ERROR_TEXT)).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('buckets=20,40');
  });

  it('re-syncs the input and clears the typed error when the url buckets change under a mounted page', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter initialEntries={['/reports/debt-aging?buckets=15,45']}>
        <DebtAgingPage />
        <Link to="/reports/debt-aging">Tuổi nợ</Link>
      </MemoryRouter>,
    );

    const input = screen.getByLabelText('Mốc tuổi nợ');
    await user.clear(input);
    await user.type(input, '45,15');
    await user.tab();
    expect(screen.getByText(ERROR_TEXT)).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: 'Tuổi nợ' }));

    expect(screen.getByLabelText('Mốc tuổi nợ')).toHaveValue('30,60,90');
    expect(screen.queryByText(ERROR_TEXT)).not.toBeInTheDocument();
  });

  it('row click opens the documents report for that partner', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-aging?at=2026-10-31');

    await user.click(screen.getByText('KH01').closest('tr')!);

    expect(screen.getByTestId('location')).toHaveTextContent(
      '/reports/debt-documents?side=Receivable&at=2026-10-31&partnerId=p1',
    );
  });

  it('renders the totals row', () => {
    renderAt('/reports/debt-aging');

    const footer = screen.getByText('Tổng cộng').closest('tr')!;
    expect(within(footer).getByText('111.111')).toBeInTheDocument();
    expect(within(footer).getByText('444.444')).toBeInTheDocument();
  });

  it('buckets from the url that are invalid show the error and disable the request', () => {
    renderAt('/reports/debt-aging?buckets=45,15');

    expect(screen.getByText(ERROR_TEXT)).toBeInTheDocument();
    expect(useDebtAgingMock).toHaveBeenLastCalledWith(expect.anything(), { enabled: false });
  });

  it('does not show cached report data when the url buckets are invalid', () => {
    reportState.data = report();
    renderAt('/reports/debt-aging?buckets=45,15');

    expect(screen.getByText(ERROR_TEXT)).toBeInTheDocument();
    expect(screen.queryByText('KH01')).not.toBeInTheDocument();
  });

  it('shows the empty message', () => {
    reportState.data = report({ rows: [] });
    renderAt('/reports/debt-aging');

    expect(screen.getByText('Không có công nợ tại ngày này')).toBeInTheDocument();
  });
});
