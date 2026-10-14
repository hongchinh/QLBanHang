import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Mock } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider, keepPreviousData } from '@tanstack/react-query';
import type { QueryKey } from '@tanstack/react-query';
import { apiGet } from '@/lib/api-client';
import type * as ApiModule from './api';
import { executiveApi } from './api';
import {
  executiveQueryOptions,
  useAttention,
  useBranchComparison,
  useCashBreakdown,
  useCashSeries,
  useCashSummary,
  useDebtBreakdown,
  useDebtSummary,
  useInventoryBreakdown,
  useInventorySummary,
  useRecentDocuments,
  useSalesSeries,
  useSalesSummary,
  useSalesTop,
} from './hooks';
import { executiveKeys } from './keys';

vi.mock('@/lib/api-client', () => ({ apiGet: vi.fn() }));
vi.mock('./api', () => ({
  executiveApi: {
    salesSummary: vi.fn(),
    salesSeries: vi.fn(),
    salesTop: vi.fn(),
    cashSummary: vi.fn(),
    cashSeries: vi.fn(),
    cashBreakdown: vi.fn(),
    debtSummary: vi.fn(),
    debtBreakdown: vi.fn(),
    inventorySummary: vi.fn(),
    inventoryBreakdown: vi.fn(),
    branches: vi.fn(),
    attention: vi.fn(),
    recent: vi.fn(),
  },
}));

type ApiName = keyof typeof executiveApi;
const apiMocks = executiveApi as unknown as Record<ApiName, Mock>;
const API_NAMES = Object.keys(executiveApi) as ApiName[];

const range = { from: '2026-10-01', to: '2026-10-13', branchId: 'b1' };
const series = { ...range, granularity: 'Week' as const };
const top = { ...range, by: 'Customer' as const, sort: 'GrossProfit' as const, limit: 5 };
const branch = { branchId: 'b1' };
const period = { from: '2026-10-01', to: '2026-10-13' };
const recent = { branchId: 'b1', limit: 10 };

interface Options { enabled?: boolean }
interface HookCase {
  hook: string;
  api: ApiName;
  url: string;
  params: object;
  key: QueryKey;
  expectedKey: unknown[];
  use: (options?: Options) => unknown;
}

const CASES: HookCase[] = [
  {
    hook: 'useSalesSummary', api: 'salesSummary', url: '/dashboard/executive/sales/summary', params: range,
    key: executiveKeys.salesSummary(range), expectedKey: ['executive', 'sales', 'summary', range],
    use: (o) => useSalesSummary(range, o),
  },
  {
    hook: 'useSalesSeries', api: 'salesSeries', url: '/dashboard/executive/sales/series', params: series,
    key: executiveKeys.salesSeries(series), expectedKey: ['executive', 'sales', 'series', series],
    use: (o) => useSalesSeries(series, o),
  },
  {
    hook: 'useSalesTop', api: 'salesTop', url: '/dashboard/executive/sales/top', params: top,
    key: executiveKeys.salesTop(top), expectedKey: ['executive', 'sales', 'top', top],
    use: (o) => useSalesTop(top, o),
  },
  {
    hook: 'useCashSummary', api: 'cashSummary', url: '/dashboard/executive/cash/summary', params: range,
    key: executiveKeys.cashSummary(range), expectedKey: ['executive', 'cash', 'summary', range],
    use: (o) => useCashSummary(range, o),
  },
  {
    hook: 'useCashSeries', api: 'cashSeries', url: '/dashboard/executive/cash/series', params: series,
    key: executiveKeys.cashSeries(series), expectedKey: ['executive', 'cash', 'series', series],
    use: (o) => useCashSeries(series, o),
  },
  {
    hook: 'useCashBreakdown', api: 'cashBreakdown', url: '/dashboard/executive/cash/breakdown', params: range,
    key: executiveKeys.cashBreakdown(range), expectedKey: ['executive', 'cash', 'breakdown', range],
    use: (o) => useCashBreakdown(range, o),
  },
  {
    hook: 'useDebtSummary', api: 'debtSummary', url: '/dashboard/executive/debt/summary', params: branch,
    key: executiveKeys.debtSummary(branch), expectedKey: ['executive', 'debt', 'summary', branch],
    use: (o) => useDebtSummary(branch, o),
  },
  {
    hook: 'useDebtBreakdown', api: 'debtBreakdown', url: '/dashboard/executive/debt/breakdown', params: branch,
    key: executiveKeys.debtBreakdown(branch), expectedKey: ['executive', 'debt', 'breakdown', branch],
    use: (o) => useDebtBreakdown(branch, o),
  },
  {
    hook: 'useInventorySummary', api: 'inventorySummary', url: '/dashboard/executive/inventory/summary', params: range,
    key: executiveKeys.inventorySummary(range), expectedKey: ['executive', 'inventory', 'summary', range],
    use: (o) => useInventorySummary(range, o),
  },
  {
    hook: 'useInventoryBreakdown', api: 'inventoryBreakdown', url: '/dashboard/executive/inventory/breakdown', params: branch,
    key: executiveKeys.inventoryBreakdown(branch), expectedKey: ['executive', 'inventory', 'breakdown', branch],
    use: (o) => useInventoryBreakdown(branch, o),
  },
  {
    hook: 'useBranchComparison', api: 'branches', url: '/dashboard/executive/branches', params: period,
    key: executiveKeys.branches(period), expectedKey: ['executive', 'cross', 'branches', period],
    use: (o) => useBranchComparison(period, o),
  },
  {
    hook: 'useAttention', api: 'attention', url: '/dashboard/executive/attention', params: branch,
    key: executiveKeys.attention(branch), expectedKey: ['executive', 'cross', 'attention', branch],
    use: (o) => useAttention(branch, o),
  },
  {
    hook: 'useRecentDocuments', api: 'recent', url: '/dashboard/executive/recent', params: recent,
    key: executiveKeys.recent(recent), expectedKey: ['executive', 'cross', 'recent', recent],
    use: (o) => useRecentDocuments(recent, o),
  },
];

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { qc, wrapper };
}

