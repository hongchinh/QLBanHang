import { apiGet } from '@/lib/api-client';

export interface CustomerSearchItem {
  id: string;
  code: string;
  name: string;
  taxCode?: string | null;
  companyAddress?: string | null;
  defaultShippingAddress?: string | null;
  contactPerson?: string | null;
  phoneNumber?: string | null;
  status: number;
}

export interface QuotationSearchItem {
  id: string;
  code: string;
  customerName: string;
  total: number;
  status: string;
  createdAt: string;
}

export interface GlobalSearchResult {
  customers: CustomerSearchItem[];
  // Supplier-role partners; filled only with suppliers.view (D38).
  suppliers?: CustomerSearchItem[];
  quotations: QuotationSearchItem[];
}

export const searchApi = {
  global: (q: string) => apiGet<GlobalSearchResult>('/search/global', { q }),
};
