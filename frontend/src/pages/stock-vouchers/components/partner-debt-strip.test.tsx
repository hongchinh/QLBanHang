import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { PartnerDebtStrip } from './partner-debt-strip';
import type { PartnerDebtSummary } from '@/features/debt/types';

const summaryMock = vi.fn();
vi.mock('@/features/debt/hooks', () => ({
  usePartnerDebtSummary: (...args: unknown[]) => summaryMock(...args),
}));

const P1 = '44444444-4444-4444-8444-444444444444';

function summary(overrides: Partial<PartnerDebtSummary> = {}): PartnerDebtSummary {
  return {
    partnerId: P1,
    receivableBalance: 7_000_000,
    payableBalance: 500_000,
    creditLimit: 5_000_000,
    available: -2_000_000,
    overdueAmount: 1_500_000,
    creditDays: 30,
    ...overrides,
  };
}

function render(ui: ReactElement) {
  return rtlRender(<MemoryRouter>{ui}</MemoryRouter>);
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function result(data: PartnerDebtSummary | undefined, extra: { isLoading?: boolean; isError?: boolean } = {}) {
  return { data, isLoading: false, isError: false, ...extra };
}

describe('PartnerDebtStrip', () => {
  beforeEach(() => {
    summaryMock.mockReset();
    setPermissions([]);
  });

  const base = { partnerId: P1, partnerLabel: 'KH01 — An Phát', cancelled: false };

  it('shows balances, after-voucher values and a negative available amount in red', () => {
    setPermissions(['reports.debt']);
    summaryMock.mockReturnValue(result(summary({ receivableBalance: 7_000_000, creditLimit: 10_000_000 })));
    render(<PartnerDebtStrip {...base} current={{ effect: 'ReceivableIncrease', total: 4_000_000, paid: 0 }} />);

    expect(screen.getByText('KH01 — An Phát')).toBeInTheDocument();
    expect(screen.getByTestId('partner-debt-receivable')).toHaveTextContent('7.000.000');
    expect(screen.getByTestId('partner-debt-receivable-after')).toHaveTextContent('11.000.000');
    expect(screen.getByTestId('partner-debt-payable')).toHaveTextContent('500.000');
    expect(screen.queryByTestId('partner-debt-payable-after')).not.toBeInTheDocument();
    expect(screen.getByTestId('partner-debt-limit')).toHaveTextContent('10.000.000');
    expect(screen.getByTestId('partner-debt-available')).toHaveTextContent('-1.000.000');
    expect(screen.getByTestId('partner-debt-available')).toHaveClass('text-destructive');
    expect(screen.getByTestId('partner-debt-overdue')).toHaveTextContent('1.500.000');
    expect(screen.getByRole('link', { name: 'Xem' })).toHaveAttribute(
      'href', `/reports/debt-overdue?side=Receivable&partnerId=${P1}`,
    );
    expect(screen.getByTestId('partner-debt-credit-days')).toHaveTextContent('30');
    expect(screen.getByText(/Còn được nợ sau phiếu/)).toBeInTheDocument();
  });

  it('the overdue link opens in a new tab', () => {
    setPermissions(['reports.debt']);
    summaryMock.mockReturnValue(result(summary()));
    render(<PartnerDebtStrip {...base} current={{ effect: 'None', total: 0, paid: 0 }} />);
    const link = screen.getByRole('link', { name: 'Xem' });
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('hides the overdue link when nothing is overdue', () => {
    setPermissions(['reports.debt']);
    summaryMock.mockReturnValue(result(summary({ overdueAmount: 0 })));
    render(<PartnerDebtStrip {...base} current={{ effect: 'None', total: 0, paid: 0 }} />);
    expect(screen.queryByRole('link', { name: 'Xem' })).not.toBeInTheDocument();
  });

  it('hides the overdue link without reports.debt', () => {
    setPermissions(['debt.partner_summary']);
    summaryMock.mockReturnValue(result(summary()));
    render(<PartnerDebtStrip {...base} current={{ effect: 'None', total: 0, paid: 0 }} />);
    expect(screen.queryByRole('link', { name: 'Xem' })).not.toBeInTheDocument();
  });

  it('a supplier stock-in shows the payable after the voucher and leaves the receivable alone', () => {
    summaryMock.mockReturnValue(result(summary({ payableBalance: 500_000, creditLimit: null, available: null })));
    render(<PartnerDebtStrip {...base} current={{ effect: 'PayableIncrease', total: 3_000_000, paid: 1_000_000 }} />);
    expect(screen.getByTestId('partner-debt-payable-after')).toHaveTextContent('2.500.000');
    expect(screen.queryByTestId('partner-debt-receivable-after')).not.toBeInTheDocument();
    // The receivable did not change, so the label stays "Còn được nợ" (no "sau phiếu").
    expect(screen.queryByText(/Còn được nợ sau phiếu/)).not.toBeInTheDocument();
    expect(screen.getByText(/Còn được nợ/)).toBeInTheDocument();
  });

  it('an edited voucher subtracts its saved impact', () => {
    summaryMock.mockReturnValue(result(summary({ receivableBalance: 7_000_000 })));
    render(
      <PartnerDebtStrip
        {...base}
        current={{ effect: 'ReceivableIncrease', total: 5_000_000, paid: 0 }}
        saved={{ effect: 'ReceivableIncrease', total: 4_000_000, paid: 0 }}
      />,
    );
    expect(screen.getByTestId('partner-debt-receivable-after')).toHaveTextContent('8.000.000');
  });

  it('a cancelled voucher shows only the current balances; no limit reads "Không giới hạn"', () => {
    summaryMock.mockReturnValue(result(summary({ creditLimit: null, available: null })));
    render(<PartnerDebtStrip {...base} cancelled current={{ effect: 'ReceivableIncrease', total: 1, paid: 0 }} />);
    expect(screen.queryByTestId('partner-debt-receivable-after')).not.toBeInTheDocument();
    expect(screen.getByTestId('partner-debt-limit')).toHaveTextContent('Không giới hạn');
    expect(screen.getByTestId('partner-debt-available')).toHaveTextContent('Không giới hạn');
  });

  it('renders nothing on error and a skeleton while loading', () => {
    summaryMock.mockReturnValue(result(undefined, { isError: true }));
    const { container, unmount } = render(<PartnerDebtStrip {...base} current={{ effect: 'None', total: 0, paid: 0 }} />);
    expect(container).toBeEmptyDOMElement();
    unmount();
    summaryMock.mockReturnValue(result(undefined, { isLoading: true }));
    render(<PartnerDebtStrip {...base} current={{ effect: 'None', total: 0, paid: 0 }} />);
    expect(screen.getByTestId('partner-debt-skeleton')).toBeInTheDocument();
  });
});
