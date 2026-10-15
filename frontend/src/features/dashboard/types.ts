export interface QuotationStats {
  totalCount: number;
  draftCount: number;
  sentCount: number;
  confirmedCount: number;
  convertedCount: number;
  cancelledCount: number;
  totalRevenue: number;
  todayRevenue: number;
  from: string;
  to: string;
}

export interface Kpi {
  value: number;
  deltaPct: number | null;
  spark: number[];
}

export interface Funnel {
  draft: number;
  sent: number;
  confirmed: number;
  cancelled: number;
  sentRate: number | null;
  confirmRate: number | null;
}

export interface DashboardSummary {
  from: string;
  to: string;
  prevFrom: string;
  prevTo: string;
  todayRevenue: Kpi;
  rangeRevenue: Kpi;
  totalCount: Kpi;
  cancelledCount: Kpi;
  funnel: Funnel;
}

export interface RevenuePoint {
  date: string;
  total: number;
  voucherCount: number;
}

export interface RevenueSeries {
  points: RevenuePoint[];
}

export interface TopCustomer {
  customerId: string;
  customerName: string;
  revenue: number;
  voucherCount: number;
}

export interface TopProduct {
  productId: string | null;
  productName: string;
  revenue: number;
  quantity: number;
}

export interface ActivityItem {
  at: string;
  type: 'created' | 'confirmed' | 'cancelled';
  quotationId: string;
  code: string;
  customerName: string;
  actorName: string | null;
  amount: number | null;
}

export interface SalesLeaderboardItem {
  userId: string;
  fullName: string;
  revenue: number;
  voucherCount: number;
  deltaPct: number | null;
}

export type Granularity = 'day' | 'week' | 'month';

export interface DashboardParams {
  from: string;
  to: string;
  saleUserId?: string;
}
