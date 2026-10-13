import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { DebtLedgerPage } from './debt-ledger-page';
import type { DebtLedgerParams, DebtLedgerReport } from '@/features/cash-reports/types';

const ledgerState: { data: DebtLedgerReport | undefined } = { data: undefined };
const useDebtLedgerMock = vi.fn((params: DebtLedgerParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: ledgerState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useDebtLedger: (params: DebtLedgerParams, options?: { enabled?: boolean }) => useDebtLedgerMock(params, options),
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

const ledger = (over: Partial<DebtLedgerReport> = {}): DebtLedgerReport => ({
  side: 'Payable',
  scope: 'Branch',
  partnerId: 's1',
  partnerCode: 'NCC01',
  partnerName: 'Thép Hòa Phát',
  from: '2026-10-01',
  to: '2026-10-31',
  openingBalance: 1_000_000,
  openingDebit: 0,
  openingCredit: 1_000_000,
  rows: [
    {
      entryId: 'e1', postedAt: '2026-10-02T02:00:00Z', docDate: '2026-10-02', docCode: 'PN00001',
      sourceType: 'StockIn', sourceId: 'v1', description: 'Nhập mua tôn',
      increase: 2_500_000, decrease: 0, runningBalance: 3_500_000,
    },
    {
      entryId: 'e2', postedAt: '2026-10-05T02:00:00Z', docDate: '2026-10-05', docCode: 'PC00001',
      sourceType: 'Payment', sourceId: 'c1', description: 'Trả tiền NCC',
      increase: 0, decrease: 4_000_000, runningBalance: -500_000,
    },
    {
      entryId: 'e3', postedAt: '2026-10-06T02:00:00Z', docDate: '2026-10-06', docCode: 'BT00001',
      sourceType: 'Offset', sourceId: 'o1', description: null,
      increase: 0, decrease: 100_000, runningBalance: -600_000,
    },
  ],
  totalIncrease: 2_500_000,
  totalDecrease: 4_100_000,
  closingBalance: -600_000,
  closingDebit: 600_000,
  closingCredit: 0,
  ...over,
});

const ALL_LINK_PERMISSIONS = [
  'stock_in.view', 'stock_out.view', 'receipts.view', 'payments.view', 'debt_offset.view', 'debt.opening_balance',
];

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
      <DebtLedgerPage />
      <LocationDisplay />
    </MemoryRouter>,
  );
}

