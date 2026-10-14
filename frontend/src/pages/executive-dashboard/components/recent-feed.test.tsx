import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { RecentDocument } from '@/features/executive-dashboard/types';
import { useAuthStore } from '@/stores/auth-store';
import { RecentFeed } from './recent-feed';

// Instants of the local day, so the printed time does not depend on the zone of the machine.
const at = (hours: number, minutes: number) => new Date(2026, 9, 14, hours, minutes).toISOString();

const ITEMS: RecentDocument[] = [
  { kind: 'Receipt', id: 'r1', code: 'PT00124', createdAt: at(14, 31), branchCode: 'CN01', partnerName: 'An Phát', ownerName: 'N.T.Hoa', amount: 45_000_000 },
  { kind: 'StockOut', id: 's1', code: 'XK00311', createdAt: at(14, 12), branchCode: 'CN01', partnerName: 'Minh Long', ownerName: 'T.V.Nam', amount: 128_400_000 },
  { kind: 'Payment', id: 'p1', code: 'PC00087', createdAt: at(13, 50), branchCode: 'CN01', partnerName: null, ownerName: 'N.T.Hoa', amount: 12_000_000 },
  { kind: 'StockIn', id: 's2', code: 'NK00092', createdAt: at(11, 5), branchCode: 'CN02', partnerName: 'NCC Hoà Phát', ownerName: 'L.Q.Huy', amount: 260_000_000 },
];

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderFeed(items: RecentDocument[] = ITEMS, showBranch = false) {
  return render(
    <MemoryRouter>
      <RecentFeed items={items} workingBranchCode="CN01" showBranch={showBranch} />
    </MemoryRouter>,
  );
}

const rows = () => screen.getAllByRole('listitem');
const rowOf = (code: string) => {
  const row = screen.getByText(code).closest('li');
  if (!row) throw new Error(`no row for ${code}`);
  return row;
};

describe('RecentFeed', () => {
  beforeEach(() => signIn(['receipts.view', 'payments.view', 'stock_in.view', 'stock_out.view']));
  afterEach(() => useAuthStore.setState({ user: null }));

  it('tags each document by its kind', () => {
    renderFeed();

    expect(rows()).toHaveLength(4);
    const tag = (code: string, label: string) => within(rowOf(code)).getByText(label);
    expect(tag('PT00124', 'Thu').className).toContain('emerald');
    expect(tag('PC00087', 'Chi').className).toContain('rose');
    expect(tag('XK00311', 'Xuất').className).toContain('primary');
    expect(tag('NK00092', 'Nhập').className).toContain('primary');
  });

  it('links a receipt of the working branch for a user who may open it', () => {
    renderFeed();

    expect(within(rowOf('PT00124')).getByRole('link', { name: 'PT00124' })).toHaveAttribute('href', '/receipts/r1');
    expect(within(rowOf('XK00311')).getByRole('link', { name: 'XK00311' })).toHaveAttribute('href', '/stock-out/s1');
    expect(within(rowOf('PC00087')).getByRole('link', { name: 'PC00087' })).toHaveAttribute('href', '/payments/p1');
  });

  it('a document of another branch is plain text', () => {
    renderFeed();

    expect(within(rowOf('NK00092')).queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText('NK00092')).toHaveAttribute('title', 'Chứng từ của chi nhánh CN02');
  });

  it('a receipt is plain text for a user without receipts.view', () => {
    signIn(['payments.view']);
    renderFeed();

    expect(within(rowOf('PT00124')).queryByRole('link')).not.toBeInTheDocument();
  });

  it('prints the amount with "+" for a receipt and "-" for a payment', () => {
    renderFeed();

    expect(within(rowOf('PT00124')).getByText('+45.000.000')).toHaveClass('tabular-nums');
    expect(within(rowOf('PC00087')).getByText('-12.000.000')).toHaveClass('tabular-nums');
    expect(within(rowOf('XK00311')).getByText('128.400.000')).toBeInTheDocument();
    expect(within(rowOf('NK00092')).getByText('260.000.000')).toBeInTheDocument();
  });

  it('prints partner, owner and time, leaving out what is missing', () => {
    renderFeed();

    expect(within(rowOf('PT00124')).getByText('An Phát · N.T.Hoa · 14:31')).toBeInTheDocument();
    expect(within(rowOf('PC00087')).getByText('N.T.Hoa · 13:50')).toBeInTheDocument();
    expect(within(rowOf('NK00092')).getByText('NCC Hoà Phát · L.Q.Huy · 11:05')).toBeInTheDocument();
  });

  it('a row without partner nor owner shows the time', () => {
    renderFeed([{ ...ITEMS[0], partnerName: null, ownerName: null }]);

    expect(within(rowOf('PT00124')).getByText('14:31')).toBeInTheDocument();
  });

  it('puts the branch code in front for the whole company', () => {
    renderFeed(ITEMS, true);

    expect(within(rowOf('PT00124')).getByText('CN01 · An Phát · N.T.Hoa · 14:31')).toBeInTheDocument();
    expect(within(rowOf('NK00092')).getByText('CN02 · NCC Hoà Phát · L.Q.Huy · 11:05')).toBeInTheDocument();
  });

  it('says so when nothing was created today', () => {
    renderFeed([]);

    expect(screen.getByText('Hôm nay chưa có chứng từ.')).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });

  it('nothing is a link while the working branch is not known', () => {
    render(
      <MemoryRouter>
        <RecentFeed items={ITEMS} workingBranchCode={undefined} showBranch={false} />
      </MemoryRouter>,
    );

    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText('PT00124')).toBeInTheDocument();
  });
});
