import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { warehousesApi } from './api';
import { warehouseKeys } from './keys';
import type {
  CreateWarehouseRequest,
  UpdateWarehouseRequest,
  WarehouseListParams,
} from './types';

export function useWarehouses(params?: WarehouseListParams) {
  return useQuery({
    queryKey: warehouseKeys.list(params),
    queryFn: () => warehousesApi.list(params),
  });
}

export function useCreateWarehouse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateWarehouseRequest) => warehousesApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: warehouseKeys.all }),
  });
}

export function useUpdateWarehouse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateWarehouseRequest }) =>
      warehousesApi.update(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: warehouseKeys.all }),
  });
}

export function useDeleteWarehouse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => warehousesApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: warehouseKeys.all }),
  });
}
