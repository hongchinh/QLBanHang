import { describe, expect, it } from 'vitest';
import { PERMISSIONS } from './permissions';
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

  it('/stock-in và /stock-in/:id cần stock_in.view, /stock-in/new cần stock_in.create', () => {
    expectRule('/stock-in', 'stock_in.view');
    expectRule('/stock-in/abc-123', 'stock_in.view');
    expectRule('/stock-in/new', 'stock_in.create');
    expect(canAccessRoute('/stock-in/new', ['stock_in.view'], [])).toBe(false);
    expect(canAccessRoute('/stock-in', ['stock_out.view'], [])).toBe(false);
  });

  it('/stock-out và /stock-out/:id cần stock_out.view, /stock-out/new cần stock_out.create', () => {
    expectRule('/stock-out', 'stock_out.view');
    expectRule('/stock-out/abc-123', 'stock_out.view');
    expectRule('/stock-out/new', 'stock_out.create');
    expect(canAccessRoute('/stock-out/new', ['stock_out.view'], [])).toBe(false);
    expect(canAccessRoute('/stock-out', ['stock_in.view'], [])).toBe(false);
  });

  it('/inventory/opening-stock cần inventory.opening_stock', () => {
    expectRule('/inventory/opening-stock', 'inventory.opening_stock');
  });

  it('/inventory/stock-on-hand và /inventory/stock-card cần reports.inventory', () => {
    expectRule('/inventory/stock-on-hand', 'reports.inventory');
    expectRule('/inventory/stock-card', 'reports.inventory');
  });

  it('/suppliers cần suppliers.view; /suppliers/new cần create, /suppliers/:id cần update', () => {
    expectRule('/suppliers', 'suppliers.view');
    expectRule('/suppliers/new', 'suppliers.create');
    expectRule('/suppliers/abc-123', 'suppliers.update');
    // WAREHOUSE has only suppliers.view (D23).
    expect(canAccessRoute('/suppliers/abc-123', ['suppliers.view'], ['WAREHOUSE'])).toBe(false);
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

  it('/settings/inventory cần inventory.settings; /settings/numbering cần inventory.settings hoặc cash.settings', () => {
    expectRule('/settings/inventory', 'inventory.settings');
    expect(canAccessRoute('/settings/inventory', ['cash.settings'], [])).toBe(false);
    expectRule('/settings/numbering', 'inventory.settings');
    expectRule('/settings/numbering', 'cash.settings');
  });

  it('/settings/recalc-cost cần inventory.recalc_cost', () => {
    expectRule('/settings/recalc-cost', 'inventory.recalc_cost');
  });
});

describe('rules mirror App.tsx guards', () => {
  it('customer, product and quotation forms need create / update', () => {
    expect(canAccessRoute('/customers', ['customers.view'], [])).toBe(true);
    expect(canAccessRoute('/customers/new', ['customers.view'], [])).toBe(false);
    expect(canAccessRoute('/customers/new', ['customers.create'], [])).toBe(true);
    expect(canAccessRoute('/customers/abc', ['customers.view'], [])).toBe(false);
    expect(canAccessRoute('/customers/abc', ['customers.update'], [])).toBe(true);
    expect(canAccessRoute('/products/abc', ['products.view'], [])).toBe(false);
    expect(canAccessRoute('/products/new', ['products.create'], [])).toBe(true);
    expect(canAccessRoute('/quotations/abc', ['quotations.view'], [])).toBe(false);
    expect(canAccessRoute('/quotations/new', ['quotations.view'], [])).toBe(false);
  });

  it('/product-groups, /settings/quotation and /reports/sales-revenue/:id are guarded', () => {
    expect(canAccessRoute('/product-groups', [], [])).toBe(false);
    expect(canAccessRoute('/product-groups', ['products.view'], [])).toBe(true);
    expect(canAccessRoute('/settings/quotation', [], [])).toBe(false);
    expect(canAccessRoute('/settings/quotation', ['system.manage_settings'], [])).toBe(true);
    expect(canAccessRoute('/reports/sales-revenue/u1', [], [])).toBe(false);
    expect(canAccessRoute('/reports/sales-revenue/u1', ['reports.revenue'], [])).toBe(true);
  });
});

describe('PERMISSIONS', () => {
  it('PERMISSIONS includes every round 2A code', () => {
    const codes: readonly string[] = PERMISSIONS;
    for (const code of [
      'receipts.view',
      'debt.partner_summary',
      'reports.cash',
      'credit_limit.manage',
    ]) {
      expect(codes).toContain(code);
    }
  });
});

describe('cash and debt routes', () => {
  function expectRule(path: string, permission: string) {
    expect(canAccessRoute(path, [], [])).toBe(false);
    expect(canAccessRoute(path, SALES_PERMS, ['SALES'])).toBe(false);
    expect(canAccessRoute(path, [permission], [])).toBe(true);
  }

  it('cash routes require their permissions', () => {
    expectRule('/receipts', 'receipts.view');
    expectRule('/receipts/new', 'receipts.create');
    expect(canAccessRoute('/receipts/new', ['receipts.view'], [])).toBe(false);
    expectRule('/receipts/abc-123', 'receipts.view');
    expectRule('/payments', 'payments.view');
    expectRule('/payments/new', 'payments.create');
    expectRule('/payments/abc-123', 'payments.view');
    expectRule('/fund-transfers', 'fund_transfers.view');
    expectRule('/fund-transfers/new', 'fund_transfers.create');
    expectRule('/fund-transfers/abc-123', 'fund_transfers.view');
    expectRule('/debt/offsets', 'debt_offset.view');
    expectRule('/debt/offsets/new', 'debt_offset.create');
    expectRule('/debt/offsets/abc-123', 'debt_offset.view');
    expectRule('/debt/matching', 'debt.matching');
    expect(canAccessRoute('/debt/matching', ['debt_offset.view'], [])).toBe(false);
    expectRule('/debt/opening', 'debt.opening_balance');
    expectRule('/reports/debt-balance', 'reports.debt');
    expectRule('/reports/debt-ledger', 'reports.debt');
    expectRule('/reports/debt-documents', 'reports.debt');
    expectRule('/reports/debt-aging', 'reports.debt');
    expectRule('/reports/debt-overdue', 'reports.debt');
    expectRule('/reports/debt-by-salesperson', 'reports.debt');
    expectRule('/reports/debt-reconciliation', 'reports.debt');
    expectRule('/reports/cash-book', 'reports.cash');
    expect(canAccessRoute('/reports/cash-book', ['reports.debt'], [])).toBe(false);
    expectRule('/cash-reasons', 'cash.catalogs.manage');
    expectRule('/funds', 'cash.catalogs.manage');
    expectRule('/funds', 'cash.opening_balance');
    expect(canAccessRoute('/funds', ['credit_limit.manage'], [])).toBe(false);
    expectRule('/credit-limits', 'credit_limit.manage');
    expectRule('/settings/cash', 'cash.settings');
    expect(canAccessRoute('/settings/cash', ['inventory.settings'], [])).toBe(false);
  });

  it('/dashboard/executive needs dashboard.executive', () => {
    expectRule('/dashboard/executive', 'dashboard.executive');
    // The quotation dashboard permission does not open it.
    expect(canAccessRoute('/dashboard/executive', ['quotations.view_all'], ['ADMIN'])).toBe(false);
    const codes: readonly string[] = PERMISSIONS;
    expect(codes).toContain('dashboard.executive');
  });
});
