import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Branch } from '@/features/branches/types';
import {
  useAttention,
  useBranchComparison,
  useCashSeries,
  useCashSummary,
  useDebtSummary,
  useInventorySummary,
  useRecentDocuments,
  useSalesSeries,
  useSalesSummary,
  useSalesTop,
} from '@/features/executive-dashboard/hooks';
import type {
  Attention,
  BranchComparisonRow,
  CashSeries,
  CashSummary,
  DebtSummary,
  ExecutiveParams,
  ExecutiveTab,
  InventorySummary,
  RecentDocument,
  SalesSeries,
  SalesSummary,
  SalesTopRow,
} from '@/features/executive-dashboard/types';
import type { useExecutiveAccess } from '@/features/executive-dashboard/use-executive-access';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { todayYmd } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import { OverviewTab } from './overview-tab';

vi.mock('@/features/executive-dashboard/hooks', () => ({
  useSalesSummary: vi.fn(),
  useSalesSeries: vi.fn(),
  useSalesTop: vi.fn(),
  useCashSummary: vi.fn(),
  useCashSeries: vi.fn(),
  useDebtSummary: vi.fn(),
  useInventorySummary: vi.fn(),
  useBranchComparison: vi.fn(),
  useAttention: vi.fn(),
  useRecentDocuments: vi.fn(),
}));

type Scope = ReturnType<typeof useExecutiveScope>;
type Access = ReturnType<typeof useExecutiveAccess>;
const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const B3: Branch = { id: 'b3', code: 'CN03', name: 'Đà Nẵng' };
const selectBranch = vi.fn();
const companyScope = (): Scope => ({
  canSwitch: true, mode: 'company', branchId: undefined, workingBranch: B1, branches: [B1, B2, B3],
  selectCompany: vi.fn(), selectBranch,
});
const branchScope = (): Scope => ({ ...companyScope(), mode: 'branch', branchId: 'b1' });
const allDomains = (): Access => ({
  sales: true, cash: true, debt: true, inventory: true, tabs: ['overview', 'sales', 'cash', 'debt', 'inventory'],
});
const state: { scope: Scope; access: Access } = { scope: companyScope(), access: allDomains() };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({ useExecutiveScope: () => state.scope }));
vi.mock('@/features/executive-dashboard/use-executive-access', () => ({ useExecutiveAccess: () => state.access }));

