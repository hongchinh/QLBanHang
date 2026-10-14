import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Branch } from '@/features/branches/types';
import { useCashBreakdown, useCashSeries, useCashSummary } from '@/features/executive-dashboard/hooks';
import type {
  CashBreakdown,
  CashSeries,
  CashSummary,
  ExecutiveParams,
} from '@/features/executive-dashboard/types';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { useAuthStore } from '@/stores/auth-store';
import { CashTab } from './cash-tab';

vi.mock('@/features/executive-dashboard/hooks', () => ({
  useCashSummary: vi.fn(),
  useCashSeries: vi.fn(),
  useCashBreakdown: vi.fn(),
}));

type Scope = ReturnType<typeof useExecutiveScope>;
const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const B3: Branch = { id: 'b3', code: 'CN03', name: 'Đà Nẵng' };
const companyScope = (): Scope => ({
  canSwitch: true, mode: 'company', branchId: undefined, workingBranch: B1, branches: [B1, B2, B3],
  selectCompany: vi.fn(), selectBranch: vi.fn(), unknown: false, retry: vi.fn(),
});
const branchScope = (): Scope => ({ ...companyScope(), mode: 'branch', branchId: 'b1' });
const state: { scope: Scope } = { scope: companyScope() };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({ useExecutiveScope: () => state.scope }));

const SUMMARY: CashSummary = {
  from: '2026-10-01', to: '2026-10-13', prevFrom: '2026-09-18', prevTo: '2026-09-30', at: '2026-10-13',
  receipts: 2_100_000_000, payments: 1_914_000_000, net: 186_000_000, prevNet: 150_000_000, netDeltaPct: 24,
  balance: 1_260_000_000, cashBalance: 210_000_000, bankBalance: 1_050_000_000,
  funds: [
    { fundId: 'f1', branchId: 'b1', branchCode: 'CN01', code: 'QTM', name: 'Tiền mặt', kind: 'Cash', isActive: true, balance: 210_000_000 },
    { fundId: 'f2', branchId: 'b2', branchCode: 'CN02', code: 'VCB', name: 'Vietcombank', kind: 'Bank', isActive: true, balance: 1_050_000_000 },
  ],
};
const SERIES: CashSeries = {
  granularity: 'Day',
  points: [
    { date: '2026-10-01', receipts: 45_000_000, payments: 12_000_000 },
    { date: '2026-10-02', receipts: 98_000_000, payments: 30_000_000 },
  ],
};
const at = (day: number, hour: number, minute: number) => new Date(2026, 9, day, hour, minute).toISOString();
const BREAKDOWN: CashBreakdown = {
  byReason: [
    { reasonId: 'r1', reasonCode: 'TKH', reasonName: 'Thu tiền khách hàng', type: 'Receipt', total: 1_800_000_000, count: 42 },
    { reasonId: 'r2', reasonCode: 'TPX', reasonName: null, type: 'Receipt', total: 300_000_000, count: 7 },
    { reasonId: 'r3', reasonCode: 'TNCC', reasonName: 'Trả nhà cung cấp', type: 'Payment', total: 1_500_000_000, count: 18 },
    { reasonId: 'r4', reasonCode: 'CK', reasonName: 'Chi khác', type: 'Payment', total: 414_000_000, count: 9 },
  ],
  largest: [
    { id: 'rc1', type: 'Receipt', code: 'PT00124', at: at(5, 9, 30), branchCode: 'CN01', partnerOrPerson: 'CT Xây dựng An Phát', description: 'Thu tiền hàng tháng 9 theo hợp đồng số 12', total: 450_000_000 },
    { id: 'pm1', type: 'Payment', code: 'PC00077', at: at(7, 14, 5), branchCode: 'CN01', partnerOrPerson: 'Thép Hoà Phát', description: null, total: 380_000_000 },
    { id: 'rc2', type: 'Receipt', code: 'PT00090', at: at(3, 8, 0), branchCode: 'CN02', partnerOrPerson: null, description: 'Thu khác', total: 120_000_000 },
  ],
};

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
const granularities = () =>
  vi.mocked(useCashSeries).mock.calls.map((call) => [call[0].from, call[0].granularity] as const);

