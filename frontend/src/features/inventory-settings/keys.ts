// Nested under the shared inventory root ['inventory'] (Phase 09 exports it as inventoryKeys.all),
// so a costing change invalidates vouchers, reports and these settings together.
const INVENTORY_ROOT = ['inventory'] as const;

export const inventorySettingsKeys = {
  root: INVENTORY_ROOT,
  settings: () => [...INVENTORY_ROOT, 'settings'] as const,
  numbering: () => [...INVENTORY_ROOT, 'numbering'] as const,
};
