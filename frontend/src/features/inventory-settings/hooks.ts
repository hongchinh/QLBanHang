import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { inventorySettingsApi } from './api';
import { inventorySettingsKeys } from './keys';
import type {
  DocumentType,
  RecalcCostRequest,
  UpdateInventorySettingsRequest,
  UpdateNumberingRequest,
} from './types';

export function useInventorySettings() {
  return useQuery({
    queryKey: inventorySettingsKeys.settings(),
    queryFn: () => inventorySettingsApi.getSettings(),
  });
}

// A costing change recalculates cost data, so everything under the inventory root is refetched.
export function useUpdateInventorySettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateInventorySettingsRequest) => inventorySettingsApi.updateSettings(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: inventorySettingsKeys.root }),
  });
}

export function useNumbering() {
  return useQuery({
    queryKey: inventorySettingsKeys.numbering(),
    queryFn: () => inventorySettingsApi.listNumbering(),
  });
}

export function useUpdateNumbering() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ docType, data }: { docType: DocumentType; data: UpdateNumberingRequest }) =>
      inventorySettingsApi.updateNumbering(docType, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: inventorySettingsKeys.numbering() }),
  });
}

export function useRecalcCost() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: RecalcCostRequest) => inventorySettingsApi.recalcCost(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: inventorySettingsKeys.root }),
  });
}