describe('DebtLedgerPage', () => {
  beforeEach(() => {
    useDebtLedgerMock.mockClear();
    useDebtPartnerSearchMock.mockClear();
    ledgerState.data = ledger();
    setPermissions(ALL_LINK_PERMISSIONS);
  });

  it('reads params from the url and renders running balance', () => {
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31');

    expect(useDebtLedgerMock).toHaveBeenLastCalledWith(
      { side: 'Payable', partnerId: 's1', from: '2026-10-01', to: '2026-10-31', scope: 'Branch' },
      { enabled: true },
    );
    // The ledger of a partner made inactive since must stay reachable.
    expect(useDebtPartnerSearchMock).toHaveBeenLastCalledWith('Payable', expect.any(String), { includeInactive: true });
    expect(screen.getByRole('combobox', { name: 'Phía' })).toHaveTextContent('Phải trả');
    expect(screen.getByRole('combobox', { name: 'Đối tượng' })).toHaveValue('NCC01 - Thép Hòa Phát');
    expect(screen.getByLabelText('Từ ngày')).toHaveValue('2026-10-01');
    expect(screen.getByLabelText('Đến ngày')).toHaveValue('2026-10-31');

    for (const name of ['Ngày', 'Số CT', 'Loại CT', 'Diễn giải', 'Lý do', 'Hạn TT', 'Tăng', 'Giảm', 'Số dư']) {
      expect(screen.getByRole('columnheader', { name })).toBeInTheDocument();
    }

    const opening = screen.getByText('Số dư đầu kỳ').closest('tr')!;
    expect(within(opening).getByText('Có 1.000.000')).toBeInTheDocument();

    const stockIn = screen.getByRole('link', { name: 'PN00001' }).closest('tr')!;
    expect(within(stockIn).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      '02/10/2026', 'PN00001', 'Phiếu nhập', 'Nhập mua tôn', '', '', '2.500.000', '', 'Có 3.500.000',
    ]);
    const payment = screen.getByRole('link', { name: 'PC00001' }).closest('tr')!;
    const paymentCells = within(payment).getAllByRole('cell');
    expect(paymentCells[7]).toHaveTextContent('4.000.000');
    // Payable: positive is Dư Có, negative is Dư Nợ (P24), like the opening and closing balances.
    expect(paymentCells[8]).toHaveTextContent('Nợ 500.000');
    expect(paymentCells[8]).toHaveClass('text-right', 'tabular-nums');

    const totals = screen.getByText('Cộng phát sinh').closest('tr')!;
    expect(within(totals).getByText('2.500.000')).toBeInTheDocument();
    expect(within(totals).getByText('4.100.000')).toBeInTheDocument();

    // Payable with a negative balance shows as Dư Nợ (P24); the server splits it.
    const closing = screen.getByText('Số dư cuối kỳ').closest('tr')!;
    expect(within(closing).getByText('Nợ 600.000')).toBeInTheDocument();
  });

  it('splits the running balance of a receivable ledger into Nợ and Có', () => {
    ledgerState.data = ledger({
      side: 'Receivable',
      rows: [
        {
          entryId: 'e1', postedAt: '2026-10-02T02:00:00Z', docDate: '2026-10-02', docCode: 'PX00001',
          sourceType: 'StockOut', sourceId: 'v1', description: null, increase: 700_000, decrease: 0, runningBalance: 700_000,
        },
        {
          entryId: 'e2', postedAt: '2026-10-03T02:00:00Z', docDate: '2026-10-03', docCode: 'PT00001',
          sourceType: 'Receipt', sourceId: 'c1', description: null, increase: 0, decrease: 900_000, runningBalance: -200_000,
        },
      ],
    });
    renderAt('/reports/debt-ledger?side=Receivable&partnerId=s1&from=2026-10-01&to=2026-10-31');

    const cells = (code: string) => within(screen.getByText(code).closest('tr')!).getAllByRole('cell');
    expect(cells('PX00001')[8]).toHaveTextContent('Nợ 700.000');
    expect(cells('PT00001')[8]).toHaveTextContent('Có 200.000');
  });

  it('asks for a partner when none is in the url', () => {
    ledgerState.data = undefined;
    renderAt('/reports/debt-ledger?side=Receivable');

    expect(useDebtLedgerMock).toHaveBeenLastCalledWith(expect.objectContaining({ partnerId: '', side: 'Receivable' }), {
      enabled: true,
    });
    expect(screen.getByText('Chọn đối tượng để xem sổ chi tiết công nợ')).toBeInTheDocument();
  });

  it('document codes link to their screens', () => {
    ledgerState.data = ledger({
      rows: [
        ...ledger().rows,
        {
          entryId: 'e4', postedAt: '2026-10-07T02:00:00Z', docDate: '2026-10-07', docCode: 'PX00001',
          sourceType: 'StockOut', sourceId: 'v2', description: null, increase: 0, decrease: 0, runningBalance: -600_000,
        },
        {
          entryId: 'e5', postedAt: '2026-10-08T02:00:00Z', docDate: '2026-10-08', docCode: 'PT00001',
          sourceType: 'Receipt', sourceId: 'c2', description: null, increase: 0, decrease: 0, runningBalance: -600_000,
        },
        {
          entryId: 'e6', postedAt: '2026-10-09T02:00:00Z', docDate: '2026-10-09', docCode: 'NDK',
          sourceType: 'Opening', sourceId: 'od1', description: null, increase: 0, decrease: 0, runningBalance: -600_000,
        },
      ],
    });
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31');

    expect(screen.getByRole('link', { name: 'PN00001' })).toHaveAttribute('href', '/stock-in/v1');
    expect(screen.getByRole('link', { name: 'PX00001' })).toHaveAttribute('href', '/stock-out/v2');
    expect(screen.getByRole('link', { name: 'PT00001' })).toHaveAttribute('href', '/receipts/c2');
    expect(screen.getByRole('link', { name: 'PC00001' })).toHaveAttribute('href', '/payments/c1');
    expect(screen.getByRole('link', { name: 'BT00001' })).toHaveAttribute('href', '/debt/offsets/o1');
    expect(screen.getByRole('link', { name: 'NDK' })).toHaveAttribute('href', '/debt/opening');
  });

  it('document codes without the permission of their screen are plain text', () => {
    ledgerState.data = ledger({
      rows: [
        ...ledger().rows,
        {
          entryId: 'e6', postedAt: '2026-10-09T02:00:00Z', docDate: '2026-10-09', docCode: 'NDK',
          sourceType: 'Opening', sourceId: 'od1', description: null, increase: 0, decrease: 0, runningBalance: -600_000,
        },
      ],
    });
    setPermissions(['stock_in.view', 'payments.view']);
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31');

    expect(screen.getByRole('link', { name: 'PN00001' })).toHaveAttribute('href', '/stock-in/v1');
    expect(screen.getByRole('link', { name: 'PC00001' })).toHaveAttribute('href', '/payments/c1');
    for (const code of ['BT00001', 'NDK']) {
      expect(screen.getByText(code)).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: code })).not.toBeInTheDocument();
    }
  });

  it('shows the reason, the due date and the branch code of a row', () => {
    ledgerState.data = ledger({
      scope: 'Company',
      rows: [
        {
          entryId: 'e1', postedAt: '2026-10-02T02:00:00Z', docDate: '2026-10-02', docCode: 'PN00001',
          sourceType: 'StockIn', sourceId: 'v1', description: 'Bán hàng tháng 10', reasonName: 'Bán hàng',
          dueDate: '2026-10-20', branchCode: 'HN', increase: 2_500_000, decrease: 0, runningBalance: 3_500_000,
        },
        {
          entryId: 'e2', postedAt: '2026-10-05T02:00:00Z', docDate: '2026-10-05', docCode: 'PC00001',
          sourceType: 'Payment', sourceId: 'c1', description: 'Trả tiền NCC',
          increase: 0, decrease: 4_000_000, runningBalance: -500_000,
        },
      ],
    });
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31');

    const withBranch = within(screen.getByText('PN00001').closest('tr')!).getAllByRole('cell');
    expect(withBranch[3]).toHaveTextContent('[HN] Bán hàng tháng 10');
    expect(withBranch[4]).toHaveTextContent('Bán hàng');
    expect(withBranch[5]).toHaveTextContent('20/10/2026');
    const without = within(screen.getByText('PC00001').closest('tr')!).getAllByRole('cell');
    expect(without[3].textContent).toBe('Trả tiền NCC');
    expect(without[5].textContent).toBe('');
  });

  it('passes scope=Company to the hook only for holders of branches.access_all', () => {
    setPermissions([...ALL_LINK_PERMISSIONS, 'branches.access_all']);
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31&scope=Company');

    expect(useDebtLedgerMock).toHaveBeenLastCalledWith(expect.objectContaining({ scope: 'Company' }), { enabled: true });
    expect(screen.getByRole('combobox', { name: 'Phạm vi' })).toHaveTextContent('Toàn công ty');
  });

  it('ignores scope=Company in the url without the permission', () => {
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31&scope=Company');

    expect(useDebtLedgerMock).toHaveBeenLastCalledWith(expect.objectContaining({ scope: 'Branch' }), { enabled: true });
    expect(screen.queryByRole('combobox', { name: 'Phạm vi' })).not.toBeInTheDocument();
  });

  it('choosing Toàn công ty puts scope=Company in the url and the hook params', async () => {
    setPermissions([...ALL_LINK_PERMISSIONS, 'branches.access_all']);
    const user = userEvent.setup();
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31');

    await user.click(screen.getByRole('combobox', { name: 'Phạm vi' }));
    await user.click(screen.getByRole('option', { name: 'Toàn công ty' }));

    expect(useDebtLedgerMock).toHaveBeenLastCalledWith(expect.objectContaining({ scope: 'Company' }), { enabled: true });
    expect(screen.getByTestId('location')).toHaveTextContent('scope=Company');
  });
});
