import { apiGet } from '@/lib/api-client';
import type {
  StockCard,
  StockCardParams,
  StockOnHandParams,
  StockOnHandReport,
  StockSummaryParams,
  StockSummaryReport,
} from './types';

export const inventoryReportsApi = {
  stockOnHand: (params: StockOnHandParams) => apiGet<StockOnHandReport>('/reports/stock-on-hand', params),
  stockCard: (params: StockCardParams) => apiGet<StockCard>('/reports/stock-card', params),
  stockSummary: (params: StockSummaryParams) => apiGet<StockSummaryReport>('/reports/stock-summary', params),
};