const tick = () => new Promise((resolve) => setTimeout(resolve, 20));

beforeEach(() => {
  for (const name of API_NAMES) apiMocks[name].mockReset().mockResolvedValue({});
  vi.mocked(apiGet).mockReset();
});

describe('executive dashboard hooks', () => {
  it('useSalesSummary calls the api with its params and exposes the data', async () => {
    const data = { revenue: 2_840_000_000, prevRevenue: 2_500_000_000 };
    apiMocks.salesSummary.mockResolvedValue(data);
    const { wrapper } = setup();
    const params = { from: '2026-10-01', to: '2026-10-13' };

    const { result } = renderHook(() => useSalesSummary(params), { wrapper });

    await waitFor(() => expect(result.current.data).toBe(data));
    expect(executiveApi.salesSummary).toHaveBeenCalledWith(params);
  });

  it('useDebtSummary does not call the api when enabled is false', async () => {
    const { wrapper } = setup();

    const { result } = renderHook(() => useDebtSummary({}, { enabled: false }), { wrapper });

    await tick();
    expect(executiveApi.debtSummary).not.toHaveBeenCalled();
    expect(result.current.fetchStatus).toBe('idle');
  });

  it('there is one hook per api function', () => {
    expect(CASES.map((c) => c.api).sort()).toEqual([...API_NAMES].sort());
  });

  it.each(CASES)('$hook calls only $api, under its own key, with the shared options', async (c) => {
    const { qc, wrapper } = setup();

    renderHook(() => c.use(), { wrapper });

    await waitFor(() => expect(apiMocks[c.api]).toHaveBeenCalledTimes(1));
    expect(apiMocks[c.api]).toHaveBeenCalledWith(c.params);
    for (const other of API_NAMES.filter((name) => name !== c.api)) {
      expect(apiMocks[other]).not.toHaveBeenCalled();
    }
    const queries = qc.getQueryCache().findAll();
    expect(queries.map((q) => q.queryKey)).toEqual([c.key]);
    expect(queries[0].observers[0].options).toMatchObject(executiveQueryOptions);
  });

  it.each(CASES)('$hook does not call the api when enabled is false', async (c) => {
    const { wrapper } = setup();

    renderHook(() => c.use({ enabled: false }), { wrapper });

    await tick();
    expect(apiMocks[c.api]).not.toHaveBeenCalled();
  });
});

describe('executiveQueryOptions', () => {
  it('refetches every minute while the tab is visible and keeps data fresh for 30 seconds', () => {
    expect(executiveQueryOptions.refetchInterval).toBe(60_000);
    expect(executiveQueryOptions.refetchIntervalInBackground).toBe(false);
    expect(executiveQueryOptions.staleTime).toBe(30_000);
  });

  it('keeps the data of the previous key while a new key loads', () => {
    expect(executiveQueryOptions.placeholderData).toBe(keepPreviousData);
  });
});

