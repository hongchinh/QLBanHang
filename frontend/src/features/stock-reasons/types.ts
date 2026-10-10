import type { DebtEffect } from '@/features/cash-common/types';

export type StockDirection = 'In' | 'Out';
export type PartnerType = 'None' | 'Customer' | 'Supplier' | 'Any';

export const STOCK_DIRECTION_LABELS: Record<StockDirection, string> = {
  In: 'Nhập',
  Out: 'Xuất',
};

export const PARTNER_TYPE_LABELS: Record<PartnerType, string> = {
  Customer: 'Khách hàng',
  Supplier: 'Nhà cung cấp',
  Any: 'Bất kỳ',
  None: 'Không có',
};

export interface StockReason {
  id: string;
  code: string;
  name: string;
  direction: StockDirection;
  partnerType: PartnerType;
  debtEffect: DebtEffect;
  isSystem: boolean;
}

// DebtEffectRules (P19): the debt effects a stock reason of each direction may carry.
export const STOCK_DEBT_EFFECTS: Record<StockDirection, DebtEffect[]> = {
  In: ['None', 'PayableIncrease', 'ReceivableDecrease'],
  Out: ['None', 'ReceivableIncrease', 'PayableDecrease'],
};

export interface StockReasonListParams {
  direction?: StockDirection;
}

export interface CreateStockReasonRequest {
  code: string;
  name: string;
  direction: StockDirection;
  partnerType: PartnerType;
  debtEffect: DebtEffect;
}

export interface UpdateStockReasonRequest {
  name: string;
  direction: StockDirection;
  partnerType: PartnerType;
  debtEffect: DebtEffect;
}
