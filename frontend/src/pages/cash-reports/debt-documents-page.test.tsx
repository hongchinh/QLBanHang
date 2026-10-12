import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { todayYmd } from '@/lib/vn-datetime';
import { DebtDocumentsPage } from './debt-documents-page';
import { formatSignedBalance } from './document-link';
import type { DebtDocumentsParams, DebtDocumentsReport } from '@/features/cash-reports/types';

const reportState: { data: DebtDocumentsReport | undefined } = { data: undefined };
const useDebtDocumentsMock = vi.fn((params: DebtDocumentsParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useDebtDocuments: (params: DebtDocumentsParams, options?: { enabled?: boolean }) =>
    useDebtDocumentsMock(params, options),
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

const report = (over: Partial<DebtDocumentsReport> = {}): DebtDocumentsReport => ({
  side: 'Receivable',
  at: '2026-10-31',
  groups: [
    {
      partnerId: 'p1', partnerCode: 'KH01', partnerName: 'Công ty A',
      items: [
        {
          entryId: 'e1', sourceType: 'StockOut', sourceId: 'v1', docCode: 'PX00001', docDate: '2026-09-20',
          dueDate: '2026-09-20', description: 'Bán tôn', amount: 2_000_000, settled: 500_000, remaining: 1_500_000,
          daysOutstanding: 41, daysOverdue: 41,
        },
      ],
      unallocated: [
        {
          entryId: 'e2', sourceType: 'Receipt', sourceId: 'c1', docCode: 'PT00001', docDate: '2026-10-05',
          description: 'Thu trước', amount: 300_000, settled: 0, remaining: 300_000,
        },
      ],
      totalRemaining: 1_500_000, totalUnallocated: 300_000, balance: 1500,
    },
  ],
  totalRemaining: 1_500_000, totalUnallocated: 300_000, totalBalance: 1500,
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
      <DebtDocumentsPage />
      <LocationDisplay />
    </MemoryRouter>,
  );
}

describe('DebtDocumentsPage', () => {
  beforeEach(() => {
    useDebtDocumentsMock.mockClear();
    useDebtPartnerSearchMock.mockClear();
    reportState.data = report();
    setPermissions(['stock_out.view', 'receipts.view']);
  });

  it('defaults to receivable as of today', () => {
    renderAt('/reports/debt-documents');

    expect(useDebtDocumentsMock).toHaveBeenLastCalledWith(
      { side: 'Receivable', at: todayYmd(), partnerId: undefined },
      undefined,
    );
  });

  it('reads params from the url', () => {
    renderAt('/reports/debt-documents?side=Payable&at=2026-10-31&partnerId=p1');

    expect(useDebtDocumentsMock).toHaveBeenLastCalledWith(
      { side: 'Payable', at: '2026-10-31', partnerId: 'p1' },
      undefined,
    );
    expect(screen.getByLabelText('Tại ngày')).toHaveValue('2026-10-31');
  });

  it('renders the item and unallocated tables with the balance line', () => {
    renderAt('/reports/debt-documents?side=Receivable&at=2026-10-31');

    expect(screen.getByRole('heading', { name: 'KH01 - Công ty A' })).toBeInTheDocument();
    expect(screen.getByText('Chứng từ còn nợ')).toBeInTheDocument();
    // Also the footer label, so match the table caption only.
    expect(screen.getAllByText('Chưa đối trừ').length).toBeGreaterThan(0);

    const row = screen.getByRole('link', { name: 'PX00001' }).closest('tr')!;
    expect(screen.getByRole('link', { name: 'PX00001' })).toHaveAttribute('href', '/stock-out/v1');
    // Số ngày nợ is 41 as well, so pick the Quá hạn cell (the last one).
    const cells = within(row).getAllByRole('cell');
    expect(cells[9]).toHaveTextContent('41');
    expect(cells[9]).toHaveClass('text-destructive');

    expect(screen.getByRole('link', { name: 'PT00001' })).toHaveAttribute('href', '/receipts/c1');
    expect(screen.getByText(`Số dư: ${formatSignedBalance('Receivable', 1500)}`)).toBeInTheDocument();
  });

  it('shows the empty message', () => {
    reportState.data = report({ groups: [], totalRemaining: 0, totalUnallocated: 0, totalBalance: 0 });
    renderAt('/reports/debt-documents');

    expect(screen.getByText('Không có chứng từ còn nợ tại ngày này')).toBeInTheDocument();
  });

  it('changing Tại ngày updates the url', () => {
    renderAt('/reports/debt-documents?at=2026-10-31');

    fireEvent.change(screen.getByLabelText('Tại ngày'), { target: { value: '2026-11-15' } });

    expect(screen.getByTestId('location')).toHaveTextContent('at=2026-11-15');
  });

  it('the clear entry removes partnerId from the url', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-documents?side=Receivable&at=2026-10-31&partnerId=p1');

    expect(screen.getByRole('combobox', { name: 'Đối tượng' })).toHaveValue('KH01 - Công ty A');
    await user.click(screen.getByRole('button', { name: 'Tất cả đối tượng' }));

    expect(screen.getByTestId('location')).not.toHaveTextContent('partnerId');
  });

  it('changing Phía removes partnerId from the url', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-documents?side=Receivable&at=2026-10-31&partnerId=p1');

    await user.click(screen.getByRole('combobox', { name: 'Phía' }));
    await user.click(await screen.findByRole('option', { name: 'Phải trả' }));

    const location = screen.getByTestId('location');
    expect(location).toHaveTextContent('side=Payable');
    expect(location).not.toHaveTextContent('partnerId');
  });
});
