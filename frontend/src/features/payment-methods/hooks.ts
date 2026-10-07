import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { paymentMethodsApi } from './api';
import { paymentMethodKeys } from './keys';
import type { CreatePaymentMethodRequest, UpdatePaymentMethodRequest } from './types';

export function usePaymentMethods() {
  return useQuery({
    queryKey: paymentMethodKeys.lists(),
    queryFn: () => paymentMethodsApi.list(),
  });
}

export function useCreatePaymentMethod() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePaymentMethodRequest) => paymentMethodsApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: paymentMethodKeys.all }),
  });
}

export function useUpdatePaymentMethod() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdatePaymentMethodRequest }) =>
      paymentMethodsApi.update(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: paymentMethodKeys.all }),
  });
}

export function useDeletePaymentMethod() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => paymentMethodsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: paymentMethodKeys.all }),
  });
}
