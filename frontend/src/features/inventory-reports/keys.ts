import { inventoryKeys } from '@/features/stock-vouchers/keys';
import type { StockCardParams, StockOnHandParams } from './types';

// Under the shared inventory root, so voucher and opening-stock saves refresh every report.
export const inventoryReportKeys = {
  all: [...inventoryKeys.all, 'reports'] as const,
  stockOnHand: (params: StockOnHandParams) => [...inventoryReportKeys.all, 'stock-on-hand', params] as const,
  stockCard: (params: StockCardParams) => [...inventoryReportKeys.all, 'stock-card', params] as const,
};

export const openingStockKeys = {
  all: [...inventoryKeys.all, 'opening-stock'] as const,
  grid: (warehouseId: string) => [...openingStockKeys.all, warehouseId] as const,
};
