import { describe, expect, it } from 'vitest';
import type { NavItem } from '../nav-config';
import { allNavGroups } from './sidebar-test-utils';
import { filterNavGroups, findActiveItem, normalizeVi, resolvePinnedItems } from './nav-search';

const item = (to: string): NavItem => ({ to, label: to, icon: 'overview' });
const ITEMS = [
  item('/admin/dashboard'), item('/quotations'), item('/settings'),
  item('/settings/my-quotation-settings'), item('/reports/sales-revenue'), item('/admin/users'),
];

describe('findActiveItem', () => {
  it.each([
    ['/settings/my-quotation-settings', '/settings/my-quotation-settings'],
    ['/settings', '/settings'],
    ['/settings/branches', '/settings'],
    ['/quotations/123', '/quotations'],
    ['/quotations/', '/quotations'],
    ['/reports/sales-revenue/0b6c', '/reports/sales-revenue'],
    ['/admin/dashboard', '/admin/dashboard'],
  ])('%s → %s', (pathname, expected) => {
    expect(findActiveItem(pathname, ITEMS)?.to).toBe(expected);
  });

  it('matches on segment boundaries only', () => {
    expect(findActiveItem('/settings-x', ITEMS)).toBeNull();
    expect(findActiveItem('/quotationsx/1', ITEMS)).toBeNull();
  });

  it('returns null for a page without an item', () => {
    expect(findActiveItem('/admin/user-settings/u1', ITEMS)).toBeNull();
  });

  it('matches "/" exactly', () => {
    const withRoot = [item('/'), ...ITEMS];
    expect(findActiveItem('/', withRoot)?.to).toBe('/');
    expect(findActiveItem('/unknown', withRoot)).toBeNull();
    expect(findActiveItem('/quotations/1', withRoot)?.to).toBe('/quotations');
  });
});

describe('normalizeVi', () => {
  it('strips accents, maps đ to d and lowercases', () => {
    expect(normalizeVi('Đối trừ chứng từ')).toBe('doi tru chung tu');
    expect(normalizeVi('ĐƯỜNG Hóa')).toBe('duong hoa');
  });

  it('keeps one character per input character', () => {
    for (const label of ['Đối trừ chứng từ', 'Số dư công nợ', 'Lý do thu/chi']) {
      expect(normalizeVi(label)).toHaveLength(label.normalize('NFC').length);
    }
    expect(normalizeVi('Đối'.normalize('NFD'))).toBe('doi');
  });
});

describe('filterNavGroups', () => {
  const groups = allNavGroups();
  const ids = (r: ReturnType<typeof filterNavGroups>) => r.groups.map((g) => g.id);
  const tos = (r: ReturnType<typeof filterNavGroups>, id: string) => r.groups.find((g) => g.id === id)?.items.map((i) => i.to);

  it('matches item labels without accents and returns the highlight range', () => {
    const r = filterNavGroups(groups, 'doi tru');
    expect(ids(r)).toEqual(['debt']);
    expect(tos(r, 'debt')).toEqual(['/debt/matching']);
    expect(r.matches.get('/debt/matching')).toEqual({ start: 0, end: 7 });
    expect(r.count).toBe(1);
  });

  it('a group label match returns every item of the group', () => {
    const r = filterNavGroups(groups, 'bao cao');
    expect(ids(r)).toEqual(['reports']);
    expect(r.count).toBe(16);
    expect(r.matches.size).toBe(0);
  });

  it('a section match returns every item of the section', () => {
    const r = filterNavGroups(groups, 'cong no');
    expect(ids(r)).toEqual(['debt', 'reports']);
    expect(tos(r, 'debt')).toEqual(['/debt/matching', '/debt/offsets', '/debt/opening', '/credit-limits']);
    expect(tos(r, 'reports')).toContain('/reports/debt-aging');
    expect(r.matches.has('/reports/debt-aging')).toBe(false);
    expect(r.matches.get('/reports/debt-balance')).toEqual({ start: 6, end: 13 });
    expect(r.count).toBe(11);
  });

  it('trims the query; an empty query keeps every group', () => {
    const r = filterNavGroups(groups, '   ');
    expect(ids(r)).toEqual(groups.map((g) => g.id));
    expect(r.count).toBe(groups.reduce((n, g) => n + g.items.length, 0));
    expect(filterNavGroups(groups, '  Phieu thu ').matches.get('/receipts')).toEqual({ start: 0, end: 9 });
  });

  it('returns nothing for an unknown word', () => {
    expect(filterNavGroups(groups, 'xyz')).toEqual({ groups: [], matches: new Map(), count: 0 });
  });
});

describe('resolvePinnedItems', () => {
  const groups = allNavGroups();

  it('returns visible items in pin order', () => {
    expect(resolvePinnedItems(['/receipts', '/quotations'], groups).map((i) => i.label)).toEqual(['Phiếu thu', 'Báo giá']);
  });

  it('skips paths without a visible item', () => {
    const salesOnly = groups.filter((g) => g.id === 'sales');
    expect(resolvePinnedItems(['/receipts', '/gone', '/quotations'], salesOnly).map((i) => i.to)).toEqual(['/quotations']);
  });
});
