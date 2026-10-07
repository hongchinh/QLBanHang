import { describe, expect, it } from 'vitest';
import { canAccessRoute } from './route-permissions';

const SALES_PERMS = [
  'customers.view', 'customers.create', 'customers.update',
  'products.view',
  'quotations.view', 'quotations.create', 'quotations.update',
  'quotations.print', 'quotations.transfer_own',
];

describe('canAccessRoute', () => {
  it('allows route không có rule cho mọi user', () => {
    expect(canAccessRoute('/', [], [])).toBe(true);
    expect(canAccessRoute('/settings', [], [])).toBe(true);
    expect(canAccessRoute('/settings/my-quotation-settings', [], [])).toBe(true);
  });

  it('chặn /admin/dashboard với sale (thiếu view_all)', () => {
    expect(canAccessRoute('/admin/dashboard', SALES_PERMS, ['SALES'])).toBe(false);
  });

  it('cho phép /admin/dashboard nếu có view_all', () => {
    expect(canAccessRoute('/admin/dashboard', ['quotations.view_all'], ['ADMIN'])).toBe(true);
  });

  it('cho phép /quotations và /quotations/:id với sale', () => {
    expect(canAccessRoute('/quotations', SALES_PERMS, ['SALES'])).toBe(true);
    expect(canAccessRoute('/quotations/abc-123', SALES_PERMS, ['SALES'])).toBe(true);
    expect(canAccessRoute('/quotations/new', SALES_PERMS, ['SALES'])).toBe(true);
  });

  it('chặn /reports/sales-performance nếu thiếu view_all (kể cả có reports.revenue)', () => {
    expect(canAccessRoute('/reports/sales-performance', ['reports.revenue'], [])).toBe(false);
  });

  it('chặn /reports/revenue nếu thiếu reports.revenue, cho phép nếu có', () => {
    expect(canAccessRoute('/reports/revenue', [], [])).toBe(false);
    expect(canAccessRoute('/reports/vehicle-revenue', [], [])).toBe(false);
    expect(canAccessRoute('/reports/revenue', ['reports.revenue'], [])).toBe(true);
    expect(canAccessRoute('/reports/sales-revenue', ['reports.revenue'], [])).toBe(true);
    expect(canAccessRoute('/reports/vehicle-revenue', ['reports.revenue'], [])).toBe(true);
  });

  it('chặn /admin/users và /admin/users/:id nếu thiếu user_settings.manage', () => {
    expect(canAccessRoute('/admin/users', SALES_PERMS, ['SALES'])).toBe(false);
    expect(canAccessRoute('/admin/users/abc', SALES_PERMS, ['SALES'])).toBe(false);
  });

  it('chặn /admin/roles với sale (thiếu roles.view)', () => {
    expect(canAccessRoute('/admin/roles', SALES_PERMS, ['SALES'])).toBe(false);
  });

  it('cho phép /admin/roles nếu có roles.view', () => {
    expect(canAccessRoute('/admin/roles', ['roles.view'], [])).toBe(true);
  });

  it('phân biệt /admin/users/:id/transfer-quotations cần transfer_any', () => {
    // user_settings.manage không đủ — pattern cụ thể hơn match trước
    expect(canAccessRoute(
      '/admin/users/abc/transfer-quotations',
      ['user_settings.manage'],
      [],
    )).toBe(false);
    expect(canAccessRoute(
      '/admin/users/abc/transfer-quotations',
      ['quotations.transfer_any'],
      [],
    )).toBe(true);
  });
});

describe('inventory routes', () => {
  function expectRule(path: string, permission: string) {
    expect(canAccessRoute(path, [], [])).toBe(false);
    expect(canAccessRoute(path, SALES_PERMS, ['SALES'])).toBe(false);
    expect(canAccessRoute(path, [permission], [])).toBe(true);
  }

  it('/stock-in và /stock-in/:id cần stock_in.view', () => {
    expectRule('/stock-in', 'stock_in.view');
    expectRule('/stock-in/abc-123', 'stock_in.view');
    expectRule('/stock-in/new', 'stock_in.view');
    expect(canAccessRoute('/stock-in', ['stock_out.view'], [])).toBe(false);
  });

  it('/stock-out và /stock-out/:id cần stock_out.view', () => {
    expectRule('/stock-out', 'stock_out.view');
    expectRule('/stock-out/abc-123', 'stock_out.view');
    expect(canAccessRoute('/stock-out', ['stock_in.view'], [])).toBe(false);
  });

  it('/inventory/opening-stock cần inventory.opening_stock', () => {
    expectRule('/inventory/opening-stock', 'inventory.opening_stock');
  });

  it('/inventory/stock-on-hand và /inventory/stock-card cần reports.inventory', () => {
    expectRule('/inventory/stock-on-hand', 'reports.inventory');
    expectRule('/inventory/stock-card', 'reports.inventory');
  });

  it('/suppliers và /suppliers/:id cần suppliers.view', () => {
    expectRule('/suppliers', 'suppliers.view');
    expectRule('/suppliers/abc-123', 'suppliers.view');
  });

  it('/warehouses, /stock-reasons, /payment-methods cần inventory.catalogs.manage', () => {
    expectRule('/warehouses', 'inventory.catalogs.manage');
    expectRule('/stock-reasons', 'inventory.catalogs.manage');
    expectRule('/payment-methods', 'inventory.catalogs.manage');
  });

  it('/settings/branches cần branches.manage', () => {
    expectRule('/settings/branches', 'branches.manage');
  });

  it('/settings/period-lock cần period_lock.manage', () => {
    expectRule('/settings/period-lock', 'period_lock.manage');
  });

  it('/settings/inventory và /settings/numbering cần inventory.settings', () => {
    expectRule('/settings/inventory', 'inventory.settings');
    expectRule('/settings/numbering', 'inventory.settings');
  });

  it('/settings/recalc-cost cần inventory.recalc_cost', () => {
    expectRule('/settings/recalc-cost', 'inventory.recalc_cost');
  });
});
