import type { Kpi } from '@/features/dashboard/types';

// The Doanh thu report counts quotations; its field names differ from the overview dashboards on purpose.
export interface QuotationRevenueSummary {
  from: string;
  to: string;
  prevFrom: string;
  prevTo: string;
  rangeRevenue: Kpi;
  totalCount: Kpi;
  cancelledCount: Kpi;
}

export interface QuotationRevenuePoint {
  date: string;
  total: number;
  confirmedCount: number;
}

export interface QuotationRevenueSeries {
  points: QuotationRevenuePoint[];
}

export interface QuotationTopCustomer {
  customerId: string;
  customerName: string;
  revenue: number;
  quotationCount: number;
}
