import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { CashBookPage } from './cash-book-page';
import type { CashBookGroup, CashBookParams, CashBookReport } from '@/features/cash-reports/types';
import type { Fund, FundListParams } from '@/features/funds/types';

const reportState: { data: CashBookReport | undefined } = { data: undefined };
const useCashBookMock = vi.fn((params: CashBookParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/cash-reports/hooks', () => ({
  useCashBook: (params: CashBookParams, options?: { enabled?: boolean }) => useCashBookMock(params, options),
}));

const fund = (over: Partial<Fund>): Fund => ({
  id: 'f1', branchId: 'b1', code: 'QTM', name: 'Quỹ tiền mặt', kind: 'Cash', bankName: null, accountNumber: null,
  isDefault: true, openingAmount: 0, isActive: true, ...over,
});
const FUNDS: Fund[] = [
  fund({}),
  fund({ id: 'f2', code: 'VCB', name: 'Vietcombank', kind: 'Bank', bankName: 'Vietcombank', accountNumber: '0123456789', isDefault: true }),
];
const useFundsMock = vi.fn((params?: FundListParams) => ({
  data: FUNDS.filter((f) => !params?.kind || f.kind === params.kind),
}));
vi.mock('@/features/funds/hooks', () => ({
  useFunds: (params?: FundListParams) => useFundsMock(params),
}));

const cashGroup: CashBookGroup = {
  fundId: 'f1', fundCode: 'QTM', fundName: 'Quỹ tiền mặt', fundKind: 'Cash', bankName: null, accountNumber: null,
  opening: 1_000_000,
  rows: [
    {
      at: '2026-10-02T02:00:00Z', sourceType: 'Receipt', sourceId: 'r1', receiptCode: 'PT00001', paymentCode: null,
      transferCode: null, partnerOrPerson: 'KH Minh Long', description: 'Thu tiền hàng', reasonName: 'Thu tiền khách hàng', in: 500_000, out: 0, running: 1_500_000,
    },
    {
      at: '2026-10-03T02:00:00Z', sourceType: 'Payment', sourceId: 'p1', receiptCode: null, paymentCode: 'PC00001',
      transferCode: null, partnerOrPerson: 'NCC Hòa Phát', description: 'Trả tiền NCC', reasonName: 'Trả tiền NCC', in: 0, out: 200_000, running: 1_300_000,
    },
    {
      at: '2026-10-04T02:00:00Z', sourceType: 'TransferOut', sourceId: 't1', receiptCode: null, paymentCode: null,
      transferCode: 'CQ00001', partnerOrPerson: null, description: 'Chuyển quỹ QTM → VCB', reasonName: 'Chuyển quỹ', in: 0, out: 300_000, running: 1_000_000,
    },
  ],
  totalIn: 500_000, totalOut: 500_000, closing: 1_000_000,
};
const bankGroup: CashBookGroup = {
  fundId: 'f2', fundCode: 'VCB', fundName: 'Vietcombank', fundKind: 'Bank', bankName: 'Vietcombank', accountNumber: '0123456789',
  opening: 0,
  rows: [
    {
      at: '2026-10-04T02:00:00Z', sourceType: 'TransferIn', sourceId: 't1', receiptCode: null, paymentCode: null,
      transferCode: 'CQ00001', partnerOrPerson: null, description: 'Chuyển quỹ QTM → VCB', reasonName: 'Chuyển quỹ', in: 300_000, out: 0, running: 300_000,
    },
  ],
  totalIn: 300_000, totalOut: 0, closing: 300_000,
};

const report = (groups: CashBookGroup[], over: Partial<CashBookReport> = {}): CashBookReport => ({
  kind: 'All', from: '2026-10-01', to: '2026-10-31', groups,
  grandOpening: 1_000_000, grandIn: 500_000, grandOut: 200_000, grandClosing: 1_300_000, ...over,
});

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderPage() {
  return render(
    <MemoryRouter>
      <CashBookPage />
    </MemoryRouter>,
  );
}

