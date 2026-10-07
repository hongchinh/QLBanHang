import { describe, expect, it } from 'vitest';
import { isCacheableApiPath } from './sw-routes';

describe('isCacheableApiPath', () => {
  it.each([
    '/api/lookups/units',
    '/api/lookups/product-groups',
    '/api/banks',
    '/api/settings/branding',
  ])('caches non-scoped reference data %s', (path) => {
    expect(isCacheableApiPath(path)).toBe(true);
  });

  it.each([
    // user- or permission-scoped
    '/api/auth/me',
    '/api/search/global',
    '/api/quotations',
    '/api/quotations/abc-123',
    '/api/dashboard/summary',
    '/api/notifications',
    '/api/notifications/unread-count',
    '/api/me/branches',
    '/api/me/quotation-settings',
    '/api/products/search',
    '/api/customers/search',
    '/api/settings/quotation',
    '/api/admin/users',
    // branch-scoped
    '/api/stock-vouchers',
    '/api/stock-vouchers/abc-123',
    '/api/inventory/opening-stock',
    '/api/inventory/settings',
    '/api/reports/stock-on-hand',
    '/api/warehouses',
    '/api/branches',
    '/api/suppliers/search',
    '/api/stock-reasons',
    '/api/payment-methods',
  ])('never caches scoped %s', (path) => {
    expect(isCacheableApiPath(path)).toBe(false);
  });

  it('returns false for non-API paths', () => {
    expect(isCacheableApiPath('/')).toBe(false);
    expect(isCacheableApiPath('/stock-in')).toBe(false);
    expect(isCacheableApiPath('/assets/index.js')).toBe(false);
  });
});
