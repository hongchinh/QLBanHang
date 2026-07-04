export const QUOTATION_LIST_STORAGE_KEY = 'quotations-list';

export const QUOTATION_LIST_COLUMN_SIZING_DEFAULTS: Record<string, number> = {
  code: 100,
  quotationDate: 100,
  revenueDate: 100,
  deliveryDate: 100,
  customerName: 160,
  subtotal: 120,
  discount: 110,
  freight: 110,
  taxRate: 80,
  taxAmount: 120,
  total: 120,
  advancePayment: 110,
  totalCost: 120,
  grossProfit: 120,
  status: 120,
  owner: 140,
  createdByName: 120,
  contactPhone: 100,
  actions: 90,
};

export const COLUMN_MIN_SIZE = 60;
export const ACTIONS_COLUMN_SIZE = QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions;
