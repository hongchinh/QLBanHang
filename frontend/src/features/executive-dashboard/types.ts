import type { CashVoucherType, DebtSourceType, FundKind } from '@/features/cash-common/types';
import type { AgingBucket } from '@/features/cash-reports/types';

// DTOs of GET /api/dashboard/executive/* (backend Application/Dashboards/Executive/Models/*.cs), one interface
// per DTO with the "Dto" suffix dropped. Dates are VN yyyy-MM-dd, instants are ISO strings, enums are their names.
// The API writes null values, so a nullable backend property is `| null` here (never left out).

export type ExecutiveDomain = 'sales' | 'cash' | 'debt' | 'inventory';
// Every domain, in the order of the tabs.
export const EXECUTIVE_DOMAINS: readonly ExecutiveDomain[] = ['sales', 'cash', 'debt', 'inventory'];
export type ExecutiveTab = 'overview' | ExecutiveDomain;
export type ExecutiveScopeMode = 'company' | 'branch';
export type SeriesGranularity = 'Day' | 'Week' | 'Month';
export type SalesTopBy = 'Customer' | 'Product' | 'Employee';
export type SalesTopSort = 'Revenue' | 'GrossProfit';
export type RecentDocumentKind = 'Receipt' | 'Payment' | 'StockIn' | 'StockOut';

// Period figures. branchId omitted: every branch the user may see.
export interface ExecutiveParams {
  from: string;
  to: string;
  branchId?: string;
}

// Balance figures ("as at" today on the server).
export interface BranchParam {
  branchId?: string;
}

// sales/summary (reports.profit)
export interface SalesSummary {
  from: string;
  to: string;
  prevFrom: string;
  prevTo: string;
  revenue: number;
  prevRevenue: number;
  revenueDeltaPct: number | null;
  canViewCost: boolean;
  // Cost, gross profit and the margins are null without inventory.view_cost.
  cost: number | null;
  grossProfit: number | null;
  marginPct: number | null;
  prevMarginPct: number | null;
  isProvisional: boolean;
}

// sales/series
export interface SalesSeries {
  granularity: SeriesGranularity;
  points: SalesPoint[];
}

export interface SalesPoint {
  // Bucket start.
  date: string;
  revenue: number;
  cost: number | null;
  grossProfit: number | null;
}

// sales/top
export interface SalesTopRow {
  // Partner, product or user id.
  id: string;
  // Partner or product code; null for employees.
  code: string | null;
  name: string;
  revenue: number;
  grossProfit: number | null;
}

// cash/summary (reports.cash)
export interface CashSummary {
  from: string;
  to: string;
  prevFrom: string;
  prevTo: string;
  // As-at date of the balances.
  at: string;
  receipts: number;
  // Positive.
  payments: number;
  // receipts − payments.
  net: number;
  prevNet: number;
  netDeltaPct: number | null;
  balance: number;
  cashBalance: number;
  bankBalance: number;
  funds: FundBalance[];
}

export interface FundBalance {
  fundId: string;
  branchId: string;
  branchCode: string;
  code: string;
  name: string;
  kind: FundKind;
  isActive: boolean;
  balance: number;
}

// cash/series
export interface CashSeries {
  granularity: SeriesGranularity;
  points: CashPoint[];
}

export interface CashPoint {
  // Bucket start.
  date: string;
  receipts: number;
  // Positive.
  payments: number;
}

// cash/breakdown
export interface CashBreakdown {
  // Receipts first, then payments; the largest total first.
  byReason: CashReasonTotal[];
  // The ten largest receipts and payments of the period.
  largest: CashDocument[];
}

export interface CashReasonTotal {
  reasonId: string;
  reasonCode: string | null;
  reasonName: string | null;
  type: CashVoucherType;
  total: number;
  count: number;
}

export interface CashDocument {
  id: string;
  type: CashVoucherType;
  code: string;
  at: string;
  branchCode: string;
  partnerOrPerson: string | null;
  description: string | null;
  // Positive.
  total: number;
}

// debt/summary (reports.debt)
export interface DebtSummary {
  at: string;
  // The overdue buckets of the Nợ quá hạn report (1–30, 31–60, 61–90, 91+).
  overdueBuckets: AgingBucket[];
  receivable: DebtSideSummary;
  payable: DebtSideSummary;
}

