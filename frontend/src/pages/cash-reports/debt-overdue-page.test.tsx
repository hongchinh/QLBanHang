import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { todayYmd } from '@/lib/vn-datetime';
import { DebtOverduePage } from './debt-overdue-page';
import type { DebtOverdueParams, DebtOverdueReport } from '@/features/cash-reports/types';

const reportState: { data: DebtOverdueReport | undefined } = { data: undefined };
const useDebtOverdueMock = vi.fn((params: DebtOverdueParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useDebtOverdue: (params: DebtOverdueParams, options?: { enabled?: boolean }) => useDebtOverdueMock(params, options),
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

const report = (over: Partial<DebtOverdueReport> = {}): DebtOverdueReport => ({
  side: 'Receivable',
  at: '2026-10-31',
  buckets: [
    { from: 1, to: 30 },
    { from: 31, to: 60 },
    { from: 61, to: null },
  ],
  groups: [
    {
      partnerId: 'p1',
      partnerCode: 'KH01',
      partnerName: 'Công ty A',
      items: [
        {
          entryId: 'e1', sourceType: 'StockOut', sourceId: 's1', docCode: 'PX-001', docDate: '2026-08-01',
          dueDate: '2026-09-01', description: 'Bán hàng', amount: 700_000, remaining: 600_000,
          daysOverdue: 40, bucketIndex: 1,
        },
        {
          entryId: 'e2', sourceType: 'StockOut', sourceId: 's2', docCode: 'PX-002', docDate: '2026-09-10',
          dueDate: '2026-10-10', description: null, amount: 250_000, remaining: 250_000,
          daysOverdue: 20, bucketIndex: 0,
        },
      ],
      amounts: [250_000, 600_000, 0],
      totalOverdue: 850_000,
      unallocated: 77_000,
    },
  ],
  totalAmounts: [250_000, 600_000, 0],
  totalOverdue: 850_000,
  totalUnallocated: 77_000,
  ...over,
});

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <DebtOverduePage />
    </MemoryRouter>,
  );
}

describe('DebtOverduePage', () => {
  beforeEach(() => {
    useDebtOverdueMock.mockClear();
    useDebtPartnerSearchMock.mockClear();
    reportState.data = report();
    setPermissions(['reports.debt']);
  });

  it('defaults to receivable, today and the standard buckets', () => {
    renderAt('/reports/debt-overdue');

    expect(useDebtOverdueMock).toHaveBeenLastCalledWith(
      { side: 'Receivable', at: todayYmd(), partnerId: undefined, buckets: '30,60,90' },
      { enabled: true },
    );
  });

  it('renders the bucket labels per row, the totals and the unallocated note', () => {
    renderAt('/reports/debt-overdue');

    const first = screen.getByText('PX-001').closest('tr')!;
    expect(first).toHaveTextContent('31–60');
    const second = screen.getByText('PX-002').closest('tr')!;
    expect(second).toHaveTextContent('1–30');
    expect(screen.getAllByText('Tổng quá hạn').length).toBeGreaterThan(0);
    expect(screen.getAllByText('850.000').length).toBeGreaterThan(0);
    expect(screen.getByText('Chưa đối trừ (tham khảo): 77.000')).toBeInTheDocument();
  });

  it('passes the partner from the url to the hook', () => {
    renderAt('/reports/debt-overdue?partnerId=p1');

    expect(useDebtOverdueMock).toHaveBeenLastCalledWith(expect.objectContaining({ partnerId: 'p1' }), {
      enabled: true,
    });
  });

  it('shows the empty message', () => {
    reportState.data = report({ groups: [], totalAmounts: [0, 0, 0], totalOverdue: 0, totalUnallocated: 0 });
    renderAt('/reports/debt-overdue');

    expect(screen.getByText('Không có khoản nợ quá hạn tại ngày này')).toBeInTheDocument();
  });

  it('buckets from the url that are invalid show the error and disable the request', () => {
    renderAt('/reports/debt-overdue?buckets=60,30');

    expect(screen.getByText(ERROR_TEXT)).toBeInTheDocument();
    expect(useDebtOverdueMock).toHaveBeenLastCalledWith(expect.anything(), { enabled: false });
    expect(screen.queryByText('PX-001')).not.toBeInTheDocument();
  });
});
