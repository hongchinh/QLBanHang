import type { LimitPolicy } from '@/features/cash-common/types';

// Mirrors backend CashDebt/Settings/Models (CashDebtSettingsDto, UpdateCashDebtSettingsRequest).
export interface CashSettings {
  creditLimitPolicy: LimitPolicy;
  negativeFundPolicy: LimitPolicy;
  updatedAt?: string;
}

export type UpdateCashSettingsRequest = Omit<CashSettings, 'updatedAt'>;
