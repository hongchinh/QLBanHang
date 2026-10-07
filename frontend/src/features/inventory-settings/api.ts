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

export const inventorySettingsApi = {
  getSettings: () => apiGet<InventorySettings>('/inventory/settings'),
  updateSettings: (data: UpdateInventorySettingsRequest) =>
    apiPut<InventorySettings>('/inventory/settings', data),
  listNumbering: () => apiGet<DocumentNumbering[]>('/inventory/numbering'),
  updateNumbering: (docType: DocumentType, data: UpdateNumberingRequest) =>
    apiPut<DocumentNumbering>(`/inventory/numbering/${docType}`, data),
  recalcCost: (data: RecalcCostRequest) => apiPost<RecalcCostResult>('/inventory/recalc-cost', data),
};