// A tile must not fall back to its skeleton when the period, the scope or the granularity changes.
describe('a change of the query key', () => {
  it('shows the previous data as placeholder until the new data arrives', async () => {
    const day = { granularity: 'Day', points: [{ date: '2026-10-01', revenue: 1, cost: null, grossProfit: null }] };
    const week = { granularity: 'Week', points: [{ date: '2026-09-28', revenue: 7, cost: null, grossProfit: null }] };
    let resolveWeek!: (value: typeof week) => void;
    apiMocks.salesSeries.mockImplementation((params: { granularity: string }) =>
      params.granularity === 'Day'
        ? Promise.resolve(day)
        : new Promise((resolve) => {
            resolveWeek = resolve;
          }),
    );
    const { wrapper } = setup();

    const { result, rerender } = renderHook(
      ({ granularity }: { granularity: 'Day' | 'Week' }) => useSalesSeries({ ...range, granularity }),
      { wrapper, initialProps: { granularity: 'Day' } },
    );
    // The first load has nothing to keep: the tile shows its skeleton.
    expect(result.current.isLoading).toBe(true);
    expect(result.current.data).toBeUndefined();
    await waitFor(() => expect(result.current.data).toBe(day));
    expect(result.current.isPlaceholderData).toBe(false);

    rerender({ granularity: 'Week' });

    expect(result.current.data).toBe(day);
    expect(result.current.isPlaceholderData).toBe(true);
    expect(result.current.isLoading).toBe(false);
    expect(result.current.isFetching).toBe(true);

    resolveWeek(week);
    await waitFor(() => expect(result.current.data).toBe(week));
    expect(result.current.isPlaceholderData).toBe(false);
  });

  it('a failed load of the new key leaves no data, so the tile can show the error', async () => {
    apiMocks.debtSummary.mockImplementation((params: { branchId?: string }) =>
      params.branchId === 'b1' ? Promise.resolve({ at: '2026-10-13' }) : Promise.reject(new Error('no')),
    );
    const { wrapper } = setup();

    const { result, rerender } = renderHook(({ branchId }: { branchId: string }) => useDebtSummary({ branchId }), {
      wrapper,
      initialProps: { branchId: 'b1' },
    });
    await waitFor(() => expect(result.current.data).toEqual({ at: '2026-10-13' }));

    rerender({ branchId: 'b2' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.data).toBeUndefined();
    expect(result.current.isPlaceholderData).toBe(false);
  });
});

describe('executiveKeys', () => {
  it('the root and the domain keys', () => {
    expect(executiveKeys.all).toEqual(['executive']);
    for (const domain of ['sales', 'cash', 'debt', 'inventory', 'cross'] as const) {
      expect(executiveKeys.domain(domain)).toEqual(['executive', domain]);
    }
  });

  it.each(CASES)('$api starts with its domain key and carries its params', (c) => {
    expect(c.key.slice(0, 2)).toEqual(executiveKeys.domain(c.expectedKey[1] as 'sales' | 'cash' | 'debt' | 'inventory' | 'cross'));
    expect(c.key).toEqual(c.expectedKey);
  });

  it('branches, attention and recent live under the cross key', () => {
    for (const key of [executiveKeys.branches(period), executiveKeys.attention(branch), executiveKeys.recent(recent)]) {
      expect(key.slice(0, 2)).toEqual(['executive', 'cross']);
    }
  });

  it('no two routes share a key prefix', () => {
    const prefixes = CASES.map((c) => c.key.slice(0, 3).join('/'));
    expect(new Set(prefixes).size).toBe(CASES.length);
  });
});

// The real api module (the hooks above run against the mocked one).
describe('executiveApi', () => {
  it('has one function per route', async () => {
    const { executiveApi: real } = await vi.importActual<typeof ApiModule>('./api');
    expect(Object.keys(real).sort()).toEqual([...API_NAMES].sort());
  });

  it.each(CASES)('$api requests $url with its params as they are (no "at")', async (c) => {
    const { executiveApi: real } = await vi.importActual<typeof ApiModule>('./api');
    const payload = { ok: true };
    vi.mocked(apiGet).mockResolvedValue(payload);

    const call = real[c.api] as (params: object) => Promise<unknown>;
    await expect(call(c.params)).resolves.toBe(payload);

    expect(apiGet).toHaveBeenCalledTimes(1);
    expect(apiGet).toHaveBeenCalledWith(c.url, c.params);
    expect(Object.keys(vi.mocked(apiGet).mock.calls[0][1] as object)).not.toContain('at');
  });
});