const SALES_SUMMARY: SalesSummary = {
  from: '2026-10-01', to: '2026-10-13', prevFrom: '2026-09-18', prevTo: '2026-09-30',
  revenue: 2_840_000_000, prevRevenue: 2_526_690_000, revenueDeltaPct: 12.4, canViewCost: true,
  cost: 2_428_000_000, grossProfit: 412_000_000, marginPct: 14.5, prevMarginPct: 13.7, isProvisional: false,
};
const SALES_SERIES: SalesSeries = {
  granularity: 'Day',
  points: [
    { date: '2026-10-01', revenue: 120_000_000, cost: 100_000_000, grossProfit: 20_000_000 },
    { date: '2026-10-02', revenue: 90_000_000, cost: 80_000_000, grossProfit: 10_000_000 },
  ],
};
const TOP: SalesTopRow[] = [
  { id: 'c1', code: 'KH001', name: 'CT Xây dựng An Phát', revenue: 412_000_000, grossProfit: 61_000_000 },
  { id: 'c2', code: 'KH002', name: 'Cơ điện lạnh Minh Long', revenue: 356_500_000, grossProfit: null },
];
const CASH_SUMMARY: CashSummary = {
  from: '2026-10-01', to: '2026-10-13', prevFrom: '2026-09-18', prevTo: '2026-09-30', at: '2026-10-13',
  receipts: 2_100_000_000, payments: 1_914_000_000, net: 186_000_000, prevNet: 150_000_000, netDeltaPct: 24,
  balance: 1_260_000_000, cashBalance: 210_000_000, bankBalance: 1_050_000_000,
  funds: [
    { fundId: 'f1', branchId: 'b1', branchCode: 'CN01', code: 'QTM', name: 'Tiền mặt', kind: 'Cash', isActive: true, balance: 210_400_000 },
    { fundId: 'f2', branchId: 'b1', branchCode: 'CN01', code: 'VCB', name: 'Vietcombank', kind: 'Bank', isActive: true, balance: 820_150_000 },
  ],
};
const CASH_SERIES: CashSeries = {
  granularity: 'Day',
  points: [
    { date: '2026-10-01', receipts: 45_000_000, payments: 12_000_000 },
    { date: '2026-10-02', receipts: 98_000_000, payments: 30_000_000 },
  ],
};
const DEBT: DebtSummary = {
  at: '2026-10-13',
  overdueBuckets: [{ from: 1, to: 30 }, { from: 31, to: 60 }, { from: 61, to: 90 }, { from: 91, to: null }],
  receivable: {
    balance: 3_100_000_000, totalOpen: 3_100_000_000, unallocated: 0, notDue: 1_798_000_000, overdue: 1_302_000_000,
    overduePartnerCount: 12, overdueAmounts: [620_000_000, 403_000_000, 279_000_000, 0], dueSoon: 0, dueSoonCount: 0,
  },
  payable: {
    balance: 1_750_000_000, totalOpen: 1_750_000_000, unallocated: 0, notDue: 1_750_000_000, overdue: 0,
    overduePartnerCount: 0, overdueAmounts: [0, 0, 0, 0], dueSoon: 310_000_000, dueSoonCount: 4,
  },
};
const INVENTORY: InventorySummary = {
  from: '2026-10-01', to: '2026-10-13', canViewCost: true, stockValue: 4_620_000_000,
  productCount: 318, warehouseCount: 3, negativeCount: 5, inValue: 0, outCost: 0,
};
const BRANCHES: BranchComparisonRow[] = [
  { branchId: 'b1', branchCode: 'CN01', branchName: 'Hà Nội', revenue: 1_620_000_000, marginPct: 15.1, receivable: 1_800_000_000, stockValue: 2_710_000_000 },
  { branchId: 'b2', branchCode: 'CN02', branchName: 'Hưng Yên', revenue: 840_000_000, marginPct: 13.2, receivable: 920_000_000, stockValue: 1_360_000_000 },
];
const ATTENTION: Attention = {
  at: '2026-10-13',
  overdueReceivables: { count: 12, amount: 420_000_000 },
  overCreditLimit: { count: 3, amount: 96_000_000 },
  negativeStock: { count: 5, amount: null },
  payablesDueSoon: { count: 4, amount: 310_000_000 },
  negativeFunds: { count: 0, amount: 0 },
};
const RECENT: RecentDocument[] = [
  { kind: 'Receipt', id: 'r1', code: 'PT00124', createdAt: new Date(2026, 9, 14, 14, 31).toISOString(), branchCode: 'CN01', partnerName: 'An Phát', ownerName: 'N.T.Hoa', amount: 45_000_000 },
];

interface FakeQuery {
  data: unknown;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
  isPlaceholderData: boolean;
  refetch: ReturnType<typeof vi.fn>;
}
const ok = (data: unknown, over: Partial<FakeQuery> = {}): FakeQuery => ({
  data, isLoading: false, isError: false, error: null, isPlaceholderData: false, refetch: vi.fn(), ...over,
});
const failed = (): FakeQuery => ok(undefined, { isError: true, error: new Error('Máy chủ không phản hồi.') });
const answer = (hook: unknown, query: FakeQuery) =>
  vi.mocked(hook as (...args: unknown[]) => unknown).mockReturnValue(query);

const HOOKS = [
  useSalesSummary, useSalesSeries, useSalesTop, useCashSummary, useCashSeries, useDebtSummary, useInventorySummary,
  useBranchComparison, useAttention, useRecentDocuments,
];
const lastOptions = (hook: unknown) => {
  const calls = vi.mocked(hook as (...args: unknown[]) => unknown).mock.calls;
  return calls[calls.length - 1]?.[1] as { enabled?: boolean } | undefined;
};
const granularities = (hook: unknown) =>
  vi.mocked(hook as (...args: unknown[]) => unknown).mock.calls.map(
    (call) => [(call[0] as { from: string }).from, (call[0] as { granularity: string }).granularity] as const,
  );

