export type PaymentQrKind = 'cash' | 'stock';

export interface VoucherPaymentQr {
  payload: string;
  amount: number;
  total: number;
  cashPaid: number;
  bankName: string;
  bankShortName: string;
  accountNumber: string;
  accountHolderName: string;
  content: string;
  fundCode: string;
  fundName: string;
  sourceStockVoucherCode?: string | null;
}
