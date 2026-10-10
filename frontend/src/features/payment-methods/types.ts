import type { FundKind } from '@/features/cash-common/types';

export interface PaymentMethod {
  id: string;
  code: string;
  name: string;
  // None: no money moves; Cash / Bank: the kind of fund the paid amount goes to (P9).
  fundKind: FundKind;
}

export interface CreatePaymentMethodRequest {
  code: string;
  name: string;
  fundKind: FundKind;
}

export interface UpdatePaymentMethodRequest {
  name: string;
  fundKind: FundKind;
}
