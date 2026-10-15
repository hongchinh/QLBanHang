import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from './api';
import { dashboardKeys } from './keys';
import type { DashboardParams, Granularity } from './types';

export function useQuotationStats(params?: { from?: string; to?: string }) {
  return useQuery({
    queryKey: dashboardKeys.quotationStats(params),
    queryFn: () => dashboardApi.getQuotationStats(params),
  });
}

export function useDashboardSummary(params: { from?: string; to?: string; saleUserId?: string }) {
  return useQuery({
    queryKey: dashboardKeys.summary(params),
    queryFn: () => dashboardApi.getSummary(params),
    staleTime: 30_000,
  });
}

export function useRevenueSeries(params: DashboardParams & { granularity: Granularity }) {
  return useQuery({
    queryKey: dashboardKeys.revenueSeries(params),
    queryFn: () => dashboardApi.getRevenueSeries(params),
    staleTime: 30_000,
  });
}

export function useTopCustomers(params: DashboardParams & { limit?: number }) {
  return useQuery({
    queryKey: dashboardKeys.topCustomers(params),
    queryFn: () => dashboardApi.getTopCustomers(params),
    staleTime: 30_000,
  });
}

export function useTopProducts(params: DashboardParams & { limit?: number }) {
  return useQuery({
    queryKey: dashboardKeys.topProducts(params),
    queryFn: () => dashboardApi.getTopProducts(params),
    staleTime: 30_000,
  });
}

export function useRecentActivity(params: { limit?: number }) {
  return useQuery({
    queryKey: dashboardKeys.recentActivity(params),
    queryFn: () => dashboardApi.getRecentActivity(params),
    staleTime: 30_000,
  });
}

export function useLeaderboard(params: { from: string; to: string; limit?: number }, enabled = true) {
  return useQuery({
    queryKey: dashboardKeys.leaderboard(params),
    queryFn: () => dashboardApi.getLeaderboard(params),
    staleTime: 30_000,
    enabled,
  });
}
