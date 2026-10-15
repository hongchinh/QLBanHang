import { useMemo } from 'react';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import { useCustomersSearch } from './hooks';

// The customer search in the item shape of the shared partner field (a quotation takes customers only).
export function useQuotationCustomerSearch(keyword: string) {
  const query = useCustomersSearch(keyword, { activeOnly: true, limit: 20 });
  const data = useMemo(
    () => query.data?.map((c): PartnerSearchItem => ({ ...c, isCustomer: true, isSupplier: false })),
    [query.data],
  );
  return { data, isFetching: query.isFetching, isLoading: query.isLoading, isError: query.isError };
}