const MONTH: ExecutiveParams = { from: '2026-10-01', to: '2026-10-13' };
const YEAR: ExecutiveParams = { from: '2026-01-01', to: '2026-10-13' };

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderTab(params: ExecutiveParams = MONTH) {
  const view = render(
    <MemoryRouter>
      <CashTab params={params} />
    </MemoryRouter>,
  );
  const rerender = (next: ExecutiveParams) =>
    view.rerender(
      <MemoryRouter>
        <CashTab params={next} />
      </MemoryRouter>,
    );
  return { ...view, rerender };
}

const tile = (title: string) => screen.getByRole('region', { name: title });
const xTicks = (element: HTMLElement) =>
  Array.from(element.querySelectorAll('.recharts-xAxis .recharts-cartesian-axis-tick-value')).map((t) => t.textContent);
const headers = (title: string) => within(tile(title)).getAllByRole('columnheader').map((h) => h.textContent);
const bodyRows = (title: string) => within(within(tile(title)).getAllByRole('rowgroup')[1]).getAllByRole('row');
const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((c) => c.textContent);
const rowOf = (title: string, text: string) => {
  const row = within(tile(title)).getByText(text).closest('tr');
  if (!row) throw new Error(`no row for ${text}`);
  return row;
};
const TITLES = ['Số dư các quỹ', 'Dòng tiền thu – chi', 'Thu theo lý do', 'Chi theo lý do', 'Khoản thu chi lớn nhất'];
const LARGEST = 'Khoản thu chi lớn nhất';

