import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
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

const useDebtPartnerSearchMock = vi.fn((side: string, keyword: string) => {
  void side;
  void keyword;
  return { data: [], isLoading: false, isError: false };
});
vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (side: string, keyword: string) => useDebtPartnerSearchMock(side, keyword),
}));

const ledger = (over: Partial<DebtLedgerReport> = {}): DebtLedgerReport => ({
  side: 'Payable',
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

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <DebtLedgerPage />
    </MemoryRouter>,
  );
}

describe('DebtLedgerPage', () => {
  beforeEach(() => {
    useDebtLedgerMock.mockClear();
    useDebtPartnerSearchMock.mockClear();
    ledgerState.data = ledger();
  });

  it('reads params from the url and renders running balance', () => {
    renderAt('/reports/debt-ledger?side=Payable&partnerId=s1&from=2026-10-01&to=2026-10-31');

    expect(useDebtLedgerMock).toHaveBeenLastCalledWith(
      { side: 'Payable', partnerId: 's1', from: '2026-10-01', to: '2026-10-31' },
      { enabled: true },
    );
    expect(useDebtPartnerSearchMock).toHaveBeenLastCalledWith('Payable', expect.any(String));
    expect(screen.getByRole('combobox', { name: 'Phía' })).toHaveTextContent('Phải trả');
    expect(screen.getByRole('combobox', { name: 'Đối tượng' })).toHaveValue('NCC01 - Thép Hòa Phát');
    expect(screen.getByLabelText('Từ ngày')).toHaveValue('2026-10-01');
    expect(screen.getByLabelText('Đến ngày')).toHaveValue('2026-10-31');

    for (const name of ['Ngày', 'Số CT', 'Loại CT', 'Diễn giải', 'Tăng', 'Giảm', 'Số dư']) {
      expect(screen.getByRole('columnheader', { name })).toBeInTheDocument();
    }

    const opening = screen.getByText('Số dư đầu kỳ').closest('tr')!;
    expect(within(opening).getByText('Có 1.000.000')).toBeInTheDocument();

    const stockIn = screen.getByRole('link', { name: 'PN00001' }).closest('tr')!;
    expect(within(stockIn).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      '02/10/2026', 'PN00001', 'Phiếu nhập', 'Nhập mua tôn', '2.500.000', '', '3.500.000',
    ]);
    const payment = screen.getByRole('link', { name: 'PC00001' }).closest('tr')!;
    const paymentCells = within(payment).getAllByRole('cell');
    expect(paymentCells[5]).toHaveTextContent('4.000.000');
    expect(paymentCells[6]).toHaveTextContent('-500.000');
    expect(paymentCells[6]).toHaveClass('text-right', 'tabular-nums');

    const totals = screen.getByText('Cộng phát sinh').closest('tr')!;
    expect(within(totals).getByText('2.500.000')).toBeInTheDocument();
    expect(within(totals).getByText('4.100.000')).toBeInTheDocument();

    // Payable with a negative balance shows as Dư Nợ (P24); the server splits it.
    const closing = screen.getByText('Số dư cuối kỳ').closest('tr')!;
    expect(within(closing).getByText('Nợ 600.000')).toBeInTheDocument();
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
});
