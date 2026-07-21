import { apiPost } from '@/lib/api-client';
import type { GenerateQrRequest, GenerateQrResponse } from './types';

export const paymentQrApi = {
  generate: (data: GenerateQrRequest) => apiPost<GenerateQrResponse>('/payment-qr/generate', data),
};
