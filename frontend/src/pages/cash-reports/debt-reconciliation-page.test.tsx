import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { DebtReconciliationPage } from './debt-reconciliation-page';
import type {
  DebtLedgerReport,
  DebtReconciliationParams,
  DebtReconciliationReport,
} from '@/features/cash-reports/types';
import type { Tt133ExportForm } from '@/features/tt133-export/types';
import { periodPreset } from '@/lib/period-presets';

const exportButtonProps: { forms: Tt133ExportForm[]; disabled?: boolean; onSelect?: unknown } = { forms: [] };
vi.mock('@/components/tt133-export/tt133-export-button', () => ({
  Tt133ExportButton: (props: typeof exportButtonProps) => {
    exportButtonProps.forms = props.forms;
    exportButtonProps.disabled = props.disabled;
    exportButtonProps.onSelect = props.onSelect;
    return <div data-testid="tt133-export" data-disabled={String(!!props.disabled)} />;
  },
}));

const reportState: { data: DebtReconciliationReport | undefined } = { data: undefined };
const useDebtReconciliationMock = vi.fn((params: DebtReconciliationParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useDebtReconciliation: (params: DebtReconciliationParams, options?: { enabled?: boolean }) =>
    useDebtReconciliationMock(params, options),
}));

const useDebtPartnerSearchMock = vi.fn(
  (side: string | undefined, keyword: string, options?: { includeInactive?: boolean }) => {
    void side;
    void keyword;
    void options;
    return { data: [], isLoading: false, isError: false };
  },
);
vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (side: string | undefined, keyword: string, options?: { includeInactive?: boolean }) =>
    useDebtPartnerSearchMock(side, keyword, options),
}));

const ledger = (side: 'Receivable' | 'Payable', code: string): DebtLedgerReport => ({
  side,
  scope: 'Branch',
  partnerId: 'p1',
  partnerCode: 'KH01',
  partnerName: 'Công ty ABC',
  from: '2026-10-01',
  to: '2026-10-31',
  openingBalance: 0,
  openingDebit: 0,
  openingCredit: 0,
  rows: [
    {
      entryId: `e-${code}`, postedAt: '2026-10-02T02:00:00Z', docDate: '2026-10-02', docCode: code,
      sourceType: side === 'Receivable' ? 'StockOut' : 'StockIn', sourceId: `v-${code}`, description: 'Giao dịch',
      increase: 800_000, decrease: 0, runningBalance: 800_000,
    },
  ],
  totalIncrease: 800_000,
  totalDecrease: 0,
  closingBalance: 800_000,
  closingDebit: side === 'Receivable' ? 800_000 : 0,
  closingCredit: side === 'Receivable' ? 0 : 800_000,
});

const report = (over: Partial<DebtReconciliationReport> = {}): DebtReconciliationReport => ({
  scope: 'Branch',
  partnerId: 'p1',
  partnerCode: 'KH01',
  partnerName: 'Công ty ABC',
  taxCode: '0312345678',
  address: '12 Lê Lợi, Quận 1',
  branchName: 'Chi nhánh Hà Nội',
  from: '2026-10-01',
  to: '2026-10-31',
  sides: [ledger('Receivable', 'PX00001')],
  netBalance: null,
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
      <DebtReconciliationPage />
      <LocationDisplay />
    </MemoryRouter>,
  );
}

const TWO_SIDES = () => [ledger('Receivable', 'PX00001'), ledger('Payable', 'PN00001')];

