import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { Customer } from './types';

// A customer / supplier record as the partner field's search item (what GET .../partners returns).
export function toPartnerSearchItem(c: Customer): PartnerSearchItem {
  return {
    id: c.id,
    code: c.code,
    name: c.name,
    taxCode: c.taxCode,
    companyAddress: c.companyAddress,
    defaultShippingAddress: c.defaultShippingAddress,
    contactPerson: c.contactPerson,
    phoneNumber: c.phoneNumber,
    status: c.status,
    isCustomer: c.isCustomer,
    isSupplier: c.isSupplier,
    creditDays: c.creditDays,
  };
}
