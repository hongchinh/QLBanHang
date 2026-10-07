export interface PaymentMethod {
  id: string;
  code: string;
  name: string;
  // Stored now, used by Round 2 cash vouchers.
  isCash: boolean;
}

export interface CreatePaymentMethodRequest {
  code: string;
  name: string;
  isCash: boolean;
}

export interface UpdatePaymentMethodRequest {
  name: string;
  isCash: boolean;
}