describe('CashBookPage', () => {
  beforeEach(() => {
    useCashBookMock.mockClear();
    useFundsMock.mockClear();
    setPermissions(['receipts.view', 'payments.view', 'fund_transfers.view']);
  });

  it('renders one group per fund with opening and closing', () => {
    reportState.data = report([cashGroup], { grandOpening: 1_000_000, grandIn: 500_000, grandOut: 500_000, grandClosing: 1_000_000 });
    renderPage();

    expect(useCashBookMock).toHaveBeenLastCalledWith(expect.objectContaining({ kind: 'All', fundId: undefined }), {
      enabled: true,
    });

    const section = screen.getByRole('region', { name: 'QTM – Quỹ tiền mặt' });
    for (const name of ['Ngày', 'Số phiếu thu', 'Số phiếu chi', 'Đối tượng/Người nộp-nhận', 'Diễn giải', 'Lý do', 'Thu', 'Chi', 'Tồn']) {
      expect(within(section).getByRole('columnheader', { name })).toBeInTheDocument();
    }
    const opening = within(section).getByText('Tồn đầu kỳ').closest('tr')!;
    expect(within(opening).getByText('1.000.000')).toBeInTheDocument();

    const receipt = within(section).getByRole('link', { name: 'PT00001' });
    expect(receipt).toHaveAttribute('href', '/receipts/r1');
    expect(within(receipt.closest('tr')!).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      '02/10/2026', 'PT00001', '', 'KH Minh Long', 'Thu tiền hàng', 'Thu tiền khách hàng', '500.000', '', '1.500.000',
    ]);
    expect(within(section).getByRole('link', { name: 'PC00001' })).toHaveAttribute('href', '/payments/p1');

    // A transfer out shows its code in the payment column.
    const transfer = within(section).getByRole('link', { name: 'CQ00001' });
    expect(transfer).toHaveAttribute('href', '/fund-transfers/t1');
    const transferCells = within(transfer.closest('tr')!).getAllByRole('cell');
    expect(transferCells[1]).toHaveTextContent('');
    expect(transferCells[2]).toHaveTextContent('CQ00001');
    expect(transferCells[7]).toHaveTextContent('300.000');
    expect(transferCells[7]).toHaveClass('text-right', 'tabular-nums');

    const total = within(section).getByText('Cộng').closest('tr')!;
    expect(within(total).getAllByText('500.000')).toHaveLength(2);
    const closing = within(section).getByText('Tồn cuối kỳ').closest('tr')!;
    expect(within(closing).getByText('1.000.000')).toBeInTheDocument();

    expect(screen.queryByText('Không tính chuyển quỹ nội bộ giữa các quỹ đang xem')).not.toBeInTheDocument();
  });

  it('document codes without the permission of their screen are plain text', () => {
    reportState.data = report([cashGroup]);
    setPermissions(['receipts.view']);
    renderPage();

    expect(screen.getByRole('link', { name: 'PT00001' })).toHaveAttribute('href', '/receipts/r1');
    for (const code of ['PC00001', 'CQ00001']) {
      expect(screen.getByText(code)).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: code })).not.toBeInTheDocument();
    }
  });

  it('grand total shown for several funds', () => {
    reportState.data = report([cashGroup, bankGroup]);
    renderPage();

    const bank = screen.getByRole('region', { name: 'VCB – Vietcombank' });
    expect(within(bank).getByText(/0123456789/)).toBeInTheDocument();
    // A transfer in shows its code in the receipt column.
    const transferCells = within(within(bank).getByRole('link', { name: 'CQ00001' }).closest('tr')!).getAllByRole('cell');
    expect(transferCells[1]).toHaveTextContent('CQ00001');
    expect(transferCells[2]).toHaveTextContent('');

    const grand = screen.getByTestId('cash-book-grand-total');
    expect(within(grand).getByText('Không tính chuyển quỹ nội bộ giữa các quỹ đang xem')).toBeInTheDocument();
    // Server totals are shown as they are (P25).
    expect(within(grand).getByText('1.000.000')).toBeInTheDocument();
    expect(within(grand).getByText('500.000')).toBeInTheDocument();
    expect(within(grand).getByText('200.000')).toBeInTheDocument();
    expect(within(grand).getByText('1.300.000')).toBeInTheDocument();
  });

  it('fund options follow the kind filter', async () => {
    const user = userEvent.setup();
    reportState.data = report([cashGroup]);
    renderPage();

    expect(useFundsMock).toHaveBeenLastCalledWith({ kind: undefined });

    await user.click(screen.getByRole('combobox', { name: 'Quỹ' }));
    expect(screen.getByRole('option', { name: 'QTM – Quỹ tiền mặt' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'VCB – Vietcombank' })).toBeInTheDocument();
    await user.click(screen.getByRole('option', { name: 'QTM – Quỹ tiền mặt' }));
    expect(useCashBookMock).toHaveBeenLastCalledWith(expect.objectContaining({ kind: 'All', fundId: 'f1' }), {
      enabled: true,
    });

    // Switching the kind lists only its funds and drops a fund of the other kind.
    await user.click(screen.getByRole('combobox', { name: 'Loại quỹ' }));
    await user.click(screen.getByRole('option', { name: 'Ngân hàng' }));
    expect(useFundsMock).toHaveBeenLastCalledWith({ kind: 'Bank' });
    expect(useCashBookMock).toHaveBeenLastCalledWith(expect.objectContaining({ kind: 'Bank', fundId: undefined }), {
      enabled: true,
    });
    expect(screen.getByRole('combobox', { name: 'Quỹ' })).toHaveTextContent('Tất cả');

    await user.click(screen.getByRole('combobox', { name: 'Quỹ' }));
    expect(screen.queryByRole('option', { name: 'QTM – Quỹ tiền mặt' })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'VCB – Vietcombank' })).toBeInTheDocument();
  });
});
