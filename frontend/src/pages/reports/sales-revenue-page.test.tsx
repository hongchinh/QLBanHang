import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { SalesRevenuePage } from './sales-revenue-page';

vi.mock('@/features/reports/sales-revenue/hooks', () => ({
  useSalesRevenue: () => ({
    isLoading: false,
    isError: false,
    data: {
      from: '2026-10-01',
      to: '2026-10-31',
      items: [
        {
          saleUserId: 'u1',
          saleName: 'An Sale',
          isSaleDeleted: false,
          quotationCount: 2,
          totalRevenueGross: 1_100_000,
          totalRevenueNet: 1_000_000,
        },
      ],
      totalQuotationCount: 2,
      grandTotalGross: 1_100_000,
      grandTotalNet: 1_000_000,
    },
  }),
}));
vi.mock('@/features/quotations/hooks', () => ({
  useQuotationOwners: () => ({ data: [] }),
}));

function LocationDisplay() {
  const loc = useLocation();
  return (
    <>
      <div data-testid="location">{loc.pathname + loc.search}</div>
      <div data-testid="state">{JSON.stringify(loc.state)}</div>
    </>
  );
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/reports/sales-revenue']}>
      <SalesRevenuePage />
      <LocationDisplay />
    </MemoryRouter>,
  );
}

describe('SalesRevenuePage drill-down', () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions: ['reports.revenue'] },
    });
  });

  it('a row opens the sale detail and passes saleName and its own url as state', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByText('An Sale'));

    expect(screen.getByTestId('location').textContent).toMatch(/^\/reports\/sales-revenue\/u1\?from=\d{4}-\d{2}-\d{2}&to=\d{4}-\d{2}-\d{2}$/);
    expect(screen.getByTestId('state')).toHaveTextContent(
      JSON.stringify({ saleName: 'An Sale', from: '/reports/sales-revenue' }),
    );
  });
});
