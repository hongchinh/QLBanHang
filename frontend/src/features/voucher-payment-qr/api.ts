import { apiGet } from '@/lib/api-client';
import type { PaymentQrKind, VoucherPaymentQr } from './types';

export const voucherPaymentQrApi = {
  get: (kind: PaymentQrKind, id: string) =>
    apiGet<VoucherPaymentQr>(`/${kind === 'cash' ? 'cash-vouchers' : 'stock-vouchers'}/${id}/payment-qr`),
};
