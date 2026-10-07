// Imported by sw.ts; kept free of DOM/service-worker APIs so it is testable.
//
// The service worker's NetworkFirst cache keys on the URL only. X-Branch-Id and
// Authorization are not part of the key, so branch- or user-scoped responses
// (including cost values) must never be cached: on a slow or offline network
// they would be served to another branch or user.
const NEVER_CACHE_PREFIXES = [
  '/api/stock-vouchers',
  '/api/inventory',
  '/api/reports',
  '/api/warehouses',
  '/api/branches',
  '/api/me/',
  '/api/suppliers',
  '/api/stock-reasons',
  '/api/payment-methods',
];

export function isCacheableApiPath(pathname: string): boolean {
  if (!pathname.startsWith('/api/')) return false;
  return !NEVER_CACHE_PREFIXES.some((prefix) => pathname.startsWith(prefix));
}
