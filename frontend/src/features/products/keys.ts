import type { ProductListParams } from './types';

export const productKeys = {
  all: ['products'] as const,
  lists: () => [...productKeys.all, 'list'] as const,
  list: (params: ProductListParams) => [...productKeys.lists(), params] as const,
  details: () => [...productKeys.all, 'detail'] as const,
  detail: (id: string) => [...productKeys.details(), id] as const,
  searches: () => [...productKeys.all, 'search'] as const,
  search: (q: string) => [...productKeys.searches(), q] as const,
};

export const lookupKeys = {
  all: ['lookups'] as const,
  productGroups: () => [...lookupKeys.all, 'product-groups'] as const,
  units: () => [...lookupKeys.all, 'units'] as const,
};
