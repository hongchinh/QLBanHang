import { describe, expect, it } from 'vitest';
import { canAccessRouteWith } from '@/lib/route-permissions';
import { executiveLinks, reportLinkTitle } from './links';

describe('executiveLinks', () => {
  it('debtOverdue carries the side and the as-at date', () => {
    expect(executiveLinks.debtOverdue('Receivable', '2026-10-13')).toBe(
      '/reports/debt-overdue?side=Receivable&at=2026-10-13',
    );
    expect(executiveLinks.debtOverdue('Payable', '2026-10-13')).toBe('/reports/debt-overdue?side=Payable&at=2026-10-13');
  });

  it('debtOverdue appends the partner when one is given', () => {
    expect(executiveLinks.debtOverdue('Receivable', '2026-10-13', 'p-1')).toBe(
      '/reports/debt-overdue?side=Receivable&at=2026-10-13&partnerId=p-1',
    );
    expect(executiveLinks.debtOverdue('Receivable', '2026-10-13', undefined)).not.toContain('partnerId');
  });

  it('debtAging carries the side and the as-at date', () => {
    expect(executiveLinks.debtAging('Payable', '2026-10-13')).toBe('/reports/debt-aging?side=Payable&at=2026-10-13');
    expect(executiveLinks.debtAging('Receivable', '2026-10-13')).toBe('/reports/debt-aging?side=Receivable&at=2026-10-13');
  });

  it('debtBalance carries the side and the period', () => {
    expect(executiveLinks.debtBalance('Payable', '2026-10-01', '2026-10-13')).toBe(
      '/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-13',
    );
    expect(executiveLinks.debtBalance('Receivable', '2026-01-01', '2026-10-13')).toBe(
      '/reports/debt-balance?side=Receivable&from=2026-01-01&to=2026-10-13',
    );
  });

  it('debtBySalesperson is the receivable side of the period', () => {
    expect(executiveLinks.debtBySalesperson('2026-10-01', '2026-10-13')).toBe(
      '/reports/debt-by-salesperson?side=Receivable&from=2026-10-01&to=2026-10-13',
    );
  });

  it('the cash book, stock on hand and credit limits open without parameters', () => {
    expect(executiveLinks.cashBook()).toBe('/reports/cash-book');
    expect(executiveLinks.stockOnHand()).toBe('/inventory/stock-on-hand');
    expect(executiveLinks.creditLimits()).toBe('/credit-limits');
  });

  // A path no rule knows is open to everyone: a typo in a link would show it to users who then land on a 404.
  it.each([
    [executiveLinks.debtOverdue('Receivable', '2026-10-13', 'p-1'), 'reports.debt'],
    [executiveLinks.debtAging('Payable', '2026-10-13'), 'reports.debt'],
    [executiveLinks.debtBalance('Payable', '2026-10-01', '2026-10-13'), 'reports.debt'],
    [executiveLinks.debtBySalesperson('2026-10-01', '2026-10-13'), 'reports.debt'],
    [executiveLinks.cashBook(), 'reports.cash'],
    [executiveLinks.stockOnHand(), 'reports.inventory'],
    [executiveLinks.creditLimits(), 'credit_limit.manage'],
  ])('%s is a route guarded by %s', (link, permission) => {
    const path = link.split('?')[0];
    const noRole = () => false;

    expect(canAccessRouteWith(path, () => false, noRole)).toBe(false);
    expect(canAccessRouteWith(path, (p) => p === permission, noRole)).toBe(true);
  });
});

describe('reportLinkTitle', () => {
  it('says which branch the report opens for when the dashboard shows the whole company', () => {
    expect(reportLinkTitle('company', 3, 'CN01')).toBe('Báo cáo mở theo chi nhánh đang làm việc (CN01)');
    expect(reportLinkTitle('company', 2, 'HN')).toBe('Báo cáo mở theo chi nhánh đang làm việc (HN)');
  });

  it('is undefined for a company with one branch: the report shows the same', () => {
    expect(reportLinkTitle('company', 1, 'CN01')).toBeUndefined();
    expect(reportLinkTitle('company', 0, 'CN01')).toBeUndefined();
  });

  it('is undefined in branch scope', () => {
    expect(reportLinkTitle('branch', 3, 'CN01')).toBeUndefined();
    expect(reportLinkTitle('branch', 1, 'CN01')).toBeUndefined();
  });

  it('leaves the code out while the working branch is not known', () => {
    expect(reportLinkTitle('company', 3)).toBe('Báo cáo mở theo chi nhánh đang làm việc');
    expect(reportLinkTitle('company', 3, undefined)).toBe('Báo cáo mở theo chi nhánh đang làm việc');
  });
});
