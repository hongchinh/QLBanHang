import { describe, expect, it } from 'vitest';
import { buildHelpIndex, firstStaticPath } from './registry';
import type { HelpDoc } from './types';

function doc(key: string, routes: string[]): HelpDoc {
  return {
    key,
    title: key,
    routes,
    sources: ['x'],
    related: [],
    updated: '2026-10-15',
    purpose: 'Một câu mục đích.',
    steps: [
      { id: 'a', text: 'Bước một' },
      { id: 'b', text: 'Bước hai' },
      { id: 'c', text: 'Bước ba' },
    ],
    notes: [],
    shortcuts: [],
  };
}

const home = doc('home', ['/']);
const list = doc('stock-out.list', ['/stock-out']);
const form = doc('stock-out.form', ['/stock-out/new', '/stock-out/:id']);
const revenue = doc('reports-sales-revenue', [
  '/reports/sales-revenue',
  '/reports/sales-revenue/:saleUserId',
]);

describe('buildHelpIndex', () => {
  const index = buildHelpIndex([home, list, form, revenue]);

  it('resolves static and dynamic paths to the owning doc', () => {
    expect(index.resolve('/stock-out/new')).toBe(form);
    expect(index.resolve('/stock-out/0b9c')).toBe(form);
    expect(index.resolve('/stock-out')).toBe(list);
    expect(index.resolve('/stock-out/')).toBe(list);
    expect(index.resolve('/')).toBe(home);
    expect(index.resolve('/reports/sales-revenue/u1')).toBe(revenue);
  });

  it('prefers a static route over a :param route of another doc, whatever the key order', () => {
    const both = buildHelpIndex([doc('a.detail', ['/x/:id']), doc('z.new', ['/x/new'])]);
    expect(both.resolve('/x/new')?.key).toBe('z.new');
    expect(both.resolve('/x/42')?.key).toBe('a.detail');
  });

  it('returns null for an unknown path', () => {
    expect(index.resolve('/products')).toBeNull();
  });

  it('sorts docs by key and indexes them by key', () => {
    expect(index.docs.map((d) => d.key)).toEqual([
      'home',
      'reports-sales-revenue',
      'stock-out.form',
      'stock-out.list',
    ]);
    expect(index.byKey.get('home')).toBe(home);
  });
});

describe('firstStaticPath', () => {
  it('returns the first route when it is static', () => {
    expect(firstStaticPath(form)).toBe('/stock-out/new');
  });

  it('replaces :param segments with _', () => {
    expect(firstStaticPath(doc('x', ['/admin/user-settings/:userId']))).toBe(
      '/admin/user-settings/_',
    );
  });
});
