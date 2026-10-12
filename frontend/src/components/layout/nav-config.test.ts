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

// Round 2A: P5 ACCOUNTANT defaults, cash and debt part.
const ACCOUNTANT_CASH_PERMS: Permission[] = [
  'receipts.view', 'receipts.create',
  'payments.view', 'payments.create',
  'fund_transfers.view', 'fund_transfers.create',
  'debt.matching',
  'debt_offset.view', 'debt_offset.create',
  'debt.opening_balance',
  'cash.opening_balance', 'cash.catalogs.manage', 'cash.settings',
  'credit_limit.manage',
  'debt.partner_summary',
  'reports.debt', 'reports.cash',
];

describe('cash and debt navigation', () => {
  const CASH_GROUP = 'Tiền & Công nợ';

  it('cash group shows items by permission', () => {
    expect(paths(['receipts.view'], CASH_GROUP)).toEqual(['/receipts']);
    expect(groupsFor(SALES_PERMS).find((g) => g.label === CASH_GROUP)).toBeUndefined();
    expect(groupsFor(WAREHOUSE_PERMS).find((g) => g.label === CASH_GROUP)).toBeUndefined();

    const groups = groupsFor([...WAREHOUSE_PERMS, ...ACCOUNTANT_CASH_PERMS]);
    const labels = groups.map((g) => g.label);
    expect(labels.indexOf(CASH_GROUP)).toBe(labels.indexOf('Kho') + 1);
    expect(paths(ACCOUNTANT_CASH_PERMS, CASH_GROUP)).toEqual([
      '/receipts',
      '/payments',
      '/fund-transfers',
      '/debt/matching',
      '/debt/offsets',
      '/debt/opening',
      '/reports/debt-balance',
      '/reports/debt-ledger',
      '/reports/debt-documents',
      '/reports/debt-aging',
      '/reports/debt-overdue',
      '/reports/debt-by-salesperson',
      '/reports/cash-book',
    ]);
    expect(groups.find((g) => g.label === CASH_GROUP)?.items.map((i) => i.label)).toEqual([
      'Phiếu thu',
      'Phiếu chi',
      'Chuyển quỹ',
      'Đối trừ chứng từ',
      'Bù trừ công nợ',
      'Nợ đầu kỳ',
      'Số dư công nợ',
      'Sổ chi tiết công nợ',
      'Công nợ theo chứng từ',
      'Tuổi nợ',
      'Nợ quá hạn',
      'Công nợ theo nhân viên',
      'Sổ quỹ',
    ]);
  });

  it('chức năng group lists cash catalogs', () => {
    expect(paths(ACCOUNTANT_CASH_PERMS, 'Chức năng')).toEqual(
      expect.arrayContaining(['/cash-reasons', '/funds', '/credit-limits']),
    );
    // The QR page has no permission, so it is always listed.
    const catalogs = (perms: Permission[]) =>
      paths(perms, 'Chức năng').filter((p) => p !== '/qr-thanh-toan');
    expect(catalogs(['cash.catalogs.manage'])).toEqual(['/cash-reasons', '/funds']);
    expect(catalogs(['cash.opening_balance'])).toEqual(['/funds']);
    expect(catalogs(['credit_limit.manage'])).toEqual(['/credit-limits']);
    for (const path of ['/cash-reasons', '/funds', '/credit-limits']) {
      expect(paths(SALES_PERMS, 'Chức năng')).not.toContain(path);
      expect(paths(WAREHOUSE_PERMS, 'Chức năng')).not.toContain(path);
    }
  });

  it('only cash.settings shows Cấu hình hệ thống', () => {
    expect(paths(['cash.settings'], 'Setting')).toContain('/settings');
    expect(paths(ACCOUNTANT_CASH_PERMS, 'Setting')).toContain('/settings');
  });
});
