import { apiGet, apiPut } from '@/lib/api-client';
import type { OpeningStockGrid, SaveOpeningStockRequest } from './types';

export const openingStockApi = {
  get: (warehouseId: string) => apiGet<OpeningStockGrid>('/inventory/opening-stock', { warehouseId }),
  save: (body: SaveOpeningStockRequest) => apiPut<OpeningStockGrid>('/inventory/opening-stock', body),
};
