import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CustomerDebtPanel } from './customer-debt-panel';
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
    creditLimit: 5_000_000,
    available: -2_000_000,
    overdueAmount: 1_500_000,
    creditDays: 30,
    ...overrides,
  };
}

function result(data: PartnerDebtSummary | undefined, extra: { isLoading?: boolean; isError?: boolean } = {}) {
  return { data, isLoading: false, isError: false, ...extra };
}

describe('CustomerDebtPanel', () => {
  beforeEach(() => {
    summaryMock.mockReset();
  });

  it('shows balance limit available and overdue', () => {
    summaryMock.mockReturnValue(result(summary()));
    render(<CustomerDebtPanel partnerId={P1} />);

    expect(summaryMock).toHaveBeenCalledWith(P1, { enabled: true });
    expect(screen.getByText('Dư nợ phải thu')).toBeInTheDocument();
    expect(screen.getByTestId('customer-debt-balance')).toHaveTextContent('7.000.000');
    expect(screen.getByTestId('customer-debt-limit')).toHaveTextContent('5.000.000');
    expect(screen.getByTestId('customer-debt-available')).toHaveTextContent('-2.000.000');
    expect(screen.getByTestId('customer-debt-available')).toHaveClass('text-destructive');
    expect(screen.getByTestId('customer-debt-overdue')).toHaveTextContent('1.500.000');
    expect(screen.getByTestId('customer-debt-overdue')).toHaveClass('text-destructive');
    expect(screen.getByTestId('customer-debt-credit-days')).toHaveTextContent('30');
    // Display only.
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('unlimited when no limit', () => {
    summaryMock.mockReturnValue(
      result(summary({ creditLimit: null, available: null, overdueAmount: 0, creditDays: null })),
    );
    render(<CustomerDebtPanel partnerId={P1} />);

    expect(screen.getByTestId('customer-debt-limit')).toHaveTextContent('Không giới hạn');
    expect(screen.getByTestId('customer-debt-available')).toHaveTextContent('Không giới hạn');
    expect(screen.getByTestId('customer-debt-available')).not.toHaveClass('text-destructive');
    expect(screen.getByTestId('customer-debt-overdue')).toHaveTextContent('0');
    expect(screen.getByTestId('customer-debt-overdue')).not.toHaveClass('text-destructive');
    expect(screen.getByTestId('customer-debt-credit-days')).toHaveTextContent('—');
  });

  it('shows a skeleton while loading and nothing on error', () => {
    summaryMock.mockReturnValue(result(undefined, { isLoading: true }));
    const { container, rerender } = render(<CustomerDebtPanel partnerId={P1} />);
    expect(screen.getByTestId('customer-debt-skeleton')).toBeInTheDocument();
    expect(screen.queryByTestId('customer-debt-balance')).not.toBeInTheDocument();

    summaryMock.mockReturnValue(result(undefined, { isError: true }));
    rerender(<CustomerDebtPanel partnerId={P1} />);
    expect(container).toBeEmptyDOMElement();
  });
});
