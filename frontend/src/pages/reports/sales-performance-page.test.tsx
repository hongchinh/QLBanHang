import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';

const apiGet = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api-client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/lib/api-client')>()),
  apiGet,
}));

import { SalesPerformancePage } from './sales-performance-page';

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
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={['/reports/sales-performance?from=2026-10-01&to=2026-10-31']}>
          <SalesPerformancePage />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await waitFor(() => expect(screen.getAllByText('An Sale').length).toBeGreaterThan(0));
    const urls = apiGet.mock.calls.map((c) => c[0] as string);
    expect(urls).toContain('/reports/revenue/sales-leaderboard');
    expect(urls.filter((u) => u.startsWith('/dashboard'))).toEqual([]);
    expect(screen.getByText(/Conversion/)).toBeInTheDocument();
    expect(screen.getByText('66.7%')).toBeInTheDocument();
  });
});
