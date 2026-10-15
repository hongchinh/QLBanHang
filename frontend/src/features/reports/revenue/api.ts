import { apiGet } from '@/lib/api-client';
import type { DashboardParams, Granularity } from '@/features/dashboard/types';
import type { QuotationRevenueSeries, QuotationRevenueSummary, QuotationTopCustomer } from './types';

export const revenueReportApi = {
  getSummary: (p: { from?: string; to?: string; saleUserId?: string }) =>
    apiGet<QuotationRevenueSummary>('/reports/revenue/summary', p),

  getSeries: (p: DashboardParams & { granularity: Granularity }) =>
    apiGet<QuotationRevenueSeries>('/reports/revenue/series', p),

  getTopCustomers: (p: DashboardParams & { limit?: number }) =>
    apiGet<QuotationTopCustomer[]>('/reports/revenue/top-customers', p),
};
