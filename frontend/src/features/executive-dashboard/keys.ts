import type {
  BranchParam,
  ExecutiveDomain,
  ExecutiveParams,
  SalesTopBy,
  SalesTopSort,
  SeriesGranularity,
} from './types';

// One root for the whole dashboard and one sub-root per domain, so a "dashboard changed" signal refetches
// only the domains it names. `cross` holds the queries that read several domains (branches, attention, recent):
// they are refetched on every signal.
export const executiveKeys = {
  all: ['executive'] as const,
  domain: (d: ExecutiveDomain | 'cross') => [...executiveKeys.all, d] as const,
  salesSummary: (p: ExecutiveParams) => [...executiveKeys.domain('sales'), 'summary', p] as const,
  salesSeries: (p: ExecutiveParams & { granularity: SeriesGranularity }) =>
    [...executiveKeys.domain('sales'), 'series', p] as const,
  salesTop: (p: ExecutiveParams & { by: SalesTopBy; sort?: SalesTopSort; limit?: number }) =>
    [...executiveKeys.domain('sales'), 'top', p] as const,
  cashSummary: (p: ExecutiveParams) => [...executiveKeys.domain('cash'), 'summary', p] as const,
  cashSeries: (p: ExecutiveParams & { granularity: SeriesGranularity }) =>
    [...executiveKeys.domain('cash'), 'series', p] as const,
  cashBreakdown: (p: ExecutiveParams) => [...executiveKeys.domain('cash'), 'breakdown', p] as const,
  debtSummary: (p: BranchParam) => [...executiveKeys.domain('debt'), 'summary', p] as const,
  debtBreakdown: (p: BranchParam) => [...executiveKeys.domain('debt'), 'breakdown', p] as const,
  inventorySummary: (p: ExecutiveParams) => [...executiveKeys.domain('inventory'), 'summary', p] as const,
  inventoryBreakdown: (p: BranchParam) => [...executiveKeys.domain('inventory'), 'breakdown', p] as const,
  branches: (p: { from: string; to: string }) => [...executiveKeys.domain('cross'), 'branches', p] as const,
  attention: (p: BranchParam) => [...executiveKeys.domain('cross'), 'attention', p] as const,
  recent: (p: BranchParam & { limit?: number }) => [...executiveKeys.domain('cross'), 'recent', p] as const,
};
