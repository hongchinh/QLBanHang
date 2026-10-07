import { inventoryKeys } from '@/features/stock-vouchers/keys';
import type { WarehouseListParams } from './types';

// Warehouses are branch-scoped, so they live under the shared inventory root and are dropped
// together with every other inventory query.
export const warehouseKeys = {
  all: [...inventoryKeys.all, 'warehouses'] as const,
  lists: () => [...warehouseKeys.all, 'list'] as const,
  list: (params?: WarehouseListParams) => [...warehouseKeys.lists(), params ?? {}] as const,
};
