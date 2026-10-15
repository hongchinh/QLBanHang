import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, useLocation } from 'react-router-dom';

const apiGet = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api-client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/lib/api-client')>()),
  apiGet,
}));

import { SalesPerformancePage } from './sales-performance-page';

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
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/reports/sales-performance?from=2026-10-01&to=2026-10-31']}>
        <SalesPerformancePage />
        <LocationDisplay />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('SalesPerformancePage (Hiệu suất sale report)', () => {
  beforeEach(() => {
    apiGet.mockReset();
    apiGet.mockImplementation((url: string) =>
      Promise.resolve(
        url === '/reports/revenue/sales-leaderboard'
          ? [{ userId: 'u1', fullName: 'An Sale', revenue: 900, confirmedCount: 4, conversionRate: 66.7, deltaPct: null }]
          : [],
      ),
    );
  });

  it('reads the quotation leaderboard of the report and keeps the Conversion column', async () => {
    renderPage();

    await waitFor(() => expect(screen.getAllByText('An Sale').length).toBeGreaterThan(0));
    const urls = apiGet.mock.calls.map((c) => c[0] as string);
    expect(urls).toContain('/reports/revenue/sales-leaderboard');
    expect(urls.filter((u) => u.startsWith('/dashboard'))).toEqual([]);
    expect(screen.getByText(/Conversion/)).toBeInTheDocument();
    expect(screen.getByText('66.7%')).toBeInTheDocument();
  });

  it('a row opens the revenue report of that sale and passes its own url as state.from', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByText('An Sale', { selector: 'td' }));

    expect(screen.getByTestId('location').textContent).toBe(
      '/reports/revenue?saleUserId=u1&from=2026-10-01&to=2026-10-31',
    );
    expect(screen.getByTestId('state').textContent).toBe(
      JSON.stringify({ from: '/reports/sales-performance?from=2026-10-01&to=2026-10-31' }),
    );
  });
});