describe('CashTab', () => {
  beforeEach(() => {
    state.scope = companyScope();
    answer(useCashSummary, ok(SUMMARY));
    answer(useCashSeries, ok(SERIES));
    answer(useCashBreakdown, ok(BREAKDOWN));
    signIn(['reports.cash', 'receipts.view', 'payments.view']);
  });
  afterEach(() => {
    for (const hook of [useCashSummary, useCashSeries, useCashBreakdown]) vi.mocked(hook).mockReset();
    useAuthStore.setState({ user: null });
  });

  it('shows the five tiles', () => {
    renderTab();

    for (const title of TITLES) expect(tile(title)).toBeInTheDocument();
  });

  // Below lg the tiles share one column that can shrink, so the widest table scrolls inside its own tile
  // instead of making every tile wider than the screen.
  it('lays the tiles in one shrinkable column below lg, two from lg, and a wide table scrolls in its tile', () => {
    renderTab();

    const grid = tile('Số dư các quỹ').parentElement;
    expect(grid).toHaveClass('grid', 'grid-cols-1', 'lg:grid-cols-2');
    for (const title of TITLES) expect(tile(title).parentElement).toBe(grid);
    expect(within(tile(LARGEST)).getByRole('table').parentElement).toHaveClass('w-full', 'overflow-auto');
  });

  it('asks each query with the params of the tab', () => {
    renderTab({ ...MONTH, branchId: 'b1' });

    expect(useCashSummary).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1' });
    expect(useCashSeries).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1', granularity: 'Day' });
    expect(useCashBreakdown).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1' });
  });

  describe('Số dư các quỹ', () => {
    it('lists every fund and a "Tổng cộng" row with the summary balance', () => {
      renderTab();

      expect(bodyRows('Số dư các quỹ')).toHaveLength(2);
      const footer = within(tile('Số dư các quỹ')).getByRole('rowheader', { name: 'Tổng cộng' }).closest('tr') as HTMLElement;
      expect(cells(footer)).toEqual(['1.260.000.000']);
    });

    it('in company scope the funds start with their branch code', () => {
      renderTab();

      expect(within(tile('Số dư các quỹ')).getByText('CN01 · QTM – Tiền mặt')).toBeInTheDocument();
      expect(within(tile('Số dư các quỹ')).getByText('CN02 · VCB – Vietcombank')).toBeInTheDocument();
    });

    it('in branch scope they have no branch code', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(within(tile('Số dư các quỹ')).getByText('QTM – Tiền mặt')).toBeInTheDocument();
    });

    it('"Sổ quỹ →" opens the cash book and says which branch it opens for', () => {
      renderTab();

      const link = within(tile('Số dư các quỹ')).getByRole('link', { name: 'Sổ quỹ →' });
      expect(link).toHaveAttribute('href', '/reports/cash-book');
      expect(link).toHaveAttribute('title', 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');
    });

    it('without a fund the tile is empty', () => {
      answer(useCashSummary, ok({ ...SUMMARY, funds: [] }));
      renderTab();

      expect(within(tile('Số dư các quỹ')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
    });
  });

  describe('Thu theo lý do and Chi theo lý do', () => {
    it('split the reasons by voucher type', () => {
      renderTab();

      expect(headers('Thu theo lý do')).toEqual(['Lý do', 'Số phiếu', 'Số tiền']);
      expect(bodyRows('Thu theo lý do').map(cells)).toEqual([
        ['Thu tiền khách hàng', '42', '1.800.000.000'],
        ['TPX', '7', '300.000.000'],
      ]);
      expect(headers('Chi theo lý do')).toEqual(['Lý do', 'Số phiếu', 'Số tiền']);
      expect(bodyRows('Chi theo lý do').map(cells)).toEqual([
        ['Trả nhà cung cấp', '18', '1.500.000.000'],
        ['Chi khác', '9', '414.000.000'],
      ]);
    });

    it('right-align the count and the amount', () => {
      renderTab();

      expect(within(rowOf('Thu theo lý do', 'Thu tiền khách hàng')).getByText('42')).toHaveClass('text-right', 'tabular-nums');
      expect(within(rowOf('Thu theo lý do', 'Thu tiền khách hàng')).getByText('1.800.000.000')).toHaveClass('text-right');
    });

    it('a type without a reason is empty', () => {
      answer(useCashBreakdown, ok({ ...BREAKDOWN, byReason: BREAKDOWN.byReason.filter((r) => r.type === 'Receipt') }));
      renderTab();

      expect(within(tile('Chi theo lý do')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(within(tile('Thu theo lý do')).queryByText('Chưa có dữ liệu.')).not.toBeInTheDocument();
    });
  });

  describe('Khoản thu chi lớn nhất', () => {
    it('a receipt shows its amount under "Thu" and nothing under "Chi"', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(headers(LARGEST)).toEqual(['Số phiếu', 'Thời gian', 'Đối tượng', 'Diễn giải', 'Thu', 'Chi']);
      expect(cells(rowOf(LARGEST, 'PT00124'))).toEqual([
        'PT00124', '09:30 05/10', 'CT Xây dựng An Phát', 'Thu tiền hàng tháng 9 theo hợp đồng số 12', '450.000.000', '',
      ]);
    });

    it('a payment shows its amount under "Chi" and nothing under "Thu"', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(cells(rowOf(LARGEST, 'PC00077'))).toEqual(['PC00077', '14:05 07/10', 'Thép Hoà Phát', '', '', '380.000.000']);
    });

    it('a document of the working branch links to its screen', () => {
      renderTab();

      expect(within(tile(LARGEST)).getByRole('link', { name: 'PT00124' })).toHaveAttribute('href', '/receipts/rc1');
      expect(within(tile(LARGEST)).getByRole('link', { name: 'PC00077' })).toHaveAttribute('href', '/payments/pm1');
    });

    it('a document of another branch is plain text', () => {
      renderTab();

      expect(within(tile(LARGEST)).queryByRole('link', { name: 'PT00090' })).not.toBeInTheDocument();
      expect(within(tile(LARGEST)).getByText('PT00090')).toHaveAttribute('title', 'Chứng từ của chi nhánh CN02');
    });

    it('a document is plain text without the right to open its screen', () => {
      signIn(['reports.cash']);
      renderTab();

      expect(within(tile(LARGEST)).queryByRole('link')).not.toBeInTheDocument();
      expect(within(tile(LARGEST)).getByText('PT00124')).toBeInTheDocument();
    });

    it('in company scope has a "Chi nhánh" column', () => {
      renderTab();

      expect(headers(LARGEST)).toEqual(['Số phiếu', 'Chi nhánh', 'Thời gian', 'Đối tượng', 'Diễn giải', 'Thu', 'Chi']);
      expect(cells(rowOf(LARGEST, 'PT00090'))[1]).toBe('CN02');
    });

    it('in branch scope has no "Chi nhánh" column', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(within(tile(LARGEST)).queryByRole('columnheader', { name: 'Chi nhánh' })).not.toBeInTheDocument();
    });

    // useExecutiveScope never reports this pair; the company columns still ask for both.
    it('the company columns need the right to switch, not only the company mode', () => {
      state.scope = { ...companyScope(), canSwitch: false };
      renderTab();

      expect(within(tile(LARGEST)).queryByRole('columnheader', { name: 'Chi nhánh' })).not.toBeInTheDocument();
      expect(within(tile('Số dư các quỹ')).getByText('QTM – Tiền mặt')).toBeInTheDocument();
    });

    it('keeps the time on one line, cuts a long description and right-aligns the amounts', () => {
      renderTab();

      const row = rowOf(LARGEST, 'PT00124');
      expect(within(row).getByText('09:30 05/10')).toHaveClass('whitespace-nowrap');
      const description = within(row).getByText('Thu tiền hàng tháng 9 theo hợp đồng số 12');
      expect(description).toHaveClass('truncate');
      expect(description).toHaveAttribute('title', 'Thu tiền hàng tháng 9 theo hợp đồng số 12');
      expect(within(row).getByText('450.000.000')).toHaveClass('text-right', 'tabular-nums');
    });

    it('without a document the tile is empty', () => {
      answer(useCashBreakdown, ok({ ...BREAKDOWN, largest: [] }));
      renderTab();

      expect(within(tile(LARGEST)).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
    });
  });

  describe('the chart', () => {
    it('is 280 px high with the granularity buttons', () => {
      renderTab();

      const chart = tile('Dòng tiền thu – chi');
      expect(chart.querySelector('svg.recharts-surface')).not.toBeNull();
      expect(chart.querySelector('[style*="height: 280px"]')).not.toBeNull();
      for (const label of ['Ngày', 'Tuần', 'Tháng']) expect(within(chart).getByRole('button', { name: label })).toBeInTheDocument();
    });

    it('"Tháng" asks for a monthly series, and a new period goes back to its default', () => {
      const { rerender } = renderTab();

      fireEvent.click(within(tile('Dòng tiền thu – chi')).getByRole('button', { name: 'Tháng' }));
      expect(useCashSeries).toHaveBeenLastCalledWith({ ...MONTH, granularity: 'Month' });

      rerender({ from: '2026-08-01', to: '2026-10-13' });
      expect(granularities().at(-1)).toEqual(['2026-08-01', 'Week']);
    });

    // keepPreviousData keeps the daily points on screen while the monthly ones load.
    it('labels the points on screen by their own granularity while another one loads', () => {
      renderTab();
      const chart = tile('Dòng tiền thu – chi');

      fireEvent.click(within(chart).getByRole('button', { name: 'Tháng' }));

      expect(useCashSeries).toHaveBeenLastCalledWith({ ...MONTH, granularity: 'Month' });
      // The mock still answers the daily series, as the placeholder data would.
      expect(xTicks(chart)).toEqual(['01/10', '02/10']);
      expect(within(chart).getByRole('button', { name: 'Tháng' })).toHaveAttribute('aria-pressed', 'true');
    });

    it('"Ngày" is disabled for a period longer than 366 days', () => {
      renderTab({ from: '2025-01-01', to: '2026-10-13' });

      expect(within(tile('Dòng tiền thu – chi')).getByRole('button', { name: 'Ngày' })).toBeDisabled();
    });

    it('"Ngày" is allowed for a year', () => {
      renderTab(YEAR);

      expect(within(tile('Dòng tiền thu – chi')).getByRole('button', { name: 'Ngày' })).toBeEnabled();
    });

    it('is empty without any movement', () => {
      answer(useCashSeries, ok({ granularity: 'Day', points: [{ date: '2026-10-01', receipts: 0, payments: 0 }] }));
      renderTab();

      expect(within(tile('Dòng tiền thu – chi')).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
    });
  });

  it('a failing breakdown leaves the funds and the chart usable', () => {
    const breakdown = failed();
    answer(useCashBreakdown, breakdown);
    renderTab();

    for (const title of ['Thu theo lý do', 'Chi theo lý do', LARGEST]) {
      expect(within(tile(title)).getByText('Máy chủ không phản hồi.')).toBeInTheDocument();
    }
    fireEvent.click(within(tile(LARGEST)).getByRole('button', { name: 'Thử lại' }));
    expect(breakdown.refetch).toHaveBeenCalled();
    expect(within(tile('Số dư các quỹ')).getByText('CN01 · QTM – Tiền mặt')).toBeInTheDocument();
    expect(tile('Dòng tiền thu – chi').querySelector('svg.recharts-surface')).not.toBeNull();
  });
});
