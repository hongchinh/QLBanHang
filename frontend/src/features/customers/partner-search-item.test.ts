import { describe, expect, it } from 'vitest';
import { toPartnerSearchItem } from './partner-search-item';
import type { Customer } from './types';

describe('toPartnerSearchItem', () => {
  it('keeps only the search fields', () => {
    const customer: Customer = {
      id: 'c1',
      code: 'KH1',
      name: 'A',
      taxCode: '1',
      companyAddress: 'x',
      defaultShippingAddress: 'y',
      contactPerson: 'z',
      phoneNumber: '0',
      email: 'a@b.com',
      note: 'n',
      status: 'Active',
      isCustomer: true,
      isSupplier: false,
      creditDays: 30,
      group: 'Company',
      createdAt: '2026-01-01T00:00:00Z',
    };

    expect(toPartnerSearchItem(customer)).toEqual({
      id: 'c1',
      code: 'KH1',
      name: 'A',
      taxCode: '1',
      companyAddress: 'x',
      defaultShippingAddress: 'y',
      contactPerson: 'z',
      phoneNumber: '0',
      status: 'Active',
      isCustomer: true,
      isSupplier: false,
      creditDays: 30,
    });
  });
});
