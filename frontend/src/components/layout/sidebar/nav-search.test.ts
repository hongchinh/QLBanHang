import { describe, expect, it } from 'vitest';
import { LayoutDashboard } from 'lucide-react';
import type { NavItem } from '../nav-config';
import { findActiveItem } from './nav-search';

const item = (to: string): NavItem => ({ to, label: to, icon: LayoutDashboard });
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
