import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Branch } from '@/features/branches/types';
import { useBranchComparison, useSalesSeries, useSalesSummary, useSalesTop } from '@/features/executive-dashboard/hooks';
import type {
  BranchComparisonRow,
  ExecutiveParams,
  SalesSeries,
  SalesSummary,
  SalesTopBy,
  SalesTopRow,
  SalesTopSort,
} from '@/features/executive-dashboard/types';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { useAuthStore } from '@/stores/auth-store';
import { SalesTab } from './sales-tab';

vi.mock('@/features/executive-dashboard/hooks', () => ({
  useSalesSummary: vi.fn(),
  useSalesSeries: vi.fn(),
  useSalesTop: vi.fn(),
  useBranchComparison: vi.fn(),
}));

type Scope = ReturnType<typeof useExecutiveScope>;
const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const selectBranch = vi.fn();
const companyScope = (): Scope => ({
  canSwitch: true, mode: 'company', branchId: undefined, workingBranch: B1, branches: [B1, B2],
  selectCompany: vi.fn(), selectBranch,
});
const branchScope = (): Scope => ({ ...companyScope(), mode: 'branch', branchId: 'b1' });
const state: { scope: Scope } = { scope: companyScope() };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({ useExecutiveScope: () => state.scope }));

const SUMMARY: SalesSummary = {
  from: '2026-10-01', to: '2026-10-13', prevFrom: '2026-09-18', prevTo: '2026-09-30',
  revenue: 2_840_000_000, prevRevenue: 2_526_690_000, revenueDeltaPct: 12.4, canViewCost: true,
  cost: 2_428_000_000, grossProfit: 412_000_000, marginPct: 14.5, prevMarginPct: 13.7, isProvisional: false,
};
const NO_COST_SUMMARY: SalesSummary = {
  ...SUMMARY, canViewCost: false, cost: null, grossProfit: null, marginPct: null, prevMarginPct: null,
};
const SERIES: SalesSeries = {
  granularity: 'Day',
  points: [
    { date: '2026-10-01', revenue: 120_000_000, cost: 100_000_000, grossProfit: 20_000_000 },
    { date: '2026-10-02', revenue: 90_000_000, cost: 80_000_000, grossProfit: 10_000_000 },
  ],
};
const CUSTOMERS: SalesTopRow[] = [
  { id: 'c1', code: 'KH001', name: 'CT Xây dựng An Phát', revenue: 412_000_000, grossProfit: 61_000_000 },
  { id: 'c2', code: null, name: 'Khách lẻ', revenue: 356_500_000, grossProfit: 40_250_000 },
];
const PRODUCTS: SalesTopRow[] = [
  { id: 'p1', code: 'SP001', name: 'Tôn lạnh 0,45', revenue: 520_000_000, grossProfit: 70_000_000 },
];
const EMPLOYEES: SalesTopRow[] = [
  { id: 'u1', code: null, name: 'Nguyễn Thị Hoa', revenue: 980_000_000, grossProfit: null },
];
const BRANCHES: BranchComparisonRow[] = [
  { branchId: 'b1', branchCode: 'CN01', branchName: 'Hà Nội', revenue: 1_620_000_000, marginPct: 15.1, receivable: 1_800_000_000, stockValue: 2_710_000_000 },
  { branchId: 'b2', branchCode: 'CN02', branchName: 'Hưng Yên', revenue: 840_000_000, marginPct: 13.2, receivable: 920_000_000, stockValue: 1_360_000_000 },
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

type TopParams = ExecutiveParams & { by: SalesTopBy; sort?: SalesTopSort; limit?: number };
const tops: Record<SalesTopBy, FakeQuery> = { Customer: ok([]), Product: ok([]), Employee: ok([]) };
const topCalls = () =>
  vi.mocked(useSalesTop as unknown as (p: TopParams) => FakeQuery).mock.calls.map((call) => call[0]);
const lastTopCall = (by: SalesTopBy) => topCalls().filter((p) => p.by === by).at(-1);
const lastOptions = (hook: unknown) => {
  const calls = vi.mocked(hook as (...args: unknown[]) => unknown).mock.calls;
  return calls[calls.length - 1]?.[1] as { enabled?: boolean } | undefined;
};
const granularities = () =>
  vi.mocked(useSalesSeries).mock.calls.map((call) => [call[0].from, call[0].granularity] as const);

const MONTH: ExecutiveParams = { from: '2026-10-01', to: '2026-10-13' };
const YEAR: ExecutiveParams = { from: '2026-01-01', to: '2026-10-13' };

function renderTab(params: ExecutiveParams = MONTH) {
  const view = render(
    <MemoryRouter>
      <SalesTab params={params} />
    </MemoryRouter>,
  );
  const rerender = (next: ExecutiveParams) =>
    view.rerender(
      <MemoryRouter>
        <SalesTab params={next} />
      </MemoryRouter>,
    );
  return { ...view, rerender };
}

const tile = (title: string) => screen.getByRole('region', { name: title });
const headers = (title: string) => within(tile(title)).getAllByRole('columnheader').map((h) => h.textContent);
const TITLES = ['Doanh thu – giá vốn – lãi gộp', 'Top khách hàng', 'Top hàng hoá', 'Theo nhân viên lập phiếu', 'So sánh chi nhánh'];
const LISTS = ['Top khách hàng', 'Top hàng hoá', 'Theo nhân viên lập phiếu'];

describe('SalesTab', () => {
  beforeEach(() => {
    state.scope = companyScope();
    selectBranch.mockReset();
    tops.Customer = ok(CUSTOMERS);
    tops.Product = ok(PRODUCTS);
    tops.Employee = ok(EMPLOYEES);
    answer(useSalesSummary, ok(SUMMARY));
    answer(useSalesSeries, ok(SERIES));
    vi.mocked(useSalesTop as unknown as (p: TopParams) => FakeQuery).mockImplementation((p) => tops[p.by]);
    answer(useBranchComparison, ok(BRANCHES));
    useAuthStore.setState({
      user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions: ['reports.profit'] },
    });
  });
  afterEach(() => {
    for (const hook of [useSalesSummary, useSalesSeries, useSalesTop, useBranchComparison]) vi.mocked(hook).mockReset();
    useAuthStore.setState({ user: null });
  });

  it('shows the five tiles in company scope', () => {
    renderTab();

    for (const title of TITLES) expect(tile(title)).toBeInTheDocument();
    expect(lastOptions(useBranchComparison)).toEqual({ enabled: true });
  });

  it('in branch scope there is no branch comparison and its request is not sent', () => {
    state.scope = branchScope();
    renderTab({ ...MONTH, branchId: 'b1' });

    expect(screen.queryByRole('region', { name: 'So sánh chi nhánh' })).not.toBeInTheDocument();
    expect(lastOptions(useBranchComparison)).toEqual({ enabled: false });
    for (const title of TITLES.filter((t) => t !== 'So sánh chi nhánh')) expect(tile(title)).toBeInTheDocument();
  });

  it('a user who cannot switch gets no branch comparison', () => {
    state.scope = { ...companyScope(), canSwitch: false };
    renderTab();

    expect(screen.queryByRole('region', { name: 'So sánh chi nhánh' })).not.toBeInTheDocument();
    expect(lastOptions(useBranchComparison)).toEqual({ enabled: false });
  });

  it('asks each query with the params of the tab', () => {
    renderTab({ ...MONTH, branchId: 'b1' });

    expect(useSalesSummary).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1' });
    expect(useSalesSeries).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1', granularity: 'Day' });
    for (const by of ['Customer', 'Product', 'Employee'] as const) {
      expect(lastTopCall(by)).toEqual({ ...MONTH, branchId: 'b1', by, sort: 'Revenue', limit: 10 });
    }
    expect(useBranchComparison).toHaveBeenLastCalledWith({ from: '2026-10-01', to: '2026-10-13' }, { enabled: true });
  });

  it('the customer and product lists show code – name, the name alone without a code', () => {
    renderTab();

    const customers = tile('Top khách hàng');
    expect(within(customers).getByText('KH001 – CT Xây dựng An Phát')).toBeInTheDocument();
    expect(within(customers).getByText('Khách lẻ')).toBeInTheDocument();
    expect(within(customers).getByText('412.000.000')).toBeInTheDocument();
    expect(within(customers).getByText('61.000.000')).toBeInTheDocument();
    expect(headers('Top khách hàng')).toEqual(['Khách hàng', 'Doanh thu', 'Lãi gộp']);
    expect(within(tile('Top hàng hoá')).getByText('SP001 – Tôn lạnh 0,45')).toBeInTheDocument();
    expect(headers('Top hàng hoá')).toEqual(['Hàng hoá', 'Doanh thu', 'Lãi gộp']);
  });

  it('employee rows show the name; a null gross profit is "—"', () => {
    renderTab();

    const employees = tile('Theo nhân viên lập phiếu');
    expect(headers('Theo nhân viên lập phiếu')).toEqual(['Nhân viên', 'Doanh thu', 'Lãi gộp']);
    const row = within(employees).getByText('Nguyễn Thị Hoa').closest('tr') as HTMLElement;
    expect(within(row).getByText('980.000.000')).toHaveClass('text-right', 'tabular-nums');
    expect(within(row).getByText('—')).toBeInTheDocument();
  });

  it('choosing "Lãi gộp" sorts the three lists by gross profit, "Doanh thu" back by revenue', () => {
    renderTab();

    const toggle = screen.getByRole('group', { name: 'Xếp hạng theo' });
    expect(within(toggle).getByRole('button', { name: 'Doanh thu' })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(within(toggle).getByRole('button', { name: 'Lãi gộp' }));

    expect(within(toggle).getByRole('button', { name: 'Lãi gộp' })).toHaveAttribute('aria-pressed', 'true');
    for (const by of ['Customer', 'Product', 'Employee'] as const) {
      expect(lastTopCall(by)).toEqual({ ...MONTH, by, sort: 'GrossProfit', limit: 10 });
    }

    fireEvent.click(within(toggle).getByRole('button', { name: 'Doanh thu' }));

    for (const by of ['Customer', 'Product', 'Employee'] as const) expect(lastTopCall(by)?.sort).toBe('Revenue');
  });

  it('without canViewCost there is no toggle and no "Lãi gộp" column', () => {
    answer(useSalesSummary, ok(NO_COST_SUMMARY));
    renderTab();

    expect(screen.queryByRole('group', { name: 'Xếp hạng theo' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Lãi gộp' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Lãi gộp' })).not.toBeInTheDocument();
    expect(headers('Top khách hàng')).toEqual(['Khách hàng', 'Doanh thu']);
    expect(within(tile('Top khách hàng')).queryByText('61.000.000')).not.toBeInTheDocument();
    for (const by of ['Customer', 'Product', 'Employee'] as const) expect(lastTopCall(by)?.sort).toBe('Revenue');
  });

  it('the sort goes back to revenue when the cost is no longer visible', () => {
    const { rerender } = renderTab();
    fireEvent.click(screen.getByRole('button', { name: 'Lãi gộp' }));
    expect(lastTopCall('Customer')?.sort).toBe('GrossProfit');

    answer(useSalesSummary, ok(NO_COST_SUMMARY));
    rerender(MONTH);

    for (const by of ['Customer', 'Product', 'Employee'] as const) expect(lastTopCall(by)?.sort).toBe('Revenue');
  });

  it('while the summary loads the lists are by revenue without the gross profit', () => {
    answer(useSalesSummary, ok(undefined, { isLoading: true }));
    renderTab();

    expect(screen.queryByRole('group', { name: 'Xếp hạng theo' })).not.toBeInTheDocument();
    expect(headers('Top khách hàng')).toEqual(['Khách hàng', 'Doanh thu']);
  });

  it('an empty list shows "Chưa có dữ liệu."', () => {
    tops.Product = ok([]);
    answer(useBranchComparison, ok([]));
    renderTab();

    expect(within(tile('Top hàng hoá')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
    expect(within(tile('Top hàng hoá')).queryByRole('table')).not.toBeInTheDocument();
    expect(within(tile('So sánh chi nhánh')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
    expect(within(tile('Top khách hàng')).queryByText('Chưa có dữ liệu.')).not.toBeInTheDocument();
  });

  it('one failing list leaves the other tiles usable', () => {
    const customers = failed();
    tops.Customer = customers;
    renderTab();

    expect(within(tile('Top khách hàng')).getByText('Máy chủ không phản hồi.')).toBeInTheDocument();
    fireEvent.click(within(tile('Top khách hàng')).getByRole('button', { name: 'Thử lại' }));
    expect(customers.refetch).toHaveBeenCalled();
    for (const title of TITLES.filter((t) => t !== 'Top khách hàng')) {
      expect(within(tile(title)).queryByText('Máy chủ không phản hồi.')).not.toBeInTheDocument();
    }
    expect(within(tile('Top hàng hoá')).getByText('SP001 – Tôn lạnh 0,45')).toBeInTheDocument();
  });

  it('a branch name of the comparison selects the branch', () => {
    renderTab();

    fireEvent.click(within(tile('So sánh chi nhánh')).getByRole('button', { name: 'CN02 — Hưng Yên' }));

    expect(selectBranch).toHaveBeenCalledWith('b2');
  });

  describe('the chart', () => {
    it('draws revenue, cost and gross profit 320 px high', () => {
      renderTab();

      const chart = tile('Doanh thu – giá vốn – lãi gộp');
      expect(chart.querySelector('svg.recharts-surface')).not.toBeNull();
      expect(chart.querySelector('[style*="height: 320px"]')).not.toBeNull();
      const legend = Array.from(chart.querySelectorAll('.recharts-legend-item-text')).map((e) => e.textContent);
      expect(legend).toEqual(['Doanh thu', 'Giá vốn', 'Lãi gộp']);
    });

    it('without canViewCost draws the revenue only', () => {
      answer(useSalesSummary, ok(NO_COST_SUMMARY));
      answer(useSalesSeries, ok({ ...SERIES, points: SERIES.points.map((p) => ({ ...p, cost: null, grossProfit: null })) }));
      renderTab();

      const legend = Array.from(
        tile('Doanh thu – giá vốn – lãi gộp').querySelectorAll('.recharts-legend-item-text'),
      ).map((e) => e.textContent);
      expect(legend).toEqual(['Doanh thu']);
    });

    it('is empty without any figure', () => {
      answer(useSalesSeries, ok({ granularity: 'Day', points: [{ date: '2026-10-01', revenue: 0, cost: 0, grossProfit: 0 }] }));
      renderTab();

      expect(within(tile('Doanh thu – giá vốn – lãi gộp')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
    });

    it('the granularity toggle asks for another granularity and goes back to the default when the period changes', () => {
      const { rerender } = renderTab();
      expect(granularities().at(-1)).toEqual(['2026-10-01', 'Day']);

      fireEvent.click(within(tile('Doanh thu – giá vốn – lãi gộp')).getByRole('button', { name: 'Tuần' }));
      expect(granularities().at(-1)).toEqual(['2026-10-01', 'Week']);

      rerender(YEAR);

      expect(granularities().at(-1)).toEqual(['2026-01-01', 'Month']);
      for (const [from, granularity] of granularities()) if (from === '2026-01-01') expect(granularity).toBe('Month');
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

  it('every list tile is a table with right-aligned numbers', () => {
    renderTab();

    for (const title of LISTS) {
      const numeric = within(tile(title)).getAllByRole('columnheader').slice(1);
      for (const header of numeric) expect(header).toHaveClass('text-right', 'tabular-nums');
    }
  });
});
