import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashSettingsApi } from './api';
import type { UpdateCashSettingsRequest } from './types';

// A global singleton (not branch-scoped), so it sits outside the ['cash'] root (P28).
export const cashSettingsKeys = { all: ['cash-settings'] as const };

export function useCashSettings() {
  return useQuery({
    queryKey: cashSettingsKeys.all,
    queryFn: () => cashSettingsApi.get(),
  });
}

export function useUpdateCashSettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateCashSettingsRequest) => cashSettingsApi.update(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: cashSettingsKeys.all }),
  });
}
