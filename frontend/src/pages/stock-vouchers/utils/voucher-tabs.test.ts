import { describe, expect, it } from 'vitest';
import { errorTabs, firstErrorTab, tabOfField } from './voucher-tabs';

describe('voucher tabs', () => {
  it('maps form and server keys to their tab; main-area fields map to null', () => {
    expect(tabOfField('lines')).toBe('lines');
    expect(tabOfField('lines[3].width')).toBe('lines');
    expect(tabOfField('lines.3.width')).toBe('lines');
    expect(tabOfField('note')).toBe('other');
    expect(tabOfField('partnerTaxCode')).toBe('other');
    expect(tabOfField('handlerName')).toBe('other');
    expect(tabOfField('paidAmount')).toBe('payment');
    expect(tabOfField('fundId')).toBe('payment');
    expect(tabOfField('dueDate')).toBe('payment');
    expect(tabOfField('freight')).toBe('payment');
    expect(tabOfField('orderDiscount')).toBe('payment');
    expect(tabOfField('reasonId')).toBeNull();
    expect(tabOfField('partnerAddress')).toBeNull();
  });

  it('collects the tabs with errors and picks the first in tab order', () => {
    expect([...errorTabs(['fundId', 'note', 'reasonId'])].sort()).toEqual(['other', 'payment']);
    expect(firstErrorTab(['fundId', 'note'])).toBe('other');
    expect(firstErrorTab(['paidAmount', 'lines[0].quantity'])).toBe('lines');
    expect(firstErrorTab(['reasonId'])).toBeNull();
  });
});
