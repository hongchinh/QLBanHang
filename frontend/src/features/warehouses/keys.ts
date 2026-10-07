import type { WarehouseListParams } from './types';

export const warehouseKeys = {
  all: ['warehouses'] as const,
  lists: () => [...warehouseKeys.all, 'list'] as const,
  list: (params?: WarehouseListParams) => [...warehouseKeys.lists(), params ?? {}] as const,
};
