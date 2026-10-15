import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  useDashboardSummary,
  useLeaderboard,
  useQuotationStats,
  useRecentActivity,
  useRevenueSeries,
  useTopCustomers,
  useTopProducts,
} from './hooks';

vi.mock('./api', () => ({
  dashboardApi: new Proxy({}, { get: () => vi.fn().mockResolvedValue([]) }),
}));

function intervalOf(useHook: () => unknown): unknown {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  renderHook(useHook, { wrapper });
  return client.getQueryCache().getAll()[0]?.observers[0]?.options.refetchInterval;
}

describe('overview dashboard queries', () => {
  const live: [string, () => unknown][] = [
    ['summary', () => useDashboardSummary({})],
    ['revenue series', () => useRevenueSeries({ from: 'a', to: 'b', granularity: 'day' })],
    ['top customers', () => useTopCustomers({})],
    ['top products', () => useTopProducts({})],
    ['recent activity', () => useRecentActivity({})],
    ['leaderboard', () => useLeaderboard({ from: 'a', to: 'b' })],
  ];

  it.each(live)('%s refetches every 60 seconds as the fallback for a missed signal', async (_name, hook) => {
    expect(intervalOf(hook)).toBe(60_000);
  });

  it('quotation-stats has no polling', () => {
    expect(intervalOf(() => useQuotationStats())).toBeUndefined();
  });
});
