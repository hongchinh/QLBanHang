import { apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  DocumentNumbering,
  DocumentType,
  InventorySettings,
  RecalcCostRequest,
  RecalcCostResult,
  UpdateInventorySettingsRequest,
  UpdateNumberingRequest,
} from './types';

// A costing change and a manual recalculation run synchronously on the server and can outlast
// the default 30 s client timeout.
const RECALC_TIMEOUT_MS = 300_000;

export const inventorySettingsApi = {
  getSettings: () => apiGet<InventorySettings>('/inventory/settings'),
  updateSettings: (data: UpdateInventorySettingsRequest) =>
    apiPut<InventorySettings>('/inventory/settings', data, { timeout: RECALC_TIMEOUT_MS }),
  listNumbering: () => apiGet<DocumentNumbering[]>('/inventory/numbering'),
  updateNumbering: (docType: DocumentType, data: UpdateNumberingRequest) =>
    apiPut<DocumentNumbering>(`/inventory/numbering/${docType}`, data),
  recalcCost: (data: RecalcCostRequest) =>
    apiPost<RecalcCostResult>('/inventory/recalc-cost', data, { timeout: RECALC_TIMEOUT_MS }),
};
