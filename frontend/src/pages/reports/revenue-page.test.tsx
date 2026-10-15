import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { RevenuePage } from './revenue-page';

const queryResult = { data: undefined, isLoading: false, isError: false };

vi.mock('@/features/dashboard/hooks', () => ({
  useDashboardSummary: () => queryResult,
  useRevenueSeries: () => queryResult,
  useTopCustomers: () => queryResult,
}));
vi.mock('@/features/reports/sales-revenue-detail/hooks', () => ({
  useRevenueLineItems: () => queryResult,
}));
vi.mock('@/features/quotations/hooks', () => ({
  useQuotationOwners: () => ({ data: [] }),
}));
// Charts and cards are not under test here.
vi.mock('@/features/dashboard/components/revenue-area-chart', () => ({ RevenueAreaChart: () => null }));
vi.mock('@/features/dashboard/components/kpi-card', () => ({ KpiCard: () => null }));
vi.mock('@/features/dashboard/components/top-list-card', () => ({ TopListCard: () => null }));

function LocationDisplay() {
  const loc = useLocation();
  return <div data-testid="location">{loc.pathname + loc.search}</div>;
}

// The real useDashboardParams runs here, so a period change goes through its history update.
function renderPage(state?: unknown) {
  return render(
    <MemoryRouter
      initialEntries={[{ pathname: '/reports/revenue', search: '?from=2026-10-01&to=2026-10-31&saleUserId=u1', state }]}
    >
      <RevenuePage />
      <LocationDisplay />
    </MemoryRouter>,
  );
}

describe('RevenuePage back link', () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions: ['quotations.view_all'] },
    });
  });

  it('links back to the report it was opened from', () => {
    renderPage({ from: '/parent?x=1' });

    expect(screen.getByRole('link', { name: 'Quay lại báo cáo trước' })).toHaveAttribute('href', '/parent?x=1');
  });

  it('has no back link without state', () => {
    renderPage();

    expect(screen.queryByRole('link', { name: /^Quay lại/ })).not.toBeInTheDocument();
  });

  it('keeps the back link after a period change', async () => {
    const user = userEvent.setup();
    renderPage({ from: '/parent?x=1' });
    const before = screen.getByTestId('location').textContent;

    await user.click(screen.getByRole('button', { name: '7N' }));

    await waitFor(() => expect(screen.getByTestId('location').textContent).not.toBe(before));
    expect(screen.getByRole('link', { name: 'Quay lại báo cáo trước' })).toHaveAttribute('href', '/parent?x=1');
  });
});
