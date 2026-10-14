import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { FundBalance } from '@/features/executive-dashboard/types';
import { FundBalanceTable } from './fund-balance-table';

const fund = (over: Partial<FundBalance>): FundBalance => ({
  fundId: 'f1', branchId: 'b1', branchCode: 'CN01', code: 'QTM', name: 'Tiền mặt', kind: 'Cash', isActive: true,
  balance: 19_300, ...over,
});
const FUNDS: FundBalance[] = [
  fund({}),
  fund({ fundId: 'f2', code: 'NH01', name: 'Vietcombank', kind: 'Bank', balance: 10_000 }),
  fund({ fundId: 'f3', branchId: 'b2', branchCode: 'CN02', code: 'QTM', name: 'Tiền mặt', balance: -25_000 }),
];

const bodyRows = () => within(screen.getAllByRole('rowgroup')[1]).getAllByRole('row');
const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((c) => c.textContent);

describe('FundBalanceTable', () => {
  it('has one row per fund: "code – name" and the balance with thousand separators', () => {
    render(<FundBalanceTable funds={FUNDS} showBranch={false} />);

    expect(screen.getAllByRole('columnheader').map((h) => h.textContent)).toEqual(['Quỹ', 'Số dư']);
    expect(bodyRows().map(cells)).toEqual([
      ['QTM – Tiền mặt', '19.300'],
      ['NH01 – Vietcombank', '10.000'],
      ['QTM – Tiền mặt', '-25.000'],
    ]);
    expect(screen.getByRole('columnheader', { name: 'Số dư' })).toHaveClass('text-right', 'tabular-nums');
  });

  it('puts the branch code in front with showBranch', () => {
    render(<FundBalanceTable funds={FUNDS} showBranch />);

    expect(bodyRows().map((row) => cells(row)[0])).toEqual([
      'CN01 · QTM – Tiền mặt',
      'CN01 · NH01 – Vietcombank',
      'CN02 · QTM – Tiền mặt',
    ]);
  });

  it('shows a negative balance in rose', () => {
    render(<FundBalanceTable funds={FUNDS} showBranch={false} />);

    expect(screen.getByText('-25.000').className).toContain('rose');
    expect(screen.getByText('19.300').className).not.toContain('rose');
  });

  it('limit keeps the funds with the largest balance in absolute value', () => {
    const { unmount } = render(<FundBalanceTable funds={FUNDS} showBranch limit={1} />);
    expect(bodyRows().map(cells)).toEqual([['CN02 · QTM – Tiền mặt', '-25.000']]);
    unmount();

    render(<FundBalanceTable funds={FUNDS} showBranch limit={2} />);
    expect(bodyRows().map((row) => cells(row)[1])).toEqual(['-25.000', '19.300']);
  });

  it('a limit above the number of funds keeps them all', () => {
    render(<FundBalanceTable funds={FUNDS} showBranch={false} limit={5} />);

    expect(bodyRows()).toHaveLength(3);
  });

  it('total adds a "Tổng cộng" row', () => {
    render(<FundBalanceTable funds={FUNDS.slice(0, 2)} showBranch={false} total={29_300} />);

    const rows = screen.getAllByRole('row');
    const total = rows[rows.length - 1];
    // "Tổng cộng" is the header of its row.
    expect(within(total).getByRole('rowheader', { name: 'Tổng cộng' })).toHaveAttribute('scope', 'row');
    expect(cells(total)).toEqual(['29.300']);
    expect(within(total).getByText('29.300')).toHaveClass('text-right', 'tabular-nums');
  });

  it('has no "Tổng cộng" row without total', () => {
    render(<FundBalanceTable funds={FUNDS} showBranch={false} />);

    expect(screen.queryByText('Tổng cộng')).not.toBeInTheDocument();
  });
});
