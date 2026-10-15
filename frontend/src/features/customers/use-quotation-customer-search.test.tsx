import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useQuotationCustomerSearch } from './use-quotation-customer-search';

const searchMock = vi.fn();
vi.mock('./hooks', () => ({
  useCustomersSearch: (...args: unknown[]) => searchMock(...args),
}));

const source = [{ id: '1', code: 'KH1', name: 'A', status: 'Active' }];

describe('useQuotationCustomerSearch', () => {
  beforeEach(() => {
    searchMock.mockReset();
    searchMock.mockReturnValue({ data: source, isFetching: false, isLoading: false, isError: false });
  });

  it('searches active customers, 20 at most', () => {
    renderHook(() => useQuotationCustomerSearch('kh'));

    expect(searchMock).toHaveBeenCalledWith('kh', { activeOnly: true, limit: 20 });
  });

  it('marks every result as a customer, not a supplier', () => {
    const { result } = renderHook(() => useQuotationCustomerSearch('kh'));

    expect(result.current.data).toEqual([{ ...source[0], isCustomer: true, isSupplier: false }]);
  });

  it('passes the query flags through', () => {
    searchMock.mockReturnValue({ data: undefined, isFetching: true, isLoading: true, isError: true });

    const { result } = renderHook(() => useQuotationCustomerSearch('kh'));

    expect(result.current).toEqual({ data: undefined, isFetching: true, isLoading: true, isError: true });
  });

  it('keeps the same data while the source data does not change', () => {
    const { result, rerender } = renderHook(() => useQuotationCustomerSearch('kh'));
    const first = result.current.data;

    rerender();

    expect(result.current.data).toBe(first);
  });
});
