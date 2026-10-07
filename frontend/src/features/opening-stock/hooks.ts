import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { openingStockKeys } from '@/features/inventory-reports/keys';
import { inventoryKeys } from '@/features/stock-vouchers/keys';
import { openingStockApi } from './api';
import type { SaveOpeningStockRequest } from './types';

export function useOpeningStock(warehouseId?: string) {
  return useQuery({
    queryKey: openingStockKeys.grid(warehouseId ?? ''),
    queryFn: () => openingStockApi.get(warehouseId as string),
    enabled: !!warehouseId,
  });
}

export function useSaveOpeningStock() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveOpeningStockRequest) => openingStockApi.save(body),
    // Vouchers, stock-at, reports and opening stock share the inventory root.
    onSuccess: () => qc.invalidateQueries({ queryKey: inventoryKeys.all }),
  });
}
