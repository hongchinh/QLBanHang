import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Attention } from '@/features/executive-dashboard/types';
import { useAuthStore } from '@/stores/auth-store';
import { AttentionList } from './attention-list';

const FULL: Attention = {
  at: '2026-10-13',
  overdueReceivables: { count: 12, amount: 420_000_000 },
  overCreditLimit: { count: 3, amount: 96_000_000 },
  negativeStock: { count: 5, amount: null },
  payablesDueSoon: { count: 4, amount: 310_000_000 },
  negativeFunds: { count: 0, amount: 0 },
};
const NONE: Attention = {
  at: '2026-10-13',
  overdueReceivables: null,
  overCreditLimit: null,
  negativeStock: null,
  payablesDueSoon: null,
  negativeFunds: null,
};
const EVERY_ROUTE = ['reports.debt', 'credit_limit.manage', 'reports.inventory', 'reports.cash'];

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderList(attention: Attention | undefined, linkTitle?: string) {
  return render(
    <MemoryRouter>
      <AttentionList attention={attention} today="2026-10-13" from="2026-10-01" to="2026-10-13" linkTitle={linkTitle} />
    </MemoryRouter>,
  );
}

const rows = () => screen.getAllByRole('listitem');
const rowOf = (text: string) => {
  const row = screen.getByText(text).closest('li');
  if (!row) throw new Error(`no row for ${text}`);
  return row;
};
const dotOf = (text: string) => rowOf(text).querySelector('[data-dot]');

describe('AttentionList', () => {
  beforeEach(() => signIn(EVERY_ROUTE));
  afterEach(() => useAuthStore.setState({ user: null }));

  it('has one row per item, in order, each with its count', () => {
    renderList(FULL);

    expect(rows()).toHaveLength(5);
    expect(rows().map((row) => row.textContent)).toEqual([
      expect.stringMatching(/^12Khách nợ quá hạn420\.000\.000\s₫$/),
      expect.stringMatching(/^3Khách vượt hạn mức nợvượt 96\.000\.000\s₫$/),
      '5Mã hàng tồn âm',
      expect.stringMatching(/^4Khoản phải trả đến hạn 7 ngày310\.000\.000\s₫$/),
      '0Quỹ âmKhông có',
    ]);
    expect(within(rowOf('Khách nợ quá hạn')).getByText('12')).toHaveClass('tabular-nums');
  });

  it('leaves out the items the user may not see', () => {
    renderList({ ...FULL, overCreditLimit: null, negativeStock: null });

    expect(rows()).toHaveLength(3);
    expect(screen.queryByText('Khách vượt hạn mức nợ')).not.toBeInTheDocument();
    expect(screen.queryByText('Mã hàng tồn âm')).not.toBeInTheDocument();
  });

  it('says so when the user may see none of them', () => {
    renderList(NONE);

    expect(screen.getByText('Không có mục nào trong quyền xem của bạn.')).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });

  it('renders nothing before the data is there', () => {
    const { container } = renderList(undefined);

    expect(container).toBeEmptyDOMElement();
  });

  it('a row with count 0 says "Không có", has an emerald dot and no link', () => {
    renderList({ ...FULL, overdueReceivables: { count: 0, amount: 0 } });

    expect(within(rowOf('Khách nợ quá hạn')).getByText('Không có')).toBeInTheDocument();
    expect(dotOf('Khách nợ quá hạn')?.className).toContain('emerald');
    expect(rowOf('Khách nợ quá hạn').querySelector('a')).toBeNull();
    expect(rowOf('Quỹ âm').querySelector('a')).toBeNull();
    expect(dotOf('Quỹ âm')?.className).toContain('emerald');
  });

  it('colours the dot of an item that needs attention', () => {
    renderList({ ...FULL, negativeFunds: { count: 1, amount: 5_000_000 } });

    expect(dotOf('Khách nợ quá hạn')?.className).toContain('rose');
    expect(dotOf('Khách vượt hạn mức nợ')?.className).toContain('rose');
    expect(dotOf('Mã hàng tồn âm')?.className).toContain('amber');
    expect(dotOf('Khoản phải trả đến hạn 7 ngày')?.className).toContain('amber');
    expect(dotOf('Quỹ âm')?.className).toContain('rose');
  });

  it('links each row with a count to its report', () => {
    renderList({ ...FULL, negativeFunds: { count: 1, amount: 5_000_000 } });

    const href = (text: string) => within(rowOf(text)).getByRole('link').getAttribute('href');
    expect(href('Khách nợ quá hạn')).toBe('/reports/debt-overdue?side=Receivable&at=2026-10-13');
    expect(href('Khách vượt hạn mức nợ')).toBe('/credit-limits');
    expect(href('Mã hàng tồn âm')).toBe('/inventory/stock-on-hand');
    expect(href('Khoản phải trả đến hạn 7 ngày')).toBe('/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-13');
    expect(href('Quỹ âm')).toBe('/reports/cash-book');
    expect(within(rowOf('Quỹ âm')).getByText(/^5\.000\.000\s₫$/)).toBeInTheDocument();
  });

  it('"Khách nợ quá hạn" is plain text for a user without reports.debt', () => {
    signIn(['credit_limit.manage', 'reports.inventory', 'reports.cash']);
    renderList(FULL);

    expect(rowOf('Khách nợ quá hạn').querySelector('a')).toBeNull();
    expect(within(rowOf('Khách nợ quá hạn')).getByText('12')).toBeInTheDocument();
    expect(rowOf('Khách vượt hạn mức nợ').querySelector('a')).not.toBeNull();
  });

  it('the credit limit row needs the right to manage credit limits', () => {
    signIn(['reports.debt']);
    renderList(FULL);

    expect(rowOf('Khách vượt hạn mức nợ').querySelector('a')).toBeNull();
    expect(rowOf('Khách nợ quá hạn').querySelector('a')).not.toBeNull();
  });

  it('the report links carry the hint of the working branch', () => {
    renderList(FULL, 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');

    for (const text of ['Khách nợ quá hạn', 'Mã hàng tồn âm', 'Khoản phải trả đến hạn 7 ngày']) {
      expect(rowOf(text).querySelector('a')).toHaveAttribute('title', 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');
    }
  });

  // The credit limits page shows the limits of the working branch only, while the count covers the scope.
  it('the credit limit row carries the hint of the working branch too', () => {
    renderList(FULL, 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');

    const link = rowOf('Khách vượt hạn mức nợ').querySelector('a');
    expect(link).toHaveAttribute('href', '/credit-limits');
    expect(link).toHaveAttribute('title', 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');
  });

  it('the report links have no title without a hint', () => {
    renderList(FULL);

    for (const link of screen.getAllByRole('link')) expect(link).not.toHaveAttribute('title');
  });
});
