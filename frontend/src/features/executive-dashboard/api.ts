import { apiGet } from '@/lib/api-client';
import type {
  Attention,
  BranchComparisonRow,
  BranchParam,
  CashBreakdown,
  CashSeries,
  CashSummary,
  DebtBreakdown,
  DebtSummary,
  ExecutiveParams,
  InventoryBreakdown,
  InventorySummary,
  RecentDocument,
  SalesSeries,
  SalesSummary,
  SalesTopBy,
  SalesTopRow,
  SalesTopSort,
  SeriesGranularity,
} from './types';

// GET /api/dashboard/executive/*, one function per route. The as-at date of the balance figures (`at`) is
// never sent: the server uses VN today.
export const executiveApi = {
  salesSummary: (params: ExecutiveParams) => apiGet<SalesSummary>('/dashboard/executive/sales/summary', params),
  salesSeries: (params: ExecutiveParams & { granularity: SeriesGranularity }) =>
    apiGet<SalesSeries>('/dashboard/executive/sales/series', params),
  salesTop: (params: ExecutiveParams & { by: SalesTopBy; sort?: SalesTopSort; limit?: number }) =>
    apiGet<SalesTopRow[]>('/dashboard/executive/sales/top', params),
  cashSummary: (params: ExecutiveParams) => apiGet<CashSummary>('/dashboard/executive/cash/summary', params),
  cashSeries: (params: ExecutiveParams & { granularity: SeriesGranularity }) =>
    apiGet<CashSeries>('/dashboard/executive/cash/series', params),
  cashBreakdown: (params: ExecutiveParams) => apiGet<CashBreakdown>('/dashboard/executive/cash/breakdown', params),
  debtSummary: (params: BranchParam) => apiGet<DebtSummary>('/dashboard/executive/debt/summary', params),
  debtBreakdown: (params: BranchParam) => apiGet<DebtBreakdown>('/dashboard/executive/debt/breakdown', params),
  inventorySummary: (params: ExecutiveParams) =>
    apiGet<InventorySummary>('/dashboard/executive/inventory/summary', params),
  inventoryBreakdown: (params: BranchParam) =>
    apiGet<InventoryBreakdown>('/dashboard/executive/inventory/breakdown', params),
  branches: (params: { from: string; to: string }) =>
    apiGet<BranchComparisonRow[]>('/dashboard/executive/branches', params),
  attention: (params: BranchParam) => apiGet<Attention>('/dashboard/executive/attention', params),
  recent: (params: BranchParam & { limit?: number }) =>
    apiGet<RecentDocument[]>('/dashboard/executive/recent', params),
};