describe('DebtReconciliationPage', () => {
  beforeEach(() => {
    useDebtReconciliationMock.mockClear();
    useDebtPartnerSearchMock.mockClear();
    reportState.data = report();
    setPermissions(['stock_out.view', 'stock_in.view']);
  });

  it('asks for a partner when none is in the url', () => {
    reportState.data = undefined;
    renderAt('/reports/debt-reconciliation');

    expect(screen.getByText('Chọn đối tượng để xem biên bản đối chiếu')).toBeInTheDocument();
    expect(useDebtReconciliationMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ partnerId: '' }),
      { enabled: true },
    );
  });

  it('renders the header and one section for a single side without a net line', () => {
    renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

    expect(useDebtReconciliationMock).toHaveBeenLastCalledWith(
      { partnerId: 'p1', from: '2026-10-01', to: '2026-10-31', scope: 'Branch' },
      { enabled: true },
    );
    expect(screen.getByText('BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ')).toBeInTheDocument();
    expect(screen.getByText('Chi nhánh Hà Nội')).toBeInTheDocument();
    expect(screen.getByText('Từ ngày 01/10/2026 đến ngày 31/10/2026')).toBeInTheDocument();
    expect(screen.getByText('KH01 - Công ty ABC', { selector: 'p' })).toBeInTheDocument();
    expect(screen.getByText(/0312345678/)).toBeInTheDocument();
    expect(screen.getByText(/12 Lê Lợi, Quận 1/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Phải thu' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 2, name: 'Phải trả' })).not.toBeInTheDocument();
    const row = screen.getByRole('link', { name: 'PX00001' }).closest('tr')!;
    expect(within(row).getByText('Giao dịch')).toBeInTheDocument();
    expect(screen.queryByText(/Bù trừ hai bên/)).not.toBeInTheDocument();
    expect(screen.getByText('In biên bản: Đợt 3.')).toBeInTheDocument();
  });

  it('rejects an inverted range and disables the request', () => {
    renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-31&to=2026-10-01');

    expect(screen.getByText('Từ ngày không được sau Đến ngày')).toBeInTheDocument();
    expect(useDebtReconciliationMock).toHaveBeenLastCalledWith(expect.anything(), { enabled: false });
  });

  it('shows both sides and who owes after the offset', () => {
    reportState.data = report({ sides: TWO_SIDES(), netBalance: 600_000 });
    renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

    expect(screen.getByRole('heading', { level: 2, name: 'Phải thu' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Phải trả' })).toBeInTheDocument();
    expect(screen.getByText('Bù trừ hai bên: Đối tượng còn nợ 600.000')).toBeInTheDocument();
  });

  it('shows the company owing when the net is negative', () => {
    reportState.data = report({ sides: TWO_SIDES(), netBalance: -50_000 });
    renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

    expect(screen.getByText('Bù trừ hai bên: Công ty còn nợ 50.000')).toBeInTheDocument();
  });

  it('shows that nobody owes when the net is zero', () => {
    reportState.data = report({ sides: TWO_SIDES(), netBalance: 0 });
    renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

    expect(screen.getByText('Bù trừ hai bên: Hai bên không còn nợ')).toBeInTheDocument();
  });

  it('choosing Toàn công ty puts scope=Company in the url and the hook params', async () => {
    setPermissions(['stock_out.view', 'stock_in.view', 'branches.access_all']);
    const user = userEvent.setup();
    renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

    await user.click(screen.getByRole('combobox', { name: 'Phạm vi' }));
    await user.click(screen.getByRole('option', { name: 'Toàn công ty' }));

    expect(useDebtReconciliationMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ scope: 'Company' }),
      { enabled: true },
    );
    expect(screen.getByTestId('location')).toHaveTextContent('scope=Company');
  });

  it('shows Toàn công ty in place of the branch name when it is null', () => {
    reportState.data = report({ scope: 'Company', branchName: null });
    renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

    expect(screen.getByText('Toàn công ty', { selector: 'p' })).toBeInTheDocument();
  });

  describe('TT133 export', () => {
    it('is disabled until a partner is chosen', () => {
      reportState.data = undefined;
      renderAt('/reports/debt-reconciliation?from=2026-10-01&to=2026-10-31');

      expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'true');
    });

    it('offers the record of the chosen partner and downloads directly', () => {
      renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

      expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'false');
      expect(exportButtonProps.forms).toEqual([
        {
          code: 'DoiChieuCongNo_KH01',
          report: 'debt-reconciliation',
          params: { partnerId: 'p1', from: '2026-10-01', to: '2026-10-31', scope: 'Branch' },
          fileBase: 'DoiChieuCongNo_KH01_20261001_20261031',
        },
      ]);
      expect(exportButtonProps.onSelect).toBeUndefined();
    });

    it('an invalid range disables the button', () => {
      renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-31&to=2026-10-01');

      expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'true');
    });

    it('the preset Tháng trước sets the previous month', async () => {
      const user = userEvent.setup();
      renderAt('/reports/debt-reconciliation?partnerId=p1&from=2026-10-01&to=2026-10-31');

      await user.click(screen.getByRole('combobox', { name: 'Kỳ' }));
      await user.click(screen.getByRole('option', { name: 'Tháng trước' }));

      const last = periodPreset('lastMonth');
      expect(screen.getByLabelText('Từ ngày')).toHaveValue(last.from);
      expect(screen.getByLabelText('Đến ngày')).toHaveValue(last.to);
    });
  });
});
