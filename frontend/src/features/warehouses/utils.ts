import type { Warehouse } from './types';

// Pickers offer active warehouses only; an inactive one stays listed while it is the selected value.
export function selectableWarehouses(warehouses: readonly Warehouse[], selectedId?: string | null): Warehouse[] {
  return warehouses.filter((w) => w.isActive || w.id === selectedId);
}