const MONTH: ExecutiveParams = { from: '2026-10-01', to: '2026-10-13' };
const YEAR: ExecutiveParams = { from: '2026-01-01', to: '2026-10-13' };

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderTab(params: ExecutiveParams = MONTH, openTabs: ExecutiveTab[] = [], onOpenTab = vi.fn()) {
  const view = render(
    <MemoryRouter>
      <OverviewTab params={params} openTabs={openTabs} onOpenTab={onOpenTab} />
    </MemoryRouter>,
  );
  const rerender = (next: ExecutiveParams) =>
    view.rerender(
      <MemoryRouter>
        <OverviewTab params={next} openTabs={openTabs} onOpenTab={onOpenTab} />
      </MemoryRouter>,
    );
  return { ...view, rerender, onOpenTab };
}

const tile = (title: string) => screen.getByRole('region', { name: title });
const TITLES = [
  'Doanh thu – giá vốn – lãi gộp',
  'Dòng tiền thu – chi',
  'Cơ cấu nợ phải thu',
  'So sánh chi nhánh',
  'Top khách hàng theo doanh thu',
  'Cần chú ý',
  'Vừa phát sinh',
];

describe('OverviewTab', () => {
  beforeEach(() => {
    state.scope = companyScope();
    state.access = allDomains();
    selectBranch.mockReset();
    answer(useSalesSummary, ok(SALES_SUMMARY));
    answer(useSalesSeries, ok(SALES_SERIES));
    answer(useSalesTop, ok(TOP));
    answer(useCashSummary, ok(CASH_SUMMARY));
    answer(useCashSeries, ok(CASH_SERIES));
    answer(useDebtSummary, ok(DEBT));
    answer(useInventorySummary, ok(INVENTORY));
    answer(useBranchComparison, ok(BRANCHES));
    answer(useAttention, ok(ATTENTION));
    answer(useRecentDocuments, ok(RECENT));
    signIn(['reports.profit', 'reports.cash', 'reports.debt', 'reports.inventory', 'receipts.view', 'credit_limit.manage']);
  });
  afterEach(() => {
    for (const hook of HOOKS) vi.mocked(hook).mockReset();
    useAuthStore.setState({ user: null });
  });

  it('shows the KPI row and every tile with every domain in company scope', () => {
    renderTab();

    expect(screen.getByText('Doanh thu xuất bán')).toBeInTheDocument();
    expect(screen.getByText('Tồn quỹ')).toBeInTheDocument();
    for (const title of TITLES) expect(tile(title)).toBeInTheDocument();
  });

  it('fills the tiles from their queries', () => {
    renderTab();

    expect(tile('Doanh thu – giá vốn – lãi gộp').querySelector('svg.recharts-surface')).not.toBeNull();
    expect(within(tile('Dòng tiền thu – chi')).getByText('CN01 · VCB – Vietcombank')).toBeInTheDocument();
    expect(within(tile('Cơ cấu nợ phải thu')).getByText('Quá hạn 1–30 ngày')).toBeInTheDocument();
    expect(within(tile('So sánh chi nhánh')).getByRole('button', { name: 'CN02 — Hưng Yên' })).toBeInTheDocument();
    expect(within(tile('Top khách hàng theo doanh thu')).getByText('CT Xây dựng An Phát')).toBeInTheDocument();
    expect(within(tile('Top khách hàng theo doanh thu')).getByText('LG 61 tr')).toBeInTheDocument();
    expect(within(tile('Cần chú ý')).getByText('Khách nợ quá hạn')).toBeInTheDocument();
    expect(within(tile('Vừa phát sinh')).getByText('CN01 · An Phát · N.T.Hoa · 14:31')).toBeInTheDocument();
  });

  it('asks each query with the params of the tab', () => {
    renderTab({ ...MONTH, branchId: 'b1' });

    expect(useSalesSeries).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1', granularity: 'Day' }, { enabled: true });
    expect(useCashSeries).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1', granularity: 'Day' }, { enabled: true });
    expect(useSalesTop).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1', by: 'Customer', limit: 5 }, { enabled: true });
    expect(useDebtSummary).toHaveBeenLastCalledWith({ branchId: 'b1' }, { enabled: true });
    expect(useAttention).toHaveBeenLastCalledWith({ branchId: 'b1' });
    expect(useRecentDocuments).toHaveBeenLastCalledWith({ branchId: 'b1', limit: 10 });
    expect(useBranchComparison).toHaveBeenLastCalledWith({ from: '2026-10-01', to: '2026-10-13' }, { enabled: true });
  });

  describe('the branch comparison', () => {
    it('is absent in branch scope and its request is not sent', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(screen.queryByRole('region', { name: 'So sánh chi nhánh' })).not.toBeInTheDocument();
      expect(lastOptions(useBranchComparison)).toEqual({ enabled: false });
    });

    it('is absent for a user who cannot switch and its request is not sent', () => {
      state.scope = { ...companyScope(), canSwitch: false, mode: 'branch', branches: [B1] };
      renderTab();

      expect(screen.queryByRole('region', { name: 'So sánh chi nhánh' })).not.toBeInTheDocument();
      expect(lastOptions(useBranchComparison)).toEqual({ enabled: false });
    });

    // useExecutiveScope never reports this pair; the tile still asks for both.
    it('needs the right to switch, not only the company mode', () => {
      state.scope = { ...companyScope(), canSwitch: false };
      renderTab();

      expect(screen.queryByRole('region', { name: 'So sánh chi nhánh' })).not.toBeInTheDocument();
      expect(lastOptions(useBranchComparison)).toEqual({ enabled: false });
    });

    it('a branch name selects the branch', () => {
      renderTab();

      fireEvent.click(within(tile('So sánh chi nhánh')).getByRole('button', { name: 'CN02 — Hưng Yên' }));

      expect(selectBranch).toHaveBeenCalledWith('b2');
    });
  });

  describe('permissions', () => {
    it('without the debt domain there is no receivable tile and no debt request', () => {
      state.access = { ...allDomains(), debt: false, tabs: ['overview', 'sales', 'cash', 'inventory'] };
      renderTab();

      expect(screen.queryByRole('region', { name: 'Cơ cấu nợ phải thu' })).not.toBeInTheDocument();
      expect(vi.mocked(useDebtSummary).mock.calls.every((call) => call[1]?.enabled === false)).toBe(true);
    });

    it('without the sales domain there is no sales tile and no sales request', () => {
      state.access = { ...allDomains(), sales: false, tabs: ['overview', 'cash', 'debt', 'inventory'] };
      renderTab();

      expect(screen.queryByRole('region', { name: 'Doanh thu – giá vốn – lãi gộp' })).not.toBeInTheDocument();
      expect(screen.queryByRole('region', { name: 'Top khách hàng theo doanh thu' })).not.toBeInTheDocument();
      expect(lastOptions(useSalesSeries)).toEqual({ enabled: false });
      expect(lastOptions(useSalesTop)).toEqual({ enabled: false });
    });

    it('without the cash domain there is no cash flow tile and no cash request', () => {
      state.access = { ...allDomains(), cash: false, tabs: ['overview', 'sales', 'debt', 'inventory'] };
      renderTab();

      expect(screen.queryByRole('region', { name: 'Dòng tiền thu – chi' })).not.toBeInTheDocument();
      expect(lastOptions(useCashSeries)).toEqual({ enabled: false });
      expect(vi.mocked(useCashSummary).mock.calls.every((call) => call[1]?.enabled === false)).toBe(true);
    });

    it('"Cần chú ý" and "Vừa phát sinh" are there without any domain', () => {
      state.access = { sales: false, cash: false, debt: false, inventory: false, tabs: ['overview'] };
      renderTab();

      expect(tile('Cần chú ý')).toBeInTheDocument();
      expect(tile('Vừa phát sinh')).toBeInTheDocument();
    });
  });

  it('one failing request leaves the other tiles usable', () => {
    const debt = failed();
    answer(useDebtSummary, debt);
    renderTab();

    const receivable = tile('Cơ cấu nợ phải thu');
    expect(within(receivable).getByText('Máy chủ không phản hồi.')).toBeInTheDocument();
    fireEvent.click(within(receivable).getByRole('button', { name: 'Thử lại' }));
    expect(debt.refetch).toHaveBeenCalled();
    for (const title of TITLES.filter((t) => t !== 'Cơ cấu nợ phải thu')) {
      expect(within(tile(title)).queryByText('Máy chủ không phản hồi.')).not.toBeInTheDocument();
    }
    expect(within(tile('Cần chú ý')).getByText('Khách nợ quá hạn')).toBeInTheDocument();
    expect(screen.getByText('1,26 tỷ')).toBeInTheDocument();
  });

  describe('the cash flow tile reads two queries', () => {
    it('is dimmed while the fund balances of the previous key are on screen', () => {
      answer(useCashSummary, ok(CASH_SUMMARY, { isPlaceholderData: true }));
      renderTab();

      expect(tile('Dòng tiền thu – chi')).toHaveAttribute('aria-busy', 'true');
      expect(tile('Cơ cấu nợ phải thu')).not.toHaveAttribute('aria-busy');
    });

    it('is not dimmed when both are fresh', () => {
      renderTab();

      expect(tile('Dòng tiền thu – chi')).not.toHaveAttribute('aria-busy');
    });

    it('a failed fund summary shows its error under the chart, with a retry', () => {
      const summary = failed();
      answer(useCashSummary, summary);
      renderTab();

      const cash = tile('Dòng tiền thu – chi');
      expect(cash.querySelector('svg.recharts-surface')).not.toBeNull();
      expect(within(cash).getByText('Máy chủ không phản hồi.')).toBeInTheDocument();
      fireEvent.click(within(cash).getByRole('button', { name: 'Thử lại' }));
      expect(summary.refetch).toHaveBeenCalled();
    });

    it('a failed fund summary is shown also when the period has no movement', () => {
      answer(useCashSeries, ok({ granularity: 'Day', points: [] }));
      answer(useCashSummary, failed());
      renderTab();

      expect(within(tile('Dòng tiền thu – chi')).getByText('Máy chủ không phản hồi.')).toBeInTheDocument();
    });

    it('a failed refresh of the fund summary keeps the funds and shows no error', () => {
      answer(useCashSummary, ok(CASH_SUMMARY, { isError: true, error: new Error('Máy chủ không phản hồi.') }));
      renderTab();

      const cash = tile('Dòng tiền thu – chi');
      expect(within(cash).getByText('CN01 · QTM – Tiền mặt')).toBeInTheDocument();
      expect(within(cash).queryByText('Máy chủ không phản hồi.')).not.toBeInTheDocument();
    });
  });

  describe('report links', () => {
    it('in company scope with three branches "Sổ quỹ →" says which branch the report opens for', () => {
      renderTab();

      const link = within(tile('Dòng tiền thu – chi')).getByRole('link', { name: 'Sổ quỹ →' });
      expect(link).toHaveAttribute('href', '/reports/cash-book');
      expect(link).toHaveAttribute('title', 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');
      expect(within(tile('Cơ cấu nợ phải thu')).getByRole('link', { name: 'Nợ quá hạn →' })).toHaveAttribute(
        'title',
        'Báo cáo mở theo chi nhánh đang làm việc (CN01)',
      );
      expect(within(tile('Cần chú ý')).getAllByRole('link')[0]).toHaveAttribute(
        'title',
        'Báo cáo mở theo chi nhánh đang làm việc (CN01)',
      );
    });

    it('in branch scope they have no hint', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(within(tile('Dòng tiền thu – chi')).getByRole('link', { name: 'Sổ quỹ →' })).not.toHaveAttribute('title');
      expect(within(tile('Cần chú ý')).getAllByRole('link')[0]).not.toHaveAttribute('title');
    });

    it('"Nợ quá hạn →" opens the overdue receivables as at today', () => {
      renderTab();

      expect(within(tile('Cơ cấu nợ phải thu')).getByRole('link', { name: 'Nợ quá hạn →' })).toHaveAttribute(
        'href',
        `/reports/debt-overdue?side=Receivable&at=${todayYmd()}`,
      );
    });

    it('a document of the working branch is a link in "Vừa phát sinh"', () => {
      renderTab();

      expect(within(tile('Vừa phát sinh')).getByRole('link', { name: 'PT00124' })).toHaveAttribute('href', '/receipts/r1');
    });

    it('"Vừa phát sinh" leaves the branch code out in branch scope', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(within(tile('Vừa phát sinh')).getByText('An Phát · N.T.Hoa · 14:31')).toBeInTheDocument();
    });
  });

  describe('granularity', () => {
    it('both charts use the same granularity, by day for a month', () => {
      renderTab();

      expect(granularities(useSalesSeries).at(-1)).toEqual(['2026-10-01', 'Day']);
      expect(granularities(useCashSeries).at(-1)).toEqual(['2026-10-01', 'Day']);
    });

    it('goes back to the default of the period when the period changes', () => {
      const { rerender } = renderTab();
      fireEvent.click(within(tile('Doanh thu – giá vốn – lãi gộp')).getByRole('button', { name: 'Tuần' }));
      expect(granularities(useSalesSeries).at(-1)).toEqual(['2026-10-01', 'Week']);
      expect(granularities(useCashSeries).at(-1)).toEqual(['2026-10-01', 'Week']);

      rerender(YEAR);

      expect(granularities(useSalesSeries).at(-1)).toEqual(['2026-01-01', 'Month']);
      expect(granularities(useCashSeries).at(-1)).toEqual(['2026-01-01', 'Month']);
      // Never asked for the new period with the granularity of the old one.
      for (const [from, granularity] of [...granularities(useSalesSeries), ...granularities(useCashSeries)]) {
        if (from === '2026-01-01') expect(granularity).toBe('Month');
      }
    });

    it('the toggle is on the profit chart only', () => {
      renderTab();

      expect(within(tile('Doanh thu – giá vốn – lãi gộp')).getByRole('button', { name: 'Ngày' })).toBeInTheDocument();
      expect(within(tile('Dòng tiền thu – chi')).queryByRole('button', { name: 'Ngày' })).not.toBeInTheDocument();
    });

    it('without the sales domain the cash chart has the toggle', () => {
      state.access = { ...allDomains(), sales: false, tabs: ['overview', 'cash', 'debt', 'inventory'] };
      renderTab();

      fireEvent.click(within(tile('Dòng tiền thu – chi')).getByRole('button', { name: 'Tháng' }));

      expect(granularities(useCashSeries).at(-1)).toEqual(['2026-10-01', 'Month']);
    });

    it.each([
      ['has no figure', ok({ granularity: 'Day', points: [{ date: '2026-10-01', revenue: 0, cost: 0, grossProfit: 0 }] })],
      ['loads', ok(undefined, { isLoading: true })],
      ['failed', failed()],
    ])('when the profit chart %s the cash chart has the toggle', (_case, sales) => {
      answer(useSalesSeries, sales);
      renderTab();

      expect(within(tile('Doanh thu – giá vốn – lãi gộp')).queryByRole('button', { name: 'Ngày' })).not.toBeInTheDocument();
      fireEvent.click(within(tile('Dòng tiền thu – chi')).getByRole('button', { name: 'Tháng' }));

      expect(granularities(useCashSeries).at(-1)).toEqual(['2026-10-01', 'Month']);
    });

    it('"Ngày" is disabled for a period longer than 366 days', () => {
      renderTab({ from: '2025-01-01', to: '2026-10-13' });

      expect(within(tile('Doanh thu – giá vốn – lãi gộp')).getByRole('button', { name: 'Ngày' })).toBeDisabled();
    });

    it('"Ngày" is allowed for a year', () => {
      renderTab(YEAR);

      expect(within(tile('Doanh thu – giá vốn – lãi gộp')).getByRole('button', { name: 'Ngày' })).toBeEnabled();
    });
  });

  describe('empty tiles', () => {
    it('a profit chart without any figure is empty', () => {
      answer(useSalesSeries, ok({ granularity: 'Day', points: [{ date: '2026-10-01', revenue: 0, cost: 0, grossProfit: 0 }] }));
      renderTab();

      expect(within(tile('Doanh thu – giá vốn – lãi gộp')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(tile('Doanh thu – giá vốn – lãi gộp').querySelector('svg.recharts-surface')).toBeNull();
    });

    it('a profit chart with a figure is not empty', () => {
      answer(useSalesSeries, ok({ granularity: 'Day', points: [{ date: '2026-10-01', revenue: 0, cost: 0, grossProfit: -5 }] }));
      renderTab();

      expect(within(tile('Doanh thu – giá vốn – lãi gộp')).queryByText('Chưa có dữ liệu.')).not.toBeInTheDocument();
    });

    it('a profit chart without cost figures draws the revenue only', () => {
      answer(useSalesSeries, ok({
        granularity: 'Day',
        points: SALES_SERIES.points.map((p) => ({ ...p, cost: null, grossProfit: null })),
      }));
      renderTab();

      const legend = Array.from(
        tile('Doanh thu – giá vốn – lãi gộp').querySelectorAll('.recharts-legend-item-text'),
      ).map((e) => e.textContent);
      expect(legend).toEqual(['Doanh thu']);
    });

    it('payments alone are a cash movement', () => {
      answer(useCashSeries, ok({ granularity: 'Day', points: [{ date: '2026-10-01', receipts: 0, payments: 700_000 }] }));
      renderTab();

      const cash = tile('Dòng tiền thu – chi');
      expect(cash.querySelector('svg.recharts-surface')).not.toBeNull();
      expect(within(cash).queryByText('Chưa có dữ liệu.')).not.toBeInTheDocument();
    });

    it('a period without cash movement still shows the fund balances', () => {
      answer(useCashSeries, ok({ granularity: 'Day', points: [{ date: '2026-10-01', receipts: 0, payments: 0 }] }));
      renderTab();

      const cash = tile('Dòng tiền thu – chi');
      expect(within(cash).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(cash.querySelector('svg.recharts-surface')).toBeNull();
      expect(within(cash).getByText('CN01 · QTM – Tiền mặt')).toBeInTheDocument();
    });

    it('no cash movement and no fund: the tile is empty', () => {
      answer(useCashSeries, ok({ granularity: 'Day', points: [] }));
      answer(useCashSummary, ok({ ...CASH_SUMMARY, funds: [] }));
      renderTab();

      const cash = tile('Dòng tiền thu – chi');
      expect(within(cash).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(within(cash).queryByRole('table')).not.toBeInTheDocument();
    });

    it('the fund table shows the five largest funds', () => {
      const funds = Array.from({ length: 7 }, (_, i) => ({
        ...CASH_SUMMARY.funds[0], fundId: `f${i}`, code: `Q${i}`, balance: (i + 1) * 1_000,
      }));
      answer(useCashSummary, ok({ ...CASH_SUMMARY, funds }));
      renderTab();

      const rows = within(tile('Dòng tiền thu – chi')).getAllByRole('row');
      expect(rows).toHaveLength(6);
      expect(within(tile('Dòng tiền thu – chi')).queryByText('CN01 · Q0 – Tiền mặt')).not.toBeInTheDocument();
    });

    it('in branch scope the fund table has no branch code', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(within(tile('Dòng tiền thu – chi')).getByText('QTM – Tiền mặt')).toBeInTheDocument();
    });

    it('lists without rows are empty', () => {
      answer(useSalesTop, ok([]));
      answer(useBranchComparison, ok([]));
      answer(useRecentDocuments, ok([]));
      renderTab();

      expect(within(tile('Top khách hàng theo doanh thu')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(within(tile('So sánh chi nhánh')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(within(tile('Vừa phát sinh')).getByText('Hôm nay chưa có chứng từ.')).toBeInTheDocument();
    });
  });

  describe('the "→" buttons', () => {
    it('open the Kinh doanh tab when it is on the bar', () => {
      const onOpenTab = vi.fn();
      renderTab(MONTH, ['sales'], onOpenTab);

      for (const label of ['Chi tiết →', 'Kinh doanh →', 'Xem thêm →']) {
        fireEvent.click(screen.getByRole('button', { name: label }));
      }

      expect(onOpenTab).toHaveBeenCalledTimes(3);
      for (const call of onOpenTab.mock.calls) expect(call).toEqual(['sales']);
    });

    it('are absent when the Kinh doanh tab is not on the bar', () => {
      renderTab(MONTH, []);

      for (const label of ['Chi tiết →', 'Kinh doanh →', 'Xem thêm →']) {
        expect(screen.queryByRole('button', { name: label })).not.toBeInTheDocument();
      }
    });

    it('are absent when only other tabs are on the bar', () => {
      renderTab(MONTH, ['cash', 'debt']);

      expect(screen.queryByRole('button', { name: 'Chi tiết →' })).not.toBeInTheDocument();
    });
  });
});
