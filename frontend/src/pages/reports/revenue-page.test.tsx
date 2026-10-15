import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';

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

describe('RevenuePage (Doanh thu report)', () => {
  beforeEach(() => {
    apiGet.mockReset();
    apiGet.mockImplementation((url: string) => Promise.resolve(respond(url)));
  });

  function renderPage() {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={['/reports/revenue?from=2026-10-01&to=2026-10-31']}>
          <RevenuePage />
        </MemoryRouter>
      </QueryClientProvider>,
    );
  }

  it('reads its own report endpoints and no /dashboard endpoint', async () => {
    renderPage();

    await waitFor(() => expect(screen.getByText(/7\.654\.321/)).toBeInTheDocument());
    const urls = apiGet.mock.calls.map((c) => c[0] as string);
    expect(urls).toContain('/reports/revenue/summary');
    expect(urls).toContain('/reports/revenue/series');
    expect(urls).toContain('/reports/revenue/top-customers');
    expect(urls.filter((u) => u.startsWith('/dashboard'))).toEqual([]);
    expect(screen.getByText('3 BG')).toBeInTheDocument();
    expect(screen.getByText('Tổng doanh thu')).toBeInTheDocument();
  });
});
