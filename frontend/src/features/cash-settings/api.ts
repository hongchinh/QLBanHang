import { apiGet, apiPut } from '@/lib/api-client';
import type { CashSettings, UpdateCashSettingsRequest } from './types';

export const cashSettingsApi = {
  get: () => apiGet<CashSettings>('/cash/settings'),
  update: (data: UpdateCashSettingsRequest) => apiPut<CashSettings>('/cash/settings', data),
};
