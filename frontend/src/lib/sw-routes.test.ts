import { describe, expect, it } from 'vitest';
import { isCacheableApiPath } from './sw-routes';

describe('isCacheableApiPath', () => {
  it.each([
    '/api/stock-vouchers',
    '/api/stock-vouchers/abc-123',
    '/api/inventory/opening-stock',
    '/api/inventory/settings',
    '/api/reports/stock-on-hand',
    '/api/warehouses',
    '/api/branches',
    '/api/me/branches',
    '/api/me/quotation-settings',
    '/api/suppliers/search',
    '/api/stock-reasons',
    '/api/payment-methods',
  ])('never caches branch- or user-scoped %s', (path) => {
    expect(isCacheableApiPath(path)).toBe(false);
  });

  it('caches other API paths', () => {
    expect(isCacheableApiPath('/api/products/search')).toBe(true);
    expect(isCacheableApiPath('/api/lookups/units')).toBe(true);
  });

  it('returns false for non-API paths', () => {
    expect(isCacheableApiPath('/')).toBe(false);
    expect(isCacheableApiPath('/stock-in')).toBe(false);
    expect(isCacheableApiPath('/assets/index.js')).toBe(false);
  });
});
