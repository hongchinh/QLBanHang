import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cashSettingsApi } from './api';
import { cashSettingsKeys } from './keys';
import type { UpdateCashSettingsRequest } from './types';

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
