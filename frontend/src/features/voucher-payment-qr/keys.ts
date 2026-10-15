import { cashKeys } from '@/features/cash-common/keys';
import type { PaymentQrKind } from './types';

// Under the cash root, so every cash and stock voucher mutation (which invalidates that root) also drops these queries.
export const voucherPaymentQrKeys = {
  all: [...cashKeys.all, 'payment-qr'] as const,
  detail: (kind: PaymentQrKind, id: string) => [...voucherPaymentQrKeys.all, kind, id] as const,
};
