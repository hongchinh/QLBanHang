import { describe, expect, it } from 'vitest';
import type { Permission, Role } from '@/lib/permissions';
import { visibleNavGroups } from './nav-config';

// D23 WAREHOUSE defaults.
const WAREHOUSE_PERMS: Permission[] = [
  'stock_in.view', 'stock_in.create', 'stock_in.edit', 'stock_in.delete', 'stock_in.cancel',
  'stock_out.view', 'stock_out.create', 'stock_out.edit', 'stock_out.delete', 'stock_out.cancel',
  'inventory.opening_stock',
  'reports.inventory',
  'suppliers.view',
];

const SALES_PERMS: Permission[] = [
  'customers.view', 'customers.create', 'customers.update',
  'products.view',
  'quotations.view', 'quotations.create', 'quotations.update',
  'quotations.print', 'quotations.transfer_own',
];

function groupsFor(perms: Permission[], roles: Role[] = []) {
  return visibleNavGroups(
    (p) => perms.includes(p),
    (r) => roles.includes(r),
  );
}

function paths(perms: Permission[], groupLabel: string): string[] {
  return groupsFor(perms).find((g) => g.label === groupLabel)?.items.map((i) => i.to) ?? [];
}

describe('visibleNavGroups', () => {
  it('warehouse user sees Kho group with vouchers, opening stock and reports', () => {
    const groups = groupsFor(WAREHOUSE_PERMS, ['WAREHOUSE']);

    expect(groups.map((g) => g.label).slice(0, 2)).toEqual(['Chức năng', 'Kho']);
    expect(groups.find((g) => g.label === 'Kho')?.items.map((i) => i.label)).toEqual([
      'Phiếu nhập kho',
      'Phiếu xuất kho',
      'Tồn đầu kỳ',
      'Tồn kho',
      'Thẻ kho',
    ]);
    expect(paths(WAREHOUSE_PERMS, 'Kho')).toEqual([
      '/stock-in',
      '/stock-out',
      '/inventory/opening-stock',
      '/inventory/stock-on-hand',
      '/inventory/stock-card',
    ]);
    expect(paths(WAREHOUSE_PERMS, 'Chức năng')).toContain('/suppliers');
    expect(paths(WAREHOUSE_PERMS, 'Chức năng')).not.toContain('/warehouses');
  });

  it('sales user sees no Kho group and no inventory catalogs', () => {
    const groups = groupsFor(SALES_PERMS, ['SALES']);

    expect(groups.find((g) => g.label === 'Kho')).toBeUndefined();
    const functionPaths = paths(SALES_PERMS, 'Chức năng');
    expect(functionPaths).toContain('/quotations');
    for (const path of ['/suppliers', '/warehouses', '/stock-reasons', '/payment-methods']) {
      expect(functionPaths).not.toContain(path);
    }
    expect(paths(SALES_PERMS, 'Setting')).not.toContain('/settings');
  });

  it('catalog manager sees warehouse, reason and payment method entries', () => {
    const perms: Permission[] = ['inventory.catalogs.manage'];

    expect(paths(perms, 'Chức năng')).toEqual(
      expect.arrayContaining(['/warehouses', '/stock-reasons', '/payment-methods']),
    );
    expect(paths(perms, 'Chức năng')).not.toContain('/suppliers');
    expect(groupsFor(perms).find((g) => g.label === 'Kho')).toBeUndefined();
  });

  it('settings entry is visible with inventory.settings only', () => {
    expect(paths(['inventory.settings'], 'Setting')).toContain('/settings');
  });
});
