import { cashKeys } from '@/features/cash-common/keys';
import type { PaymentQrKind } from './types';

// Under the cash root so branch switches clear it (P28).
export const voucherPaymentQrKeys = {
  all: [...cashKeys.all, 'payment-qr'] as const,
  detail: (kind: PaymentQrKind, id: string) => [...voucherPaymentQrKeys.all, kind, id] as const,
};
