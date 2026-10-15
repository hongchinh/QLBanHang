import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { DebtBalancePage } from './debt-balance-page';
import type { DebtBalanceParams, DebtBalanceReport } from '@/features/cash-reports/types';
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

const reportState: { data: DebtBalanceReport | undefined } = { data: undefined };
const useDebtBalanceMock = vi.fn((params: DebtBalanceParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useDebtBalance: (params: DebtBalanceParams, options?: { enabled?: boolean }) => useDebtBalanceMock(params, options),
}));

const report = (over: Partial<DebtBalanceReport> = {}): DebtBalanceReport => ({
  side: 'Payable',
  scope: 'Branch',
  from: '2026-10-01',
  to: '2026-10-31',
  rows: [
    {
      partnerId: 's1', partnerCode: 'NCC01', partnerName: 'Thép Hòa Phát',
      openingDebit: 0, openingCredit: 1_000_000, increase: 2_500_000, decrease: 500_000,
      closingDebit: 0, closingCredit: 3_000_000,
    },
    {
      partnerId: 's2', partnerCode: 'NCC02', partnerName: 'Tôn Đông Á',
      openingDebit: 200_000, openingCredit: 0, increase: 0, decrease: 100_000,
      closingDebit: 300_000, closingCredit: 0,
    },
  ],
  totals: {
    partnerId: null, partnerCode: null, partnerName: null,
    openingDebit: 200_000, openingCredit: 1_000_000, increase: 2_500_000, decrease: 600_000,
    closingDebit: 300_000, closingCredit: 3_000_000,
  },
  ...over,
});

function LocationDisplay() {
  const loc = useLocation();
  return (
    <>
      <div data-testid="location">{loc.pathname + loc.search}</div>
      <div data-testid="state">{JSON.stringify(loc.state)}</div>
    </>
  );
}

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/reports/debt-balance" element={<DebtBalancePage />} />
        <Route path="/reports/debt-ledger" element={<div>ledger</div>} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>,
  );
}

describe('DebtBalancePage', () => {
  beforeEach(() => {
    useDebtBalanceMock.mockClear();
    reportState.data = report();
    useAuthStore.setState({
      user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions: ['branches.access_all'] },
    });
  });

  it('renders rows and totals with debit/credit columns', () => {
    renderAt('/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-31&search=NCC');

    expect(useDebtBalanceMock).toHaveBeenLastCalledWith(
      { side: 'Payable', from: '2026-10-01', to: '2026-10-31', search: 'NCC', scope: 'Branch' },
      { enabled: true },
    );
    expect(screen.getByRole('combobox', { name: 'Phía' })).toHaveTextContent('Phải trả');
    expect(screen.getByLabelText('Tìm kiếm')).toHaveValue('NCC');

    for (const name of [
      'Mã', 'Tên', 'Đầu kỳ Nợ', 'Đầu kỳ Có', 'Phát sinh tăng', 'Phát sinh giảm', 'Cuối kỳ Nợ', 'Cuối kỳ Có',
    ]) {
      expect(screen.getByRole('columnheader', { name })).toBeInTheDocument();
    }

    const first = screen.getByText('NCC01').closest('tr')!;
    const cells = within(first).getAllByRole('cell').map((c) => c.textContent);
    expect(cells).toEqual(['NCC01', 'Thép Hòa Phát', '', '1.000.000', '2.500.000', '500.000', '', '3.000.000']);
    const credit = within(first).getAllByRole('cell')[7];
    expect(credit).toHaveClass('text-right', 'tabular-nums');

    const second = screen.getByText('NCC02').closest('tr')!;
    expect(within(second).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      'NCC02', 'Tôn Đông Á', '200.000', '', '', '100.000', '300.000', '',
    ]);

    const totals = screen.getByText('Tổng cộng').closest('tr')!;
    expect(within(totals).getByText('600.000')).toBeInTheDocument();
    expect(within(totals).getByText('3.000.000')).toBeInTheDocument();
    expect(within(totals).getByText('2.500.000')).toBeInTheDocument();
  });

  it('defaults the side to receivable and holds the request on an invalid range', () => {
    renderAt('/reports/debt-balance?from=2026-10-10&to=2026-10-01');

    expect(useDebtBalanceMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ side: 'Receivable', from: '2026-10-10', to: '2026-10-01' }),
      { enabled: false },
    );
    expect(screen.getByText('Từ ngày không được sau Đến ngày')).toBeInTheDocument();
  });

  it('row click opens the ledger with params', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-31');

    await user.click(screen.getByText('Thép Hòa Phát'));

    expect(screen.getByTestId('location')).toHaveTextContent(
      '/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31',
    );
    expect(screen.getByTestId('state')).toHaveTextContent(
      JSON.stringify({ from: '/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-31' }),
    );
  });

  it('choosing Toàn công ty puts scope=Company in the url and the hook params', async () => {
    const user = userEvent.setup();
    renderAt('/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-31');

    await user.click(screen.getByRole('combobox', { name: 'Phạm vi' }));
    await user.click(screen.getByRole('option', { name: 'Toàn công ty' }));

    expect(useDebtBalanceMock).toHaveBeenLastCalledWith(expect.objectContaining({ scope: 'Company' }), { enabled: true });
    expect(screen.getByTestId('location')).toHaveTextContent('scope=Company');
  });

  it('has no scope select and stays on Branch without branches.access_all', () => {
    useAuthStore.setState({
      user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions: [] },
    });
    renderAt('/reports/debt-balance?side=Payable&scope=Company');

    expect(screen.queryByRole('combobox', { name: 'Phạm vi' })).not.toBeInTheDocument();
    expect(useDebtBalanceMock).toHaveBeenLastCalledWith(expect.objectContaining({ scope: 'Branch' }), { enabled: true });
  });

  describe('TT133 export', () => {
    it('offers TongHopCongNo with the current filters and downloads directly', () => {
      renderAt('/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-31&search=NCC');

      expect(exportButtonProps.forms).toEqual([
        {
          code: 'TongHopCongNo',
          report: 'debt-balance',
          params: { side: 'Payable', from: '2026-10-01', to: '2026-10-31', search: 'NCC', scope: 'Branch' },
          fileBase: 'TongHopCongNo_20261001_20261031',
        },
      ]);
      expect(exportButtonProps.onSelect).toBeUndefined();
      expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'false');
    });

    it('carries the company scope and disables on an invalid range', () => {
      renderAt('/reports/debt-balance?side=Payable&from=2026-10-31&to=2026-10-01&scope=Company');

      expect(exportButtonProps.forms[0].params).toEqual(expect.objectContaining({ scope: 'Company' }));
      expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'true');
    });

    it('the preset Tháng trước sets the previous month', async () => {
      const user = userEvent.setup();
      renderAt('/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-31');

      await user.click(screen.getByRole('combobox', { name: 'Kỳ' }));
      await user.click(screen.getByRole('option', { name: 'Tháng trước' }));

      const last = periodPreset('lastMonth');
      expect(screen.getByLabelText('Từ ngày')).toHaveValue(last.from);
      expect(screen.getByLabelText('Đến ngày')).toHaveValue(last.to);
    });
  });
});
