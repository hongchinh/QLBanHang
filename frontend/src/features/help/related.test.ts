import { describe, expect, it } from 'vitest';
import { productGroupsDoc, receiptsDoc, stockOutDoc, fixtureDocs } from '@/components/help/test-fixtures';
import { visibleNavGroups } from '@/components/layout/nav-config';
import { buildHelpIndex } from './registry';
import { accessibleRelated, docsInSameGroup } from './related';
import type { HelpDoc } from './types';

const index = buildHelpIndex(fixtureDocs);
const groups = visibleNavGroups(() => true, () => true);

describe('accessibleRelated', () => {
  it('returns the related docs the user can open', () => {
    expect(accessibleRelated(stockOutDoc, index, () => true)).toEqual([receiptsDoc]);
  });
  it('drops a related doc whose page is refused', () => {
    expect(accessibleRelated(stockOutDoc, index, (p) => p !== '/receipts/new')).toEqual([]);
  });
  it('skips an unknown related key', () => {
    const doc: HelpDoc = { ...stockOutDoc, related: ['nope', 'receipts.form'] };
    expect(accessibleRelated(doc, index, () => true)).toEqual([receiptsDoc]);
  });
});

describe('docsInSameGroup', () => {
  it('lists the docs of the same sidebar group', () => {
    expect(docsInSameGroup('/products', groups, index, () => true)).toEqual([productGroupsDoc]);
  });
  it('sorts the docs by title in Vietnamese order', () => {
    const mk = (key: string, title: string, route: string): HelpDoc => ({ ...productGroupsDoc, key, title, routes: [route] });
    const sorted = buildHelpIndex([
      mk('a-suppliers', 'Đơn vị', '/suppliers'),
      mk('b-warehouses', 'Ăn', '/warehouses'),
      mk('c-groups', 'Bò', '/product-groups'),
    ]);
    expect(docsInSameGroup('/products', groups, sorted, () => true).map((d) => d.title)).toEqual(['Ăn', 'Bò', 'Đơn vị']);
  });
  it('drops docs the user cannot open', () => {
    expect(docsInSameGroup('/products', groups, index, (p) => p !== '/product-groups')).toEqual([]);
  });
  it('returns nothing for a page outside every group', () => {
    expect(docsInSameGroup('/', groups, index, () => true)).toEqual([]);
  });
});
