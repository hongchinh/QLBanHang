import { useMutation } from '@tanstack/react-query';
import { paymentQrApi } from './api';
import type { GenerateQrRequest } from './types';

export function useGenerateQr() {
  return useMutation({
    mutationFn: (data: GenerateQrRequest) => paymentQrApi.generate(data),
  });
}
