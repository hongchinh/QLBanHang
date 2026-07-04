import { describe, expect, it } from 'vitest';
import {
  QUOTATION_LIST_STORAGE_KEY,
  QUOTATION_LIST_COLUMN_SIZING_DEFAULTS,
} from './column-sizing-defaults';

describe('quotation list column sizing defaults', () => {
  it('exposes a stable storage key', () => {
    expect(QUOTATION_LIST_STORAGE_KEY).toBe('quotations-list');
  });

  it('defines a positive default width for every known column id', () => {
    const expectedIds = [
      'code',
      'quotationDate',
      'revenueDate',
      'deliveryDate',
      'customerName',
      'subtotal',
      'discount',
      'freight',
      'taxRate',
      'taxAmount',
      'total',
      'advancePayment',
      'totalCost',
      'grossProfit',
      'status',
      'owner',
      'createdByName',
      'contactPhone',
      'actions',
    ];
    for (const id of expectedIds) {
      expect(QUOTATION_LIST_COLUMN_SIZING_DEFAULTS[id]).toBeGreaterThan(0);
    }
  });

  it('gives the actions column a fixed compact width', () => {
    expect(QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions).toBe(90);
  });
});
