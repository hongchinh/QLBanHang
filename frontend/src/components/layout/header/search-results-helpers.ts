import type { CustomerSearchItem, GlobalSearchResult } from '@/features/search/api';

export type PartnerKind = 'customer' | 'supplier';

export type SearchTarget =
  | { kind: PartnerKind; item: CustomerSearchItem }
  | { kind: 'quotation'; id: string };

export function flattenResultIndex(
  data: GlobalSearchResult | undefined,
  activeIndex: number,
): SearchTarget | null {
  if (!data) return null;
  const cs = data.customers;
  const ss = data.suppliers ?? [];
  const qs = data.quotations;
  if (activeIndex < 0) return null;
  if (activeIndex < cs.length) return { kind: 'customer', item: cs[activeIndex] };
  const sIdx = activeIndex - cs.length;
  if (sIdx < ss.length) return { kind: 'supplier', item: ss[sIdx] };
  const qIdx = sIdx - ss.length;
  if (qIdx < qs.length) return { kind: 'quotation', id: qs[qIdx].id };
  return null;
}

export function totalResultCount(data: GlobalSearchResult | undefined): number {
  if (!data) return 0;
  return data.customers.length + (data.suppliers?.length ?? 0) + data.quotations.length;
}

const PARTNER_ROUTES: Record<PartnerKind, { base: string; updatePermission: string }> = {
  customer: { base: '/customers', updatePermission: 'customers.update' },
  supplier: { base: '/suppliers', updatePermission: 'suppliers.update' },
};

// The partner form route is guarded by `*.update`; without it (e.g. WAREHOUSE has only
// suppliers.view) a hit opens the list filtered by its code instead of landing on /403.
export function partnerResultPath(
  kind: PartnerKind,
  item: Pick<CustomerSearchItem, 'id' | 'code'>,
  hasPermission: (permission: string) => boolean,
): string {
  const { base, updatePermission } = PARTNER_ROUTES[kind];
  return hasPermission(updatePermission) ? `${base}/${item.id}` : `${base}?q=${encodeURIComponent(item.code)}`;
}
