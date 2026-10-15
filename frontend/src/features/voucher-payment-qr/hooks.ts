import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { voucherPaymentQrApi } from './api';
import { voucherPaymentQrKeys } from './keys';
import type { PaymentQrKind, VoucherPaymentQr } from './types';

// The QR depends on the live fund and voucher, so it is read afresh on every open. A 422
// (PAYMENT_QR_UNAVAILABLE) is an answer, not a failure, so there is no retry.
export function useVoucherPaymentQr(
  kind: PaymentQrKind,
  id: string | undefined,
  options: { enabled: boolean },
): UseQueryResult<VoucherPaymentQr> {
  return useQuery({
    queryKey: voucherPaymentQrKeys.detail(kind, id ?? ''),
    queryFn: () => voucherPaymentQrApi.get(kind, id as string),
    enabled: options.enabled && !!id,
    staleTime: 0,
    gcTime: 0,
    retry: false,
    refetchOnMount: 'always',
  });
}
