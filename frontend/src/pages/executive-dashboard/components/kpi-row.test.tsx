import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Branch } from '@/features/branches/types';
import { useCashSummary, useDebtSummary, useInventorySummary, useSalesSummary } from '@/features/executive-dashboard/hooks';
import type {
  CashSummary,
  DebtSideSummary,
  DebtSummary,
  ExecutiveParams,
  InventorySummary,
  SalesSummary,
} from '@/features/executive-dashboard/types';
import type { useExecutiveAccess } from '@/features/executive-dashboard/use-executive-access';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { todayYmd } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import { KpiRow } from './kpi-row';

vi.mock('@/features/executive-dashboard/hooks', () => ({
  useSalesSummary: vi.fn(),
  useCashSummary: vi.fn(),
  useDebtSummary: vi.fn(),
  useInventorySummary: vi.fn(),
}));

type Scope = ReturnType<typeof useExecutiveScope>;
type Access = ReturnType<typeof useExecutiveAccess>;
const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const B3: Branch = { id: 'b3', code: 'CN03', name: 'Đà Nẵng' };
const branchScope = (): Scope => ({
  canSwitch: true, mode: 'branch', branchId: 'b1', workingBranch: B1, branches: [B1, B2, B3],
  selectCompany: vi.fn(), selectBranch: vi.fn(), unknown: false, retry: vi.fn(),
});
const allDomains = (): Access => ({
  sales: true, cash: true, debt: true, inventory: true, tabs: ['overview', 'sales', 'cash', 'debt', 'inventory'],
});
const state: { scope: Scope; access: Access } = { scope: branchScope(), access: allDomains() };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({ useExecutiveScope: () => state.scope }));
vi.mock('@/features/executive-dashboard/use-executive-access', () => ({ useExecutiveAccess: () => state.access }));

const SALES: SalesSummary = {
  from: '2026-10-01', to: '2026-10-13', prevFrom: '2026-09-18', prevTo: '2026-09-30',
  revenue: 2_840_000_000, prevRevenue: 2_526_690_000, revenueDeltaPct: 12.4,
  canViewCost: true, cost: 2_428_000_000, grossProfit: 412_000_000, marginPct: 14.5, prevMarginPct: 13.7,
  isProvisional: false,
};
const CASH: CashSummary = {
  from: '2026-10-01', to: '2026-10-13', prevFrom: '2026-09-18', prevTo: '2026-09-30', at: '2026-10-13',
  receipts: 2_100_000_000, payments: 1_914_000_000, net: 186_000_000, prevNet: 150_000_000, netDeltaPct: 24,
  balance: 1_260_000_000, cashBalance: 210_000_000, bankBalance: 1_050_000_000, funds: [],
};
const side = (over: Partial<DebtSideSummary> = {}): DebtSideSummary => ({
  balance: 0, totalOpen: 0, unallocated: 0, notDue: 0, overdue: 0, overduePartnerCount: 0,
  overdueAmounts: [0, 0, 0, 0], dueSoon: 0, dueSoonCount: 0, ...over,
});
const DEBT: DebtSummary = {
  at: '2026-10-13',
  overdueBuckets: [{ from: 1, to: 30 }, { from: 31, to: 60 }, { from: 61, to: 90 }, { from: 91, to: null }],
  receivable: side({ balance: 3_100_000_000, totalOpen: 3_100_000_000, overdue: 420_000_000 }),
  payable: side({ balance: 1_750_000_000, totalOpen: 1_750_000_000, dueSoon: 310_000_000, dueSoonCount: 4 }),
};
const INVENTORY: InventorySummary = {
  from: '2026-10-01', to: '2026-10-13', canViewCost: true, stockValue: 4_620_000_000,
  productCount: 318, warehouseCount: 3, negativeCount: 0, inValue: 0, outCost: 0,
};

interface FakeQuery<T> {
  data: T | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  isPlaceholderData: boolean;
  refetch: ReturnType<typeof vi.fn>;
}
const ok = <T,>(data: T, over: Partial<FakeQuery<T>> = {}): FakeQuery<T> => ({
  data, isLoading: false, isError: false, error: null, isPlaceholderData: false, refetch: vi.fn(), ...over,
});
const failed = <T,>(): FakeQuery<T> => ok<T>(undefined as T, { data: undefined, isError: true, error: new Error('boom') });

