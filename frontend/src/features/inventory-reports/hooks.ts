import { useQuery } from '@tanstack/react-query';
import { inventoryReportsApi } from './api';
import { inventoryReportKeys } from './keys';
import type { StockCardParams, StockOnHandParams } from './types';

export function useStockOnHand(params: StockOnHandParams) {
  return useQuery({
    queryKey: inventoryReportKeys.stockOnHand(params),
    queryFn: () => inventoryReportsApi.stockOnHand(params),
  });
}

export function useStockCard(params: StockCardParams) {
  return useQuery({
    queryKey: inventoryReportKeys.stockCard(params),
    queryFn: () => inventoryReportsApi.stockCard(params),
    enabled: !!params.productId,
  });
}
