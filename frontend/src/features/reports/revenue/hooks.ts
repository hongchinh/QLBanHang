import { useQuery } from '@tanstack/react-query';
import type { DashboardParams, Granularity } from '@/features/dashboard/types';
import { revenueReportApi } from './api';
import { revenueReportKeys } from './keys';

export function useRevenueReportSummary(params: { from?: string; to?: string; saleUserId?: string }) {
  return useQuery({
    queryKey: revenueReportKeys.summary(params),
    queryFn: () => revenueReportApi.getSummary(params),
    staleTime: 30_000,
  });
}

export function useRevenueReportSeries(params: DashboardParams & { granularity: Granularity }) {
  return useQuery({
    queryKey: revenueReportKeys.series(params),
    queryFn: () => revenueReportApi.getSeries(params),
    staleTime: 30_000,
  });
}

export function useRevenueReportTopCustomers(params: DashboardParams & { limit?: number }) {
  return useQuery({
    queryKey: revenueReportKeys.topCustomers(params),
    queryFn: () => revenueReportApi.getTopCustomers(params),
    staleTime: 30_000,
  });
}