function answer(hook: unknown, query: FakeQuery<unknown>) {
  vi.mocked(hook as (...args: unknown[]) => unknown).mockReturnValue(query);
}

const PARAMS: ExecutiveParams = { from: '2026-10-01', to: '2026-10-13', branchId: 'b1' };

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderRow(params: ExecutiveParams = PARAMS) {
  return render(
    <MemoryRouter>
      <KpiRow params={params} />
    </MemoryRouter>,
  );
}

// The tile of a label: its link (the label comes first in it), or the group a tile without a link is.
function tileOf(label: string): HTMLElement {
  const link = screen.queryAllByRole('link').find((a) => a.textContent?.startsWith(label));
  return link ?? screen.getByRole('group', { name: label });
}

const LABELS = [
  'Doanh thu xuất bán', 'Lãi gộp', 'Thu − chi ròng', 'Tồn quỹ', 'Phải thu', 'Phải trả', 'Giá trị tồn kho',
];

describe('KpiRow', () => {
  beforeEach(() => {
    state.scope = branchScope();
    state.access = allDomains();
    answer(useSalesSummary, ok(SALES));
    answer(useCashSummary, ok(CASH));
    answer(useDebtSummary, ok(DEBT));
    answer(useInventorySummary, ok(INVENTORY));
    signIn(['reports.profit', 'reports.cash', 'reports.debt', 'reports.inventory']);
  });
  afterEach(() => {
    vi.mocked(useSalesSummary).mockReset();
    vi.mocked(useCashSummary).mockReset();
    vi.mocked(useDebtSummary).mockReset();
    vi.mocked(useInventorySummary).mockReset();
    useAuthStore.setState({ user: null });
  });

  it('shows the seven tiles with their compact values', () => {
    renderRow();

    for (const label of LABELS) expect(screen.getByText(label)).toBeInTheDocument();
    const values: Record<string, string> = {
      'Doanh thu xuất bán': '2,84 tỷ',
      'Lãi gộp': '412 tr',
      'Thu − chi ròng': '+186 tr',
      'Tồn quỹ': '1,26 tỷ',
      'Phải thu': '3,10 tỷ',
      'Phải trả': '1,75 tỷ',
      'Giá trị tồn kho': '4,62 tỷ',
    };
    for (const [label, value] of Object.entries(values)) {
      expect(within(tileOf(label)).getByText(value)).toHaveClass('tabular-nums');
    }
  });

  it('groups the tiles under "Trong kỳ" and "Hiện tại"', () => {
    renderRow();

    const period = screen.getByRole('group', { name: 'Trong kỳ' });
    const now = screen.getByRole('group', { name: 'Hiện tại' });
    for (const label of ['Doanh thu xuất bán', 'Lãi gộp', 'Thu − chi ròng']) {
      expect(within(period).getByText(label)).toBeInTheDocument();
    }
    for (const label of ['Tồn quỹ', 'Phải thu', 'Phải trả', 'Giá trị tồn kho']) {
      expect(within(now).getByText(label)).toBeInTheDocument();
    }
  });

  it('each group heading spans the tiles of its own group, also with some domains missing', () => {
    state.access = { sales: false, cash: true, debt: true, inventory: false, tabs: ['overview', 'cash', 'debt'] };
    renderRow();

    const period = screen.getByRole('group', { name: 'Trong kỳ' });
    const now = screen.getByRole('group', { name: 'Hiện tại' });
    // On wide screens each group is a box of the row as wide as its tiles, which share its columns.
    expect(period).toHaveClass('grid-cols-subgrid', 'xl:col-span-1');
    expect(now).toHaveClass('grid-cols-subgrid', 'xl:col-span-3');
    for (const group of [period, now]) {
      expect(group).not.toHaveClass('contents');
      const heading = within(group).getByRole('heading');
      expect(heading).toHaveClass('col-span-full');
      expect(heading.className).not.toMatch(/xl:col-span-\d/);
    }
    expect(period).toContainElement(tileOf('Thu − chi ròng'));
    for (const label of ['Tồn quỹ', 'Phải thu', 'Phải trả']) {
      expect(now).toContainElement(tileOf(label));
      expect(period).not.toContainElement(tileOf(label));
    }
  });

  it('with every domain the groups are three and four columns wide', () => {
    renderRow();

    expect(screen.getByRole('group', { name: 'Trong kỳ' })).toHaveClass('xl:col-span-3');
    expect(screen.getByRole('group', { name: 'Hiện tại' })).toHaveClass('xl:col-span-4');
  });

  it('figures of the previous period or scope are dimmed while the new ones load', () => {
    answer(useSalesSummary, ok(SALES, { isPlaceholderData: true }));
    renderRow();

    expect(tileOf('Doanh thu xuất bán')).toHaveAttribute('aria-busy', 'true');
    expect(within(tileOf('Doanh thu xuất bán')).getByText('2,84 tỷ')).toHaveClass('opacity-60');
    expect(tileOf('Lãi gộp')).toHaveAttribute('aria-busy', 'true');
    expect(tileOf('Tồn quỹ')).not.toHaveAttribute('aria-busy');
    expect(within(tileOf('Tồn quỹ')).getByText('1,26 tỷ')).not.toHaveClass('opacity-60');
  });

  it('shows the hints and the deltas', () => {
    renderRow();

    expect(within(tileOf('Doanh thu xuất bán')).getByText('12,4%')).toBeInTheDocument();
    expect(within(tileOf('Doanh thu xuất bán')).getByRole('img', { name: 'tăng' })).toBeInTheDocument();
    expect(within(tileOf('Doanh thu xuất bán')).getByText('so với kỳ trước')).toBeInTheDocument();
    expect(within(tileOf('Thu − chi ròng')).getByText('24%')).toBeInTheDocument();
    expect(within(tileOf('Thu − chi ròng')).getByRole('img', { name: 'tăng' })).toBeInTheDocument();
    expect(within(tileOf('Thu − chi ròng')).getByText('Thu 2,10 tỷ · Chi 1,91 tỷ')).toBeInTheDocument();
    expect(within(tileOf('Tồn quỹ')).getByText('TM 210 tr · NH 1,05 tỷ')).toBeInTheDocument();
    expect(within(tileOf('Phải thu')).getByText('Quá hạn 420 tr')).toBeInTheDocument();
    expect(within(tileOf('Phải trả')).getByText('Đến hạn 7 ngày: 310 tr')).toBeInTheDocument();
    expect(within(tileOf('Giá trị tồn kho')).getByText('318 mã · 3 kho')).toBeInTheDocument();
    // The balance tiles have no delta (the gross profit has an arrow only in its margin hint).
    for (const label of ['Tồn quỹ', 'Phải thu', 'Phải trả', 'Giá trị tồn kho']) {
      expect(within(tileOf(label)).queryByRole('img', { name: /tăng|giảm/ })).not.toBeInTheDocument();
    }
  });

  it('a revenue without a previous one has "—" as delta', () => {
    answer(useSalesSummary, ok({ ...SALES, revenueDeltaPct: null }));
    renderRow();

    expect(within(tileOf('Doanh thu xuất bán')).getByText('—')).toBeInTheDocument();
  });

  describe('Lãi gộp', () => {
    it('shows the margin and its change in points', () => {
      renderRow();

      const hint = within(tileOf('Lãi gộp')).getByText(/^Tỷ suất/);
      expect(hint).toHaveTextContent('Tỷ suất 14,5%');
      expect(hint).toHaveTextContent('0,8 điểm');
      expect(hint.textContent).toBe('Tỷ suất 14,5% (0,8 điểm)');
      expect(within(hint).getByRole('img', { name: 'tăng' })).toBeInTheDocument();
    });

    it('rounds the change to one decimal', () => {
      answer(useSalesSummary, ok({ ...SALES, marginPct: 14.56, prevMarginPct: 13.71 }));
      renderRow();

      const hint = within(tileOf('Lãi gộp')).getByText(/^Tỷ suất/);
      expect(hint.textContent).toBe('Tỷ suất 14,6% (0,9 điểm)');
      expect(within(hint).getByRole('img', { name: 'tăng' })).toBeInTheDocument();
    });

    it('rounds a fall the same way as a rise', () => {
      answer(useSalesSummary, ok({ ...SALES, marginPct: 13.71, prevMarginPct: 14.56 }));
      renderRow();

      const hint = within(tileOf('Lãi gộp')).getByText(/^Tỷ suất/);
      expect(hint.textContent).toBe('Tỷ suất 13,7% (0,9 điểm)');
      expect(within(hint).getByRole('img', { name: 'giảm' })).toBeInTheDocument();
    });

    it('a lower margin than before is a down arrow', () => {
      answer(useSalesSummary, ok({ ...SALES, marginPct: 13, prevMarginPct: 14.5 }));
      renderRow();

      const hint = within(tileOf('Lãi gộp')).getByText(/^Tỷ suất/);
      expect(hint.textContent).toBe('Tỷ suất 13% (1,5 điểm)');
      expect(within(hint).getByRole('img', { name: 'giảm' })).toBeInTheDocument();
    });

    // A change that rounds to 0 points has no arrow: "▲ 0" would read as a rise of a margin that fell.
    it.each([
      ['a fall', 14.5, 14.54],
      ['a rise', 14.54, 14.5],
      ['no change', 14.5, 14.5],
    ])('%s that rounds to 0 points is "±0 điểm", without an arrow', (_case, marginPct, prevMarginPct) => {
      answer(useSalesSummary, ok({ ...SALES, marginPct, prevMarginPct }));
      renderRow();

      const hint = within(tileOf('Lãi gộp')).getByText(/^Tỷ suất/);
      expect(hint.textContent).toMatch(/^Tỷ suất 14,5% \(±0 điểm\)$/);
      expect(within(hint).queryByRole('img')).not.toBeInTheDocument();
    });

    it('has no change without a previous margin', () => {
      answer(useSalesSummary, ok({ ...SALES, prevMarginPct: null }));
      renderRow();

      expect(within(tileOf('Lãi gộp')).getByText(/^Tỷ suất/).textContent).toBe('Tỷ suất 14,5%');
    });

    it('has no change without a margin (no revenue)', () => {
      answer(useSalesSummary, ok({ ...SALES, revenue: 0, grossProfit: 0, marginPct: null }));
      renderRow();

      expect(within(tileOf('Lãi gộp')).getByText(/^Tỷ suất/).textContent).toBe('Tỷ suất —');
    });

    it('is "tạm tính" while the costing period is open', () => {
      answer(useSalesSummary, ok({ ...SALES, isProvisional: true }));
      renderRow();

      expect(within(tileOf('Lãi gộp')).getByText('tạm tính')).toBeInTheDocument();
    });

    it('is not "tạm tính" once the costing period has ended', () => {
      renderRow();

      expect(screen.queryByText('tạm tính')).not.toBeInTheDocument();
    });
  });

  it('without inventory.view_cost the gross profit and the stock value are "—", with no hint and no badge', () => {
    answer(useSalesSummary, ok({
      ...SALES, canViewCost: false, cost: null, grossProfit: null, marginPct: null, prevMarginPct: null, isProvisional: true,
    }));
    answer(useInventorySummary, ok({ ...INVENTORY, canViewCost: false, stockValue: null }));
    renderRow();

    expect(within(tileOf('Lãi gộp')).getByText('—')).toHaveClass('tabular-nums');
    expect(within(tileOf('Giá trị tồn kho')).getByText('—')).toHaveClass('tabular-nums');
    expect(screen.queryByText('tạm tính')).not.toBeInTheDocument();
    expect(screen.queryByText(/Tỷ suất/)).not.toBeInTheDocument();
    // The counts do not need the cost.
    expect(within(tileOf('Giá trị tồn kho')).getByText('318 mã · 3 kho')).toBeInTheDocument();
  });

  it('the cost right decides, not the presence of a figure', () => {
    answer(useSalesSummary, ok({ ...SALES, canViewCost: false }));
    answer(useInventorySummary, ok({ ...INVENTORY, canViewCost: false }));
    renderRow();

    expect(screen.queryByText('412 tr')).not.toBeInTheDocument();
    expect(screen.queryByText('4,62 tỷ')).not.toBeInTheDocument();
    expect(within(tileOf('Lãi gộp')).getByText('—')).toBeInTheDocument();
    expect(within(tileOf('Giá trị tồn kho')).getByText('—')).toBeInTheDocument();
  });

  describe('tones', () => {
    it('an overdue receivable is rose, none is muted', () => {
      const { unmount } = renderRow();
      expect(screen.getByText('Quá hạn 420 tr').className).toContain('rose');
      unmount();

      answer(useDebtSummary, ok({ ...DEBT, receivable: side({ balance: 3_100_000_000 }) }));
      renderRow();
      expect(screen.getByText('Quá hạn 0').className).toContain('muted');
    });

    it('a payable due within 7 days is amber, none is muted', () => {
      const { unmount } = renderRow();
      expect(screen.getByText('Đến hạn 7 ngày: 310 tr').className).toContain('amber');
      unmount();

      answer(useDebtSummary, ok({ ...DEBT, payable: side({ balance: 1_750_000_000 }) }));
      renderRow();
      expect(screen.getByText('Đến hạn 7 ngày: 0').className).toContain('muted');
    });

    it('a negative fund balance is rose, zero is muted', () => {
      answer(useCashSummary, ok({ ...CASH, balance: -5_000_000, cashBalance: -5_000_000, bankBalance: 0 }));
      const { unmount } = renderRow();
      expect(within(tileOf('Tồn quỹ')).getByText('-5 tr')).toBeInTheDocument();
      expect(screen.getByText('TM -5 tr · NH 0').className).toContain('rose');
      unmount();

      answer(useCashSummary, ok({ ...CASH, balance: 0, cashBalance: 0, bankBalance: 0 }));
      renderRow();
      expect(screen.getByText('TM 0 · NH 0').className).toContain('muted');
    });
  });

  describe('permissions', () => {
    it('without the cash domain there is no cash tile and its request is not sent', () => {
      state.access = { ...allDomains(), cash: false, tabs: ['overview', 'sales', 'debt', 'inventory'] };
      renderRow();

      expect(screen.queryByText('Tồn quỹ')).not.toBeInTheDocument();
      expect(screen.queryByText('Thu − chi ròng')).not.toBeInTheDocument();
      expect(useCashSummary).toHaveBeenCalledWith(PARAMS, { enabled: false });
      expect(useSalesSummary).toHaveBeenCalledWith(PARAMS, { enabled: true });
    });

    it.each([
      ['sales', useSalesSummary, ['Doanh thu xuất bán', 'Lãi gộp']],
      ['debt', useDebtSummary, ['Phải thu', 'Phải trả']],
      ['inventory', useInventorySummary, ['Giá trị tồn kho']],
    ] as const)('without the %s domain its tiles are absent and its hook is disabled', (domain, hook, labels) => {
      state.access = { ...allDomains(), [domain]: false };
      renderRow();

      for (const label of labels) expect(screen.queryByText(label)).not.toBeInTheDocument();
      const calls = vi.mocked(hook).mock.calls;
      expect(calls.length).toBeGreaterThan(0);
      expect(calls.every((call) => (call[1] as { enabled: boolean }).enabled === false)).toBe(true);
    });

    it('a group without a tile has no heading', () => {
      state.access = { sales: false, cash: false, debt: true, inventory: false, tabs: ['overview', 'debt'] };
      renderRow();

      expect(screen.queryByRole('group', { name: 'Trong kỳ' })).not.toBeInTheDocument();
      expect(screen.getByRole('group', { name: 'Hiện tại' })).toBeInTheDocument();
    });

    it('renders nothing without any domain', () => {
      state.access = { sales: false, cash: false, debt: false, inventory: false, tabs: ['overview'] };
      const { container } = renderRow();

      expect(container).toBeEmptyDOMElement();
    });
  });

  it('asks for the period figures with the params and for the debt with the branch only', () => {
    renderRow();

    expect(useSalesSummary).toHaveBeenCalledWith(PARAMS, { enabled: true });
    expect(useCashSummary).toHaveBeenCalledWith(PARAMS, { enabled: true });
    expect(useInventorySummary).toHaveBeenCalledWith(PARAMS, { enabled: true });
    expect(useDebtSummary).toHaveBeenCalledWith({ branchId: 'b1' }, { enabled: true });
  });

  describe('links', () => {
    it('the balance tiles open their reports', () => {
      renderRow();

      expect(tileOf('Phải thu')).toHaveAttribute('href', `/reports/debt-overdue?side=Receivable&at=${todayYmd()}`);
      expect(tileOf('Phải trả')).toHaveAttribute('href', '/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-13');
      expect(tileOf('Tồn quỹ')).toHaveAttribute('href', '/reports/cash-book');
      expect(tileOf('Giá trị tồn kho')).toHaveAttribute('href', '/inventory/stock-on-hand');
      for (const label of ['Doanh thu xuất bán', 'Lãi gộp', 'Thu − chi ròng']) {
        expect(tileOf(label).tagName).not.toBe('A');
      }
    });

    it('a report the user may not open is no link', () => {
      signIn(['reports.cash']);
      renderRow();

      expect(tileOf('Tồn quỹ')).toHaveAttribute('href', '/reports/cash-book');
      expect(tileOf('Phải thu').tagName).not.toBe('A');
      expect(tileOf('Giá trị tồn kho').tagName).not.toBe('A');
    });

    it('in company scope with several branches a link says which branch the report opens for', () => {
      state.scope = { ...branchScope(), mode: 'company', branchId: undefined };
      renderRow({ from: '2026-10-01', to: '2026-10-13' });

      for (const label of ['Tồn quỹ', 'Phải thu', 'Phải trả', 'Giá trị tồn kho']) {
        expect(tileOf(label)).toHaveAttribute('title', 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');
      }
    });

    it('in branch scope a link has no hint', () => {
      renderRow();

      expect(tileOf('Phải thu')).not.toHaveAttribute('title');
    });
  });

  describe('loading and errors', () => {
    it('a summary that loads hides the values of its tiles', () => {
      answer(useSalesSummary, ok<SalesSummary>(undefined as unknown as SalesSummary, { data: undefined, isLoading: true }));
      renderRow();

      expect(screen.queryByText('2,84 tỷ')).not.toBeInTheDocument();
      expect(tileOf('Doanh thu xuất bán')).toHaveAttribute('aria-busy', 'true');
      expect(tileOf('Lãi gộp')).toHaveAttribute('aria-busy', 'true');
      expect(tileOf('Tồn quỹ')).not.toHaveAttribute('aria-busy');
    });

    it('a summary in error without data shows "Thử lại" on its tiles only, and retries', () => {
      const query = failed<SalesSummary>();
      answer(useSalesSummary, query);
      renderRow();

      const retries = screen.getAllByRole('button', { name: 'Thử lại' });
      expect(retries).toHaveLength(2);
      expect(within(tileOf('Doanh thu xuất bán')).getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
      expect(within(tileOf('Lãi gộp')).getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
      expect(within(tileOf('Phải thu')).getByText('3,10 tỷ')).toBeInTheDocument();

      fireEvent.click(retries[0]);
      expect(query.refetch).toHaveBeenCalledTimes(1);
    });

    it('a failed refresh keeps the figures it has', () => {
      answer(useDebtSummary, ok(DEBT, { isError: true, error: new Error('timeout') }));
      renderRow();

      expect(within(tileOf('Phải thu')).getByText('3,10 tỷ')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
    });
  });

  // Seven tiles in one row at 1366 px: a label stays on one line so the values line up; a hint wraps under the
  // value, so no figure in it is cut off.
  it('lays seven tiles in one row on wide screens, each label on one line, each hint whole, with the full text as title', () => {
    renderRow();

    const grid = screen.getByRole('group', { name: 'Trong kỳ' }).parentElement;
    // One column that can shrink on a phone, so a long hint never widens the page.
    expect(grid).toHaveClass('grid', 'grid-cols-1', 'sm:grid-cols-2', 'lg:grid-cols-4', 'xl:grid-cols-7');
    for (const label of LABELS) {
      const element = screen.getByText(label);
      expect(element).toHaveClass('truncate');
      expect(element).toHaveAttribute('title', label);
    }
    for (const hint of ['so với kỳ trước', 'Thu 2,10 tỷ · Chi 1,91 tỷ', 'TM 210 tr · NH 1,05 tỷ', 'Quá hạn 420 tr', 'Đến hạn 7 ngày: 310 tr', '318 mã · 3 kho']) {
      const element = screen.getByText(hint);
      expect(element).not.toHaveClass('truncate');
      expect(element.parentElement?.className).not.toMatch(/\b(whitespace-nowrap|truncate|line-clamp-\d)\b/);
      expect(element).toHaveAttribute('title', hint);
    }
    // The margin hint holds an icon, so it is not text: it flows the same way but has no title.
    const margin = screen.getByText(/^Tỷ suất 14,5%/);
    expect(margin).not.toHaveClass('truncate');
    expect(margin.parentElement?.className).not.toMatch(/\b(whitespace-nowrap|truncate|line-clamp-\d)\b/);
  });
});
