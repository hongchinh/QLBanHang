import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Branch } from '@/features/branches/types';
import { useInventoryBreakdown, useInventorySummary } from '@/features/executive-dashboard/hooks';
import type { ExecutiveParams, InventoryBreakdown, InventorySummary } from '@/features/executive-dashboard/types';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { useAuthStore } from '@/stores/auth-store';
import { InventoryTab } from './inventory-tab';

vi.mock('@/features/executive-dashboard/hooks', () => ({
  useInventorySummary: vi.fn(),
  useInventoryBreakdown: vi.fn(),
}));

type Scope = ReturnType<typeof useExecutiveScope>;
const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const B3: Branch = { id: 'b3', code: 'CN03', name: 'Đà Nẵng' };
const companyScope = (): Scope => ({
  canSwitch: true, mode: 'company', branchId: undefined, workingBranch: B1, branches: [B1, B2, B3],
  selectCompany: vi.fn(), selectBranch: vi.fn(),
});
const branchScope = (): Scope => ({ ...companyScope(), mode: 'branch', branchId: 'b1' });
const state: { scope: Scope } = { scope: companyScope() };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({ useExecutiveScope: () => state.scope }));

const SUMMARY: InventorySummary = {
  from: '2026-10-01', to: '2026-10-13', canViewCost: true, stockValue: 4_620_000_000,
  productCount: 318, warehouseCount: 3, negativeCount: 5, inValue: 1_250_000_000, outCost: 980_000_000,
};
const NO_COST_SUMMARY: InventorySummary = { ...SUMMARY, canViewCost: false, stockValue: null, inValue: null, outCost: null };
const BREAKDOWN: InventoryBreakdown = {
  canViewCost: true,
  byWarehouse: [
    { id: 'w1', code: 'KHO01', name: 'Kho chính', branchCode: 'CN01', productCount: 210, value: 3_100_000_000 },
    { id: 'w2', code: 'KHO02', name: 'Kho Hưng Yên', branchCode: 'CN02', productCount: 108, value: 1_520_000_000 },
  ],
  byProductGroup: [
    { id: 'g1', code: null, name: 'Tôn lợp', branchCode: null, productCount: 120, value: 2_000_000_000 },
    { id: null, code: null, name: null, branchCode: null, productCount: 4, value: 20_000_000 },
  ],
  negative: [
    { productId: 'p9', productCode: 'SP009', productName: 'Vít bắn tôn', unitName: 'Hộp', warehouseId: 'w2', warehouseCode: 'KHO02', branchCode: 'CN02', quantity: -2.5 },
  ],
  topByValue: [
    { productId: 'p1', code: 'SP001', name: 'Tôn lạnh 0,45', unitName: 'm²', quantity: 1_250.125, value: 412_000_000 },
  ],
};
const NO_COST_BREAKDOWN: InventoryBreakdown = {
  ...BREAKDOWN,
  canViewCost: false,
  byWarehouse: BREAKDOWN.byWarehouse.map((row) => ({ ...row, value: null })),
  byProductGroup: BREAKDOWN.byProductGroup.map((row) => ({ ...row, value: null })),
  topByValue: [],
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
const STAT_LABELS = ['Giá trị tồn kho', 'Nhập trong kỳ', 'Giá vốn xuất trong kỳ', 'Tồn âm'];
const TITLES = ['Giá trị tồn theo kho', 'Giá trị tồn theo nhóm hàng', 'Hàng tồn giá trị lớn', 'Mã hàng tồn âm'];
const HINT = 'Báo cáo mở theo chi nhánh đang làm việc (CN01)';

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderTab(params: ExecutiveParams = MONTH) {
  return render(
    <MemoryRouter>
      <InventoryTab params={params} />
    </MemoryRouter>,
  );
}

const stat = (label: string) => screen.getByRole('group', { name: label });
const tile = (title: string) => screen.getByRole('region', { name: title });
const headers = (title: string) => within(tile(title)).getAllByRole('columnheader').map((h) => h.textContent);
const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((c) => c.textContent);
const rowOf = (title: string, text: string) => {
  const row = within(tile(title)).getByText(text).closest('tr');
  if (!row) throw new Error(`no row for ${text}`);
  return row;
};

describe('InventoryTab', () => {
  beforeEach(() => {
    state.scope = companyScope();
    answer(useInventorySummary, ok(SUMMARY));
    answer(useInventoryBreakdown, ok(BREAKDOWN));
    signIn(['reports.inventory']);
  });
  afterEach(() => {
    vi.mocked(useInventorySummary).mockReset();
    vi.mocked(useInventoryBreakdown).mockReset();
    useAuthStore.setState({ user: null });
  });

  it('asks for the period figures with the params and the stock with the branch only', () => {
    renderTab({ ...MONTH, branchId: 'b1' });

    expect(useInventorySummary).toHaveBeenLastCalledWith({ ...MONTH, branchId: 'b1' });
    expect(useInventoryBreakdown).toHaveBeenLastCalledWith({ branchId: 'b1' });
  });

  describe('the stat tiles', () => {
    it('show the four figures in compact money', () => {
      renderTab();

      expect(within(stat('Giá trị tồn kho')).getByText('4,62 tỷ')).toHaveClass('tabular-nums');
      expect(within(stat('Giá trị tồn kho')).getByText('318 mã · 3 kho')).toBeInTheDocument();
      expect(within(stat('Nhập trong kỳ')).getByText('1,25 tỷ')).toBeInTheDocument();
      expect(within(stat('Giá vốn xuất trong kỳ')).getByText('980 tr')).toBeInTheDocument();
      expect(within(stat('Tồn âm')).getByText('5')).toBeInTheDocument();
    });

    it('without the cost the three money tiles are "—" and the counts stay', () => {
      answer(useInventorySummary, ok(NO_COST_SUMMARY));
      renderTab();

      for (const label of ['Giá trị tồn kho', 'Nhập trong kỳ', 'Giá vốn xuất trong kỳ']) {
        expect(within(stat(label)).getByText('—')).toBeInTheDocument();
      }
      expect(within(stat('Giá trị tồn kho')).getByText('318 mã · 3 kho')).toBeInTheDocument();
      expect(within(stat('Tồn âm')).getByText('5')).toBeInTheDocument();
    });

    it('negative stock is an amber hint', () => {
      renderTab();

      expect(within(stat('Tồn âm')).getByText('cặp mã × kho dưới 0')).toHaveClass('text-amber-700');
    });

    it('no negative stock says "Không có"', () => {
      answer(useInventorySummary, ok({ ...SUMMARY, negativeCount: 0 }));
      renderTab();

      expect(within(stat('Tồn âm')).getByText('0')).toBeInTheDocument();
      expect(within(stat('Tồn âm')).getByText('Không có')).not.toHaveClass('text-amber-700');
      expect(within(stat('Tồn âm')).queryByText('cặp mã × kho dưới 0')).not.toBeInTheDocument();
    });

    it('a failed summary shows "Thử lại" on the four tiles and retries', () => {
      const summary = failed();
      answer(useInventorySummary, summary);
      renderTab();

      for (const label of STAT_LABELS) expect(within(stat(label)).getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
      fireEvent.click(within(stat('Tồn âm')).getByRole('button', { name: 'Thử lại' }));
      expect(summary.refetch).toHaveBeenCalled();
      expect(within(tile('Giá trị tồn theo kho')).getByText('210')).toBeInTheDocument();
    });

    it('load and dim with their query', () => {
      answer(useInventorySummary, ok(undefined, { isLoading: true }));
      const { unmount } = renderTab();
      for (const label of STAT_LABELS) expect(stat(label)).toHaveAttribute('aria-busy', 'true');
      expect(screen.queryByText('4,62 tỷ')).not.toBeInTheDocument();
      unmount();

      answer(useInventorySummary, ok(SUMMARY, { isPlaceholderData: true }));
      renderTab();
      expect(stat('Giá trị tồn kho')).toHaveAttribute('aria-busy', 'true');
      expect(within(stat('Giá trị tồn kho')).getByText('4,62 tỷ')).toHaveClass('opacity-60');
    });
  });

  it('shows the four tiles', () => {
    renderTab();

    for (const title of TITLES) expect(tile(title)).toBeInTheDocument();
  });

  describe('Giá trị tồn theo kho', () => {
    it('in company scope a warehouse starts with its branch code', () => {
      renderTab();

      expect(headers('Giá trị tồn theo kho')).toEqual(['Kho', 'Số mã', 'Giá trị']);
      expect(cells(rowOf('Giá trị tồn theo kho', 'CN01 · KHO01 – Kho chính'))).toEqual([
        'CN01 · KHO01 – Kho chính', '210', '3.100.000.000',
      ]);
    });

    it('in branch scope it has no branch code', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(within(tile('Giá trị tồn theo kho')).getByText('KHO01 – Kho chính')).toBeInTheDocument();
    });

    it('a user who cannot switch sees no branch code', () => {
      state.scope = { ...companyScope(), canSwitch: false };
      renderTab();

      expect(within(tile('Giá trị tồn theo kho')).getByText('KHO01 – Kho chính')).toBeInTheDocument();
      expect(within(tile('Mã hàng tồn âm')).getByText('KHO02')).toBeInTheDocument();
    });

    it('without the cost the value is "—"', () => {
      answer(useInventoryBreakdown, ok(NO_COST_BREAKDOWN));
      renderTab();

      expect(cells(rowOf('Giá trị tồn theo kho', 'CN01 · KHO01 – Kho chính'))[2]).toBe('—');
    });

    it('"Tồn kho →" opens stock on hand and says which branch it opens for', () => {
      renderTab();

      const link = within(tile('Giá trị tồn theo kho')).getByRole('link', { name: 'Tồn kho →' });
      expect(link).toHaveAttribute('href', '/inventory/stock-on-hand');
      expect(link).toHaveAttribute('title', HINT);
    });
  });

  describe('Giá trị tồn theo nhóm hàng', () => {
    it('lists the groups; products without a group are "Chưa phân nhóm"', () => {
      renderTab();

      expect(headers('Giá trị tồn theo nhóm hàng')).toEqual(['Nhóm', 'Số mã', 'Giá trị']);
      expect(cells(rowOf('Giá trị tồn theo nhóm hàng', 'Tôn lợp'))).toEqual(['Tôn lợp', '120', '2.000.000.000']);
      expect(cells(rowOf('Giá trị tồn theo nhóm hàng', 'Chưa phân nhóm'))).toEqual(['Chưa phân nhóm', '4', '20.000.000']);
    });
  });

  describe('Hàng tồn giá trị lớn', () => {
    it('lists the product, the unit, the quantity and the value', () => {
      renderTab();

      expect(headers('Hàng tồn giá trị lớn')).toEqual(['Hàng hoá', 'ĐVT', 'Số lượng', 'Giá trị']);
      const row = rowOf('Hàng tồn giá trị lớn', 'SP001 – Tôn lạnh 0,45');
      expect(cells(row)).toEqual(['SP001 – Tôn lạnh 0,45', 'm²', '1,250.125', '412.000.000']);
      expect(within(row).getByText('1,250.125')).toHaveClass('text-right', 'tabular-nums');
    });

    it('is absent without the cost', () => {
      answer(useInventoryBreakdown, ok(NO_COST_BREAKDOWN));
      renderTab();

      expect(screen.queryByRole('region', { name: 'Hàng tồn giá trị lớn' })).not.toBeInTheDocument();
      for (const title of TITLES.filter((t) => t !== 'Hàng tồn giá trị lớn')) expect(tile(title)).toBeInTheDocument();
    });
  });

  describe('Mã hàng tồn âm', () => {
    it('lists the product, the warehouse with its branch in company scope, the unit and the quantity in rose', () => {
      renderTab();

      expect(headers('Mã hàng tồn âm')).toEqual(['Hàng hoá', 'Kho', 'ĐVT', 'Số lượng']);
      const row = rowOf('Mã hàng tồn âm', 'SP009 – Vít bắn tôn');
      expect(cells(row)).toEqual(['SP009 – Vít bắn tôn', 'CN02 · KHO02', 'Hộp', '-2.5']);
      expect(within(row).getByText('-2.5')).toHaveClass('text-rose-700');
      expect(within(row).getByText('-2.5').closest('td')).toHaveClass('text-right', 'tabular-nums');
    });

    it('in branch scope the warehouse has no branch code', () => {
      state.scope = branchScope();
      renderTab({ ...MONTH, branchId: 'b1' });

      expect(cells(rowOf('Mã hàng tồn âm', 'SP009 – Vít bắn tôn'))[1]).toBe('KHO02');
    });

    it('"Tồn kho →" opens stock on hand', () => {
      renderTab();

      expect(within(tile('Mã hàng tồn âm')).getByRole('link', { name: 'Tồn kho →' })).toHaveAttribute(
        'href',
        '/inventory/stock-on-hand',
      );
    });
  });

  it('empty lists show "Chưa có dữ liệu."', () => {
    answer(useInventoryBreakdown, ok({ canViewCost: true, byWarehouse: [], byProductGroup: [], negative: [], topByValue: [] }));
    renderTab();

    for (const title of TITLES) {
      expect(within(tile(title)).getByText('Chưa có dữ liệu.')).toBeInTheDocument();
      expect(within(tile(title)).queryByRole('table')).not.toBeInTheDocument();
    }
  });

  it('a failing breakdown leaves the stat tiles usable and shows its error on each of its tiles', () => {
    const breakdown = failed();
    answer(useInventoryBreakdown, breakdown);
    renderTab();

    for (const title of TITLES) expect(within(tile(title)).getByText('Máy chủ không phản hồi.')).toBeInTheDocument();
    fireEvent.click(within(tile('Mã hàng tồn âm')).getByRole('button', { name: 'Thử lại' }));
    expect(breakdown.refetch).toHaveBeenCalled();
    expect(within(stat('Giá trị tồn kho')).getByText('4,62 tỷ')).toBeInTheDocument();
  });
});
