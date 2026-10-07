import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type {
  CreatePaymentMethodRequest,
  PaymentMethod,
  UpdatePaymentMethodRequest,
} from './types';

export const paymentMethodsApi = {
  list: () => apiGet<PaymentMethod[]>('/payment-methods'),
  get: (id: string) => apiGet<PaymentMethod>(`/payment-methods/${id}`),
  create: (data: CreatePaymentMethodRequest) => apiPost<PaymentMethod>('/payment-methods', data),
  update: (id: string, data: UpdatePaymentMethodRequest) =>
    apiPut<PaymentMethod>(`/payment-methods/${id}`, data),
  remove: (id: string) => apiDelete(`/payment-methods/${id}`),
};
