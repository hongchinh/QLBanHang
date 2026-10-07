import { apiGet } from '@/lib/api-client';
import type { StockCard, StockCardParams, StockOnHandParams, StockOnHandReport } from './types';

export const inventoryReportsApi = {
  stockOnHand: (params: StockOnHandParams) => apiGet<StockOnHandReport>('/reports/stock-on-hand', params),
  stockCard: (params: StockCardParams) => apiGet<StockCard>('/reports/stock-card', params),
};
