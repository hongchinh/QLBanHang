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

const all = () => visibleNavGroups(() => true, () => true);

function paths(perms: Permission[], groupId: string): string[] {
  return groupsFor(perms).find((g) => g.id === groupId)?.items.map((i) => i.to) ?? [];
}

const DEBT_REPORTS = [
  '/reports/debt-balance', '/reports/debt-ledger', '/reports/debt-documents', '/reports/debt-aging',
  '/reports/debt-overdue', '/reports/debt-by-salesperson', '/reports/debt-reconciliation',
];

describe('visibleNavGroups', () => {
  it('admin sees seven task groups in order', () => {
    expect(all().map((g) => g.id)).toEqual(['sales', 'inventory', 'cash', 'debt', 'reports', 'catalogs', 'system']);
    expect(all().map((g) => g.label)).toEqual(['Bán hàng', 'Kho', 'Thu chi', 'Công nợ', 'Báo cáo', 'Danh mục', 'Hệ thống']);
  });

  it('only Danh mục and Hệ thống start collapsed', () => {
    expect(all().filter((g) => !g.defaultOpen).map((g) => g.id)).toEqual(['catalogs', 'system']);
  });

  it('each group carries its color class', () => {
    for (const g of all()) expect(g.color).toBe(`bg-nav-${g.id}`);
  });

  it('sales user', () => {
    expect(groupsFor(SALES_PERMS, ['SALES']).map((g) => g.id)).toEqual(['sales', 'catalogs', 'system']);
    expect(paths(SALES_PERMS, 'sales')).toEqual(['/quotations', '/customers', '/qr-thanh-toan']);
    expect(paths(SALES_PERMS, 'catalogs')).toEqual(['/products', '/product-groups']);
    expect(paths(SALES_PERMS, 'system')).toEqual(['/settings/my-quotation-settings']);
  });

  it('warehouse user', () => {
    const groups = groupsFor(WAREHOUSE_PERMS, ['WAREHOUSE']);
    expect(groups.map((g) => g.id)).toEqual(['sales', 'inventory', 'reports', 'catalogs', 'system']);
    expect(paths(WAREHOUSE_PERMS, 'inventory')).toEqual(['/stock-in', '/stock-out', '/inventory/opening-stock']);
    expect(groups.find((g) => g.id === 'reports')?.items.map((i) => [i.section, i.to])).toEqual([
      ['Kho', '/inventory/stock-on-hand'],
      ['Kho', '/inventory/stock-card'],
    ]);
    expect(paths(WAREHOUSE_PERMS, 'catalogs')).toEqual(['/suppliers']);
  });

  it('accountant sees cash, debt, report and catalog items', () => {
    expect(paths(ACCOUNTANT_CASH_PERMS, 'cash')).toEqual(['/receipts', '/payments', '/fund-transfers']);
    expect(paths(ACCOUNTANT_CASH_PERMS, 'debt')).toEqual(['/debt/matching', '/debt/offsets', '/debt/opening', '/credit-limits']);
    expect(paths(ACCOUNTANT_CASH_PERMS, 'reports')).toEqual(['/reports/cash-book', ...DEBT_REPORTS]);
    expect(paths(ACCOUNTANT_CASH_PERMS, 'catalogs')).toEqual(['/cash-reasons', '/funds']);
    expect(paths(ACCOUNTANT_CASH_PERMS, 'system')).toContain('/settings');
    expect(groupsFor(SALES_PERMS).find((g) => g.id === 'cash')).toBeUndefined();
    expect(groupsFor(WAREHOUSE_PERMS).find((g) => g.id === 'debt')).toBeUndefined();
  });

  it('report sections for admin', () => {
    const reports = all().find((g) => g.id === 'reports')!;
    expect(reports.items.map((i) => [i.section, i.label])).toEqual([
      ['Doanh thu', 'Doanh thu'], ['Doanh thu', 'Doanh thu sale'], ['Doanh thu', 'Doanh thu xe'],
      ['Doanh thu', 'Hiệu suất sale'], ['Kho', 'Tồn kho'], ['Kho', 'Thẻ kho'], ['Quỹ', 'Sổ quỹ'],
      ['Công nợ', 'Số dư công nợ'], ['Công nợ', 'Sổ chi tiết công nợ'], ['Công nợ', 'Công nợ theo chứng từ'],
      ['Công nợ', 'Tuổi nợ'], ['Công nợ', 'Nợ quá hạn'], ['Công nợ', 'Công nợ theo nhân viên'],
      ['Công nợ', 'Biên bản đối chiếu'],
    ]);
  });

  it('catalog items follow their permissions', () => {
    expect(paths(['inventory.catalogs.manage'], 'catalogs')).toEqual(['/warehouses', '/stock-reasons', '/payment-methods']);
    expect(paths(['cash.catalogs.manage'], 'catalogs')).toEqual(['/cash-reasons', '/funds']);
    expect(paths(['cash.opening_balance'], 'catalogs')).toEqual(['/funds']);
    expect(paths(['credit_limit.manage'], 'debt')).toEqual(['/credit-limits']);
    expect(all().find((g) => g.id === 'catalogs')?.items.find((i) => i.to === '/warehouses')?.label).toBe('Kho hàng');
  });

  it('Cấu hình hệ thống shows with inventory.settings or cash.settings only', () => {
    expect(paths(['inventory.settings'], 'system')).toContain('/settings');
    expect(paths(['cash.settings'], 'system')).toContain('/settings');
    expect(paths(SALES_PERMS, 'system')).not.toContain('/settings');
  });

  it('every item icon is unique and differs from every group icon', () => {
    const groups = all();
    const itemIcons = groups.flatMap((g) => g.items.map((i) => i.icon));
    expect(new Set(itemIcons).size).toBe(itemIcons.length);
    const groupIcons = new Set(groups.map((g) => g.icon));
    expect(groupIcons.size).toBe(groups.length);
    expect(itemIcons.filter((icon) => groupIcons.has(icon))).toEqual([]);
  });
});
