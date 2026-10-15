import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Branch } from '@/features/branches/types';
import { useDebtBreakdown, useDebtSummary } from '@/features/executive-dashboard/hooks';
import type { DebtBreakdown, DebtSideSummary, DebtSummary, ExecutiveParams } from '@/features/executive-dashboard/types';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { todayYmd } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import { DebtTab } from './debt-tab';

vi.mock('@/features/executive-dashboard/hooks', () => ({
  useDebtSummary: vi.fn(),
  useDebtBreakdown: vi.fn(),
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

const side = (over: Partial<DebtSideSummary> = {}): DebtSideSummary => ({
  balance: 0, totalOpen: 0, unallocated: 0, notDue: 0, overdue: 0, overduePartnerCount: 0,
  overdueAmounts: [0, 0, 0, 0], dueSoon: 0, dueSoonCount: 0, ...over,
});
const SUMMARY: DebtSummary = {
  at: '2026-10-13',
  overdueBuckets: [{ from: 1, to: 30 }, { from: 31, to: 60 }, { from: 61, to: 90 }, { from: 91, to: null }],
  receivable: side({ balance: 3_000, totalOpen: 3_000, notDue: 1_000, overdue: 2_000, overdueAmounts: [2_000, 0, 0, 0] }),
  payable: side({ balance: 5_000, totalOpen: 5_000, notDue: 4_000, overdue: 1_000, overdueAmounts: [0, 0, 1_000, 0] }),
};
const BREAKDOWN: DebtBreakdown = {
  at: '2026-10-13',
  topDebtors: [
    { partnerId: 'p1', partnerCode: 'KH001', partnerName: 'CT Xây dựng An Phát', balance: 420_000_000, overdue: 120_000_000 },
    { partnerId: 'p2', partnerCode: null, partnerName: 'Khách lẻ', balance: 80_000_000, overdue: 0 },
  ],
  overLimit: [
    { partnerId: 'p1', partnerCode: 'KH001', partnerName: 'CT Xây dựng An Phát', branchId: 'b2', branchCode: 'CN02', balance: 150_000_000, limit: 100_000_000, over: 50_000_000 },
  ],
  byEmployee: [
    { ownerUserId: 'u7', ownerName: 'Nguyễn Thị Hoa', open: 300_000_000, overdue: 90_000_000 },
    { ownerUserId: null, ownerName: null, open: 1_000, overdue: 1_000 },
  ],
  payablesDue: [
    { partnerId: 's1', partnerCode: 'NCC01', partnerName: 'Thép Hoà Phát', branchId: 'b1', branchCode: 'CN01', sourceType: 'StockIn', sourceId: 'si1', docCode: 'NK00012', dueDate: '2026-10-20', open: 250_000_000 },
    { partnerId: 's2', partnerCode: 'NCC02', partnerName: 'Tôn Đông Á', branchId: 'b2', branchCode: 'CN02', sourceType: 'StockIn', sourceId: 'si2', docCode: 'NK00007', dueDate: '2026-11-02', open: 90_000_000 },
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

const MONTH: ExecutiveParams = { from: '2026-10-01', to: '2026-10-13' };
const EVERY_ROUTE = ['reports.debt', 'credit_limit.manage', 'stock_in.view'];

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderTab(params: ExecutiveParams = MONTH) {
  return render(
    <MemoryRouter>
      <DebtTab params={params} />
    </MemoryRouter>,
  );
}

const tile = (title: string) => screen.getByRole('region', { name: title });
const headers = (title: string) => within(tile(title)).getAllByRole('columnheader').map((h) => h.textContent);
const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((c) => c.textContent);
const rowOf = (title: string, text: string) => {
  const row = within(tile(title)).getByText(text).closest('tr');
  if (!row) throw new Error(`no row for ${text}`);
  return row;
};
const TITLES = [
  'Cơ cấu nợ phải thu',
  'Cơ cấu nợ phải trả',
  'Khách nợ nhiều nhất',
  'Khách vượt hạn mức',
  'Công nợ theo nhân viên',
  'Phải trả đến hạn 30 ngày',
];
const HINT = 'Báo cáo mở theo chi nhánh đang làm việc (CN01)';

describe('DebtTab', () => {
  beforeEach(() => {
    state.scope = companyScope();
    answer(useDebtSummary, ok(SUMMARY));
    answer(useDebtBreakdown, ok(BREAKDOWN));
    signIn(EVERY_ROUTE);
  });
  afterEach(() => {
    vi.mocked(useDebtSummary).mockReset();
    vi.mocked(useDebtBreakdown).mockReset();
    useAuthStore.setState({ user: null });
  });

  it('shows the six tiles', () => {
    renderTab();

    for (const title of TITLES) expect(tile(title)).toBeInTheDocument();
  });

  // Below lg the tiles share one column that can shrink, so a wide table scrolls inside its own tile instead
  // of making every tile wider than the screen.
  it('lays the tiles in one shrinkable column below lg, two from lg, and a wide table scrolls in its tile', () => {
    renderTab();

    const grid = tile('Cơ cấu nợ phải thu').parentElement;
    expect(grid).toHaveClass('grid', 'grid-cols-1', 'lg:grid-cols-2');
    for (const title of TITLES) expect(tile(title).parentElement).toBe(grid);
    expect(within(tile('Khách vượt hạn mức')).getByRole('table').parentElement).toHaveClass('w-full', 'overflow-auto');
  });

  it('asks for the balances of the branch of the tab', () => {
    renderTab({ ...MONTH, branchId: 'b1' });

    expect(useDebtSummary).toHaveBeenLastCalledWith({ branchId: 'b1' });
    expect(useDebtBreakdown).toHaveBeenLastCalledWith({ branchId: 'b1' });
  });

  describe('the term structures', () => {
    it('show the receivable and the payable each in its own tile', () => {
      renderTab();

      expect(cells(rowOf('Cơ cấu nợ phải thu', 'Quá hạn 1–30 ngày'))).toEqual(['Quá hạn 1–30 ngày', '2.000', '67%']);
      expect(cells(rowOf('Cơ cấu nợ phải trả', 'Quá hạn 61–90 ngày'))).toEqual(['Quá hạn 61–90 ngày', '1.000', '20%']);
    });

    it('"Tuổi nợ" opens the aging of each side as at today', () => {
      renderTab();

      const receivable = within(tile('Cơ cấu nợ phải thu')).getByRole('link', { name: 'Tuổi nợ' });
      expect(receivable).toHaveAttribute('href', `/reports/debt-aging?side=Receivable&at=${todayYmd()}`);
      expect(receivable).toHaveAttribute('title', HINT);
      const payable = within(tile('Cơ cấu nợ phải trả')).getByRole('link', { name: 'Tuổi nợ' });
      expect(payable).toHaveAttribute('href', `/reports/debt-aging?side=Payable&at=${todayYmd()}`);
      expect(payable).toHaveAttribute('title', HINT);
    });
  });

  describe('Khách nợ nhiều nhất', () => {
    it('lists code – name, the balance and the overdue amount; the name alone without a code', () => {
      renderTab();

      expect(headers('Khách nợ nhiều nhất')).toEqual(['Khách hàng', 'Còn nợ', 'Quá hạn']);
      expect(cells(rowOf('Khách nợ nhiều nhất', 'KH001 – CT Xây dựng An Phát'))).toEqual([
        'KH001 – CT Xây dựng An Phát', '420.000.000', '120.000.000',
      ]);
      expect(cells(rowOf('Khách nợ nhiều nhất', 'Khách lẻ'))).toEqual(['Khách lẻ', '80.000.000', '0']);
    });

    it('a customer links to the overdue documents of that customer as at today', () => {
      renderTab();

      const link = within(tile('Khách nợ nhiều nhất')).getByRole('link', { name: 'KH001 – CT Xây dựng An Phát' });
      expect(link).toHaveAttribute('href', `/reports/debt-overdue?side=Receivable&at=${todayYmd()}&partnerId=p1`);
      expect(link).toHaveAttribute('title', HINT);
    });

    it('a customer is plain text for a user who may not open the report', () => {
      signIn(['credit_limit.manage']);
      renderTab();

      expect(within(tile('Khách nợ nhiều nhất')).queryByRole('link', { name: /An Phát/ })).not.toBeInTheDocument();
      expect(within(tile('Khách nợ nhiều nhất')).getByText('KH001 – CT Xây dựng An Phát')).toBeInTheDocument();
    });

    it('an overdue amount above 0 is rose, 0 is not', () => {
      renderTab();

      expect(within(rowOf('Khách nợ nhiều nhất', 'KH001 – CT Xây dựng An Phát')).getByText('120.000.000')).toHaveClass('text-rose-700');
      expect(within(rowOf('Khách nợ nhiều nhất', 'Khách lẻ')).getByText('0')).not.toHaveClass('text-rose-700');
    });

    it('"Số dư công nợ" opens the receivable balance of the period', () => {
      renderTab();

      const link = within(tile('Khách nợ nhiều nhất')).getByRole('link', { name: 'Số dư công nợ' });
      expect(link).toHaveAttribute('href', '/reports/debt-balance?side=Receivable&from=2026-10-01&to=2026-10-13');
      expect(link).toHaveAttribute('title', HINT);
    });
  });

  describe('Khách vượt hạn mức', () => {
    it('in company scope has a "Chi nhánh" column', () => {
      renderTab();

      expect(headers('Khách vượt hạn mức')).toEqual(['Khách hàng', 'Chi nhánh', 'Dư nợ', 'Hạn mức', 'Vượt']);
      expect(cells(rowOf('Khách vượt hạn mức', 'KH001 – CT Xây dựng An Phát'))).toEqual([
        'KH001 – CT Xây dựng An Phát', 'CN02', '150.000.000', '100.000.000', '50.000.000',
      ]);
    });

    it('in branch scope has no "Chi nhánh" column', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(headers('Khách vượt hạn mức')).toEqual(['Khách hàng', 'Dư nợ', 'Hạn mức', 'Vượt']);
    });

    // useExecutiveScope never reports this pair; the column still asks for both.
    it('the column needs the right to switch, not only the company mode', () => {
      state.scope = { ...companyScope(), canSwitch: false };
      renderTab();

      expect(within(tile('Khách vượt hạn mức')).queryByRole('columnheader', { name: 'Chi nhánh' })).not.toBeInTheDocument();
    });

    // The credit limits page shows the limits of the working branch only, while the list covers the scope.
    it('"Hạn mức nợ" opens the credit limits with the hint of the working branch', () => {
      renderTab();

      const link = within(tile('Khách vượt hạn mức')).getByRole('link', { name: 'Hạn mức nợ' });
      expect(link).toHaveAttribute('href', '/credit-limits');
      expect(link).toHaveAttribute('title', HINT);
    });
  });

  describe('Công nợ theo nhân viên', () => {
    it('lists the owner, the open and the overdue amount; documents without an owner are "Không xác định"', () => {
      renderTab();

      expect(headers('Công nợ theo nhân viên')).toEqual(['Nhân viên', 'Còn nợ', 'Quá hạn']);
      expect(cells(rowOf('Công nợ theo nhân viên', 'Nguyễn Thị Hoa'))).toEqual(['Nguyễn Thị Hoa', '300.000.000', '90.000.000']);
      expect(cells(rowOf('Công nợ theo nhân viên', 'Không xác định'))).toEqual(['Không xác định', '1.000', '1.000']);
    });

    it('"Công nợ theo nhân viên" opens the report of the period', () => {
      renderTab();

      const link = within(tile('Công nợ theo nhân viên')).getByRole('link', { name: 'Công nợ theo nhân viên' });
      expect(link).toHaveAttribute('href', '/reports/debt-by-salesperson?side=Receivable&from=2026-10-01&to=2026-10-13');
      expect(link).toHaveAttribute('title', HINT);
    });
  });

  describe('Phải trả đến hạn 30 ngày', () => {
    it('lists the supplier, the document, the due date as dd/MM/yyyy and the amount', () => {
      renderTab();

      expect(headers('Phải trả đến hạn 30 ngày')).toEqual(['Nhà cung cấp', 'Chứng từ', 'Hạn TT', 'Còn phải trả']);
      expect(cells(rowOf('Phải trả đến hạn 30 ngày', 'NCC01 – Thép Hoà Phát'))).toEqual([
        'NCC01 – Thép Hoà Phát', 'NK00012', '20/10/2026', '250.000.000',
      ]);
    });

    it('a document of the working branch links to its screen, one of another branch is plain text', () => {
      renderTab();

      const payables = tile('Phải trả đến hạn 30 ngày');
      expect(within(payables).getByRole('link', { name: 'NK00012' })).toHaveAttribute('href', '/stock-in/si1');
      expect(within(payables).queryByRole('link', { name: 'NK00007' })).not.toBeInTheDocument();
      expect(within(payables).getByText('NK00007')).toHaveAttribute('title', 'Chứng từ của chi nhánh CN02');
    });

    it('"Số dư công nợ" opens the payable balance of the period', () => {
      renderTab();

      expect(within(tile('Phải trả đến hạn 30 ngày')).getByRole('link', { name: 'Số dư công nợ' })).toHaveAttribute(
        'href',
        '/reports/debt-balance?side=Payable&from=2026-10-01&to=2026-10-13',
      );
    });
  });

  it('every list puts its amounts right-aligned in tabular figures', () => {
    renderTab();

    for (const [title, text, amount] of [
      ['Khách nợ nhiều nhất', 'Khách lẻ', '80.000.000'],
      ['Khách vượt hạn mức', 'CN02', '100.000.000'],
      ['Công nợ theo nhân viên', 'Nguyễn Thị Hoa', '300.000.000'],
      ['Phải trả đến hạn 30 ngày', 'NK00012', '250.000.000'],
    ]) {
      expect(within(rowOf(title, text)).getByText(amount).closest('td')).toHaveClass('text-right', 'tabular-nums');
    }
  });

  it('in branch scope the report links have no hint', () => {
    state.scope = branchScope();
    renderTab({ ...MONTH, branchId: 'b1' });

    expect(within(tile('Cơ cấu nợ phải thu')).getByRole('link', { name: 'Tuổi nợ' })).not.toHaveAttribute('title');
    expect(within(tile('Khách nợ nhiều nhất')).getByRole('link', { name: 'KH001 – CT Xây dựng An Phát' })).not.toHaveAttribute('title');
    expect(within(tile('Khách vượt hạn mức')).getByRole('link', { name: 'Hạn mức nợ' })).not.toHaveAttribute('title');
  });

  it('empty lists show "Chưa có dữ liệu."', () => {
    answer(useDebtBreakdown, ok({ at: '2026-10-13', topDebtors: [], overLimit: [], byEmployee: [], payablesDue: [] }));
    renderTab();

    for (const title of TITLES.slice(2)) {
      expect(within(tile(title)).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(within(tile(title)).queryByRole('table')).not.toBeInTheDocument();
    }
  });

  it('a failing breakdown leaves the term structures usable', () => {
    const breakdown = failed();
    answer(useDebtBreakdown, breakdown);
    renderTab();

    for (const title of TITLES.slice(2)) {
      expect(within(tile(title)).getByText('Máy chủ không phản hồi.')).toBeInTheDocument();
    }
    fireEvent.click(within(tile('Khách vượt hạn mức')).getByRole('button', { name: 'Thử lại' }));
    expect(breakdown.refetch).toHaveBeenCalled();
    expect(within(tile('Cơ cấu nợ phải thu')).getByText('Quá hạn 1–30 ngày')).toBeInTheDocument();
  });
});
