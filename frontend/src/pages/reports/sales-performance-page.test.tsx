import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { SalesPerformancePage } from './sales-performance-page';

vi.mock('@/features/dashboard/hooks', () => ({
  useLeaderboard: () => ({
    data: [
      { userId: 'u1', fullName: 'An Sale', revenue: 5_000_000, confirmedCount: 3, conversionRate: 50, deltaPct: 10 },
    ],
    isLoading: false,
  }),
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
    <MemoryRouter initialEntries={['/reports/sales-performance?from=2026-10-01&to=2026-10-31']}>
      <SalesPerformancePage />
      <LocationDisplay />
    </MemoryRouter>,
  );
}

describe('SalesPerformancePage drill-down', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  it('a row opens the revenue report of that sale and passes its own url as state.from', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByText('An Sale', { selector: 'td' }));

    expect(screen.getByTestId('location')).toHaveTextContent(
      '/reports/revenue?saleUserId=u1&from=2026-10-01&to=2026-10-31',
    );
    expect(screen.getByTestId('state')).toHaveTextContent(
      JSON.stringify({ from: '/reports/sales-performance?from=2026-10-01&to=2026-10-31' }),
    );
  });
});
