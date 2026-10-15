import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';

const apiGet = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api-client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/lib/api-client')>()),
  apiGet,
}));

import { RevenuePage } from './revenue-page';

const kpi = (value: number) => ({ value, deltaPct: null, spark: [] });

function respond(url: string) {
  switch (url) {
    case '/reports/revenue/summary':
      return {
        from: '2026-10-01',
        to: '2026-10-31',
        prevFrom: '2026-09-01',
        prevTo: '2026-09-30',
        rangeRevenue: kpi(7_654_321),
        totalCount: kpi(5),
        cancelledCount: kpi(1),
      };
    case '/reports/revenue/series':
      return { points: [{ date: '2026-10-02', total: 1000, confirmedCount: 2 }] };
    case '/reports/revenue/top-customers':
      return [{ customerId: 'c1', customerName: 'Khách A', revenue: 500, quotationCount: 3 }];
    default:
      return [];
  }
}

function LocationDisplay() {
  const loc = useLocation();
  return <div data-testid="location">{loc.pathname + loc.search}</div>;
}

// The real useDashboardParams runs here, so a period change goes through its history update.
function renderPage(search: string, state?: unknown) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[{ pathname: '/reports/revenue', search, state }]}>
        <RevenuePage />
        <LocationDisplay />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('RevenuePage (Doanh thu report)', () => {
  beforeEach(() => {
    apiGet.mockReset();
    apiGet.mockImplementation((url: string) => Promise.resolve(respond(url)));
    useAuthStore.setState({
      user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions: ['quotations.view_all'] },
    });
  });

  it('reads its own report endpoints and no /dashboard endpoint', async () => {
    renderPage('?from=2026-10-01&to=2026-10-31');

    await waitFor(() => expect(screen.getByText(/7\.654\.321/)).toBeInTheDocument());
    const urls = apiGet.mock.calls.map((c) => c[0] as string);
    expect(urls).toContain('/reports/revenue/summary');
    expect(urls).toContain('/reports/revenue/series');
    expect(urls).toContain('/reports/revenue/top-customers');
    expect(urls.filter((u) => u.startsWith('/dashboard'))).toEqual([]);
    expect(screen.getByText('3 BG')).toBeInTheDocument();
    expect(screen.getByText('Tổng doanh thu')).toBeInTheDocument();
  });

  it('links back to the report it was opened from', () => {
    renderPage('?from=2026-10-01&to=2026-10-31&saleUserId=u1', { from: '/parent?x=1' });

    expect(screen.getByRole('link', { name: 'Quay lại báo cáo trước' })).toHaveAttribute('href', '/parent?x=1');
  });

  it('has no back link without state', () => {
    renderPage('?from=2026-10-01&to=2026-10-31&saleUserId=u1');

    expect(screen.queryByRole('link', { name: /^Quay lại/ })).not.toBeInTheDocument();
  });

  it('keeps the back link after a period change', async () => {
    const user = userEvent.setup();
    renderPage('?from=2026-10-01&to=2026-10-31&saleUserId=u1', { from: '/parent?x=1' });
    const before = screen.getByTestId('location').textContent;

    await user.click(screen.getByRole('button', { name: '7N' }));

    await waitFor(() => expect(screen.getByTestId('location').textContent).not.toBe(before));
    expect(screen.getByRole('link', { name: 'Quay lại báo cáo trước' })).toHaveAttribute('href', '/parent?x=1');
  });
});