export interface DebtSideSummary {
  // totalOpen − unallocated.
  balance: number;
  totalOpen: number;
  unallocated: number;
  // totalOpen − overdue.
  notDue: number;
  overdue: number;
  overduePartnerCount: number;
  // One amount per overdue bucket.
  overdueAmounts: number[];
  // Due within the next 7 days: amount and number of documents.
  dueSoon: number;
  dueSoonCount: number;
}

// debt/breakdown
export interface DebtBreakdown {
  at: string;
  topDebtors: DebtPartnerRow[];
  overLimit: CreditLimitBreach[];
  byEmployee: DebtEmployeeRow[];
  payablesDue: PayableDueRow[];
}

export interface DebtPartnerRow {
  partnerId: string;
  partnerCode: string | null;
  partnerName: string | null;
  balance: number;
  overdue: number;
}

export interface CreditLimitBreach {
  partnerId: string;
  partnerCode: string | null;
  partnerName: string | null;
  branchId: string;
  branchCode: string;
  balance: number;
  limit: number;
  // balance − limit.
  over: number;
}

export interface DebtEmployeeRow {
  // null: documents without an owner.
  ownerUserId: string | null;
  ownerName: string | null;
  open: number;
  overdue: number;
}

export interface PayableDueRow {
  partnerId: string;
  partnerCode: string | null;
  partnerName: string | null;
  branchId: string;
  branchCode: string;
  sourceType: DebtSourceType;
  sourceId: string;
  docCode: string;
  dueDate: string;
  open: number;
}

// inventory/summary (reports.inventory)
export interface InventorySummary {
  from: string;
  to: string;
  canViewCost: boolean;
  // Now; null without inventory.view_cost.
  stockValue: number | null;
  // Products with a non-zero quantity.
  productCount: number;
  // Warehouses holding stock.
  warehouseCount: number;
  // Product × warehouse pairs below zero.
  negativeCount: number;
  // Period: value received by stock-in vouchers and cost issued by stock-out vouchers.
  inValue: number | null;
  outCost: number | null;
}

// inventory/breakdown
export interface InventoryBreakdown {
  canViewCost: boolean;
  byWarehouse: StockGroup[];
  byProductGroup: StockGroup[];
  negative: NegativeStockRow[];
  topByValue: StockProductRow[];
}

export interface StockGroup {
  // Warehouse id or product group id; null: products without a group.
  id: string | null;
  // Warehouse code; null for product groups.
  code: string | null;
  name: string | null;
  // Warehouses only.
  branchCode: string | null;
  productCount: number;
  value: number | null;
}

export interface NegativeStockRow {
  productId: string;
  productCode: string;
  productName: string;
  unitName: string | null;
  warehouseId: string;
  warehouseCode: string;
  branchCode: string;
  quantity: number;
}

export interface StockProductRow {
  productId: string;
  code: string;
  name: string;
  unitName: string | null;
  quantity: number;
  value: number | null;
}

// branches (branches.access_all). A column is null when the user may not see its domain.
export interface BranchComparisonRow {
  branchId: string;
  branchCode: string;
  branchName: string;
  revenue: number | null;
  marginPct: number | null;
  receivable: number | null;
  stockValue: number | null;
}

// attention. An item is null when the user may not see its domain.
export interface Attention {
  at: string;
  // count = customers, amount = overdue amount.
  overdueReceivables: AttentionItem | null;
  // count = customer × branch pairs, amount = the total above the limits.
  overCreditLimit: AttentionItem | null;
  // count = documents due within 7 days.
  payablesDueSoon: AttentionItem | null;
  // count = product × warehouse pairs; no amount.
  negativeStock: AttentionItem | null;
  // count = funds, amount = the total below zero (positive).
  negativeFunds: AttentionItem | null;
}

export interface AttentionItem {
  count: number;
  amount: number | null;
}

// recent: documents created today, newest first.
export interface RecentDocument {
  kind: RecentDocumentKind;
  id: string;
  code: string;
  createdAt: string;
  branchCode: string;
  partnerName: string | null;
  ownerName: string | null;
  amount: number;
}
