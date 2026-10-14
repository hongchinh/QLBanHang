import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Outlet, useLocation } from 'react-router-dom';
import { App } from '@/App';
import type { Branch } from '@/features/branches/types';
import type { useExecutiveAccess } from '@/features/executive-dashboard/use-executive-access';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { useAuthStore } from '@/stores/auth-store';
import type { OverviewTab } from './tabs/overview-tab';
import type { SalesTab } from './tabs/sales-tab';
import { ExecutiveDashboardPage } from './executive-dashboard-page';

vi.mock('@/hooks/useBeforeInstallPrompt', () => ({
  useBeforeInstallPrompt: () => ({ canShow: false, install: vi.fn(), dismiss: vi.fn() }),
}));
vi.mock('@/hooks/usePushNotification', () => ({
  usePushNotification: () => ({ state: 'unsupported', subscribe: vi.fn() }),
}));
vi.mock('@/routes/auth-init', () => ({ AuthInit: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('@/components/layout/app-layout', () => ({ AppLayout: () => <Outlet /> }));

type OverviewProps = React.ComponentProps<typeof OverviewTab>;
type Scope = ReturnType<typeof useExecutiveScope>;
type Access = ReturnType<typeof useExecutiveAccess>;

const overviewTab = vi.fn((props: OverviewProps) => {
  void props;
  return <div data-testid="overview-tab" />;
});
vi.mock('./tabs/overview-tab', () => ({ OverviewTab: (props: OverviewProps) => overviewTab(props) }));
type SalesProps = React.ComponentProps<typeof SalesTab>;
const salesTab = vi.fn((props: SalesProps) => {
  void props;
  return <div data-testid="sales-tab" />;
});
vi.mock('./tabs/sales-tab', () => ({ SalesTab: (props: SalesProps) => salesTab(props) }));
vi.mock('./tabs/cash-tab', () => ({ CashTab: () => <div data-testid="cash-tab" /> }));
vi.mock('./tabs/debt-tab', () => ({ DebtTab: () => <div data-testid="debt-tab" /> }));
vi.mock('./tabs/inventory-tab', () => ({ InventoryTab: () => <div data-testid="inventory-tab" /> }));

const useExecutiveLive = vi.fn((branchId: string | undefined) => {
  void branchId;
  return { status: 'connected' as const, updatedAt: null };
});
vi.mock('@/features/executive-dashboard/use-executive-live', () => ({
  useExecutiveLive: (branchId: string | undefined) => useExecutiveLive(branchId),
}));

const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const branchScope = (): Scope => ({
  canSwitch: true,
  mode: 'branch',
  branchId: 'b1',
  workingBranch: B1,
  branches: [B1, B2],
  selectCompany: vi.fn(),
  selectBranch: vi.fn(),
  unknown: false,
  retry: vi.fn(),
});
// Every panel is registered; this access keeps the bar at the overview alone.
const overviewOnly = (): Access => ({ sales: true, cash: true, debt: true, inventory: true, tabs: ['overview'] });

// access null: the real useExecutiveAccess, from the permissions of the signed-in user.
const state: { scope: Scope; access: Access | null } = { scope: branchScope(), access: overviewOnly() };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({ useExecutiveScope: () => state.scope }));
vi.mock('@/features/executive-dashboard/use-executive-access', async (importOriginal) => {
  const actual = await importOriginal<{ useExecutiveAccess: typeof useExecutiveAccess }>();
  return {
    useExecutiveAccess: () => {
      const real = actual.useExecutiveAccess();
      return state.access ?? real;
    },
  };
});

function signIn(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 'token',
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="search">{location.search}</div>;
}

function renderPage(url = '/dashboard/executive?from=2026-10-01&to=2026-10-13') {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <ExecutiveDashboardPage />
      <LocationProbe />
    </MemoryRouter>,
  );
}

const lastOverviewProps = () => overviewTab.mock.calls[overviewTab.mock.calls.length - 1][0];

describe('ExecutiveDashboardPage', () => {
  beforeEach(() => {
    overviewTab.mockClear();
    salesTab.mockClear();
    useExecutiveLive.mockClear();
    state.scope = branchScope();
    state.access = overviewOnly();
  });
  afterEach(() => useAuthStore.setState({ accessToken: null, user: null }));

  it('shows the heading, the live status, the scope select and the period chips', () => {
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Điều hành' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Trực tiếp');
    expect(screen.getByRole('combobox')).toHaveTextContent('CN01 — Hà Nội');
    for (const chip of ['Hôm nay', 'Tuần này', 'Tháng này', 'Năm nay', 'Tuỳ chỉnh ▾']) {
      expect(screen.getByRole('button', { name: chip })).toBeInTheDocument();
    }
    expect(screen.queryByRole('button', { name: '7N' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tháng trước' })).not.toBeInTheDocument();
  });

  it('prints the comparison period', () => {
    renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-13');

    expect(screen.getByText('So với kỳ trước: 18/09/2026 – 30/09/2026')).toBeInTheDocument();
  });

  it('selects the Tổng quan tab and hands it the period, the branch of the scope and no other tab', () => {
    renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-13');

    expect(screen.getByRole('tab', { name: 'Tổng quan' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('overview-tab')).toBeInTheDocument();
    const props = lastOverviewProps();
    expect(props.params).toEqual({ from: '2026-10-01', to: '2026-10-13', branchId: 'b1' });
    expect(props.openTabs).toEqual([]);
  });

  it('sends no branch in company scope', () => {
    state.scope = { ...branchScope(), mode: 'company', branchId: undefined };
    renderPage();

    expect(lastOverviewProps().params).toEqual({ from: '2026-10-01', to: '2026-10-13' });
    expect(lastOverviewProps().params.branchId).toBeUndefined();
    expect(useExecutiveLive).toHaveBeenLastCalledWith(undefined);
  });

  it('follows the live refresh of the branch it shows', () => {
    renderPage();

    expect(useExecutiveLive).toHaveBeenLastCalledWith('b1');
  });

  it('lists only the tabs the user may open', () => {
    renderPage();

    expect(screen.getAllByRole('tab').map((t) => t.textContent)).toEqual(['Tổng quan']);
  });

  it('shows the overview for a tab in the URL that the user cannot open', () => {
    renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-13&tab=cash');

    expect(screen.getByRole('tab', { name: 'Tổng quan' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('overview-tab')).toBeInTheDocument();
  });

  it('the overview opens another tab through the URL', () => {
    renderPage();

    act(() => lastOverviewProps().onOpenTab('sales'));

    expect(new URLSearchParams(screen.getByTestId('search').textContent ?? '').get('tab')).toBe('sales');
  });

  describe('with the tabs of the permissions', () => {
    const EVERY_DOMAIN = ['dashboard.executive', 'reports.profit', 'reports.cash', 'reports.debt', 'reports.inventory'];
    const tabNames = () => screen.getAllByRole('tab').map((t) => t.textContent);
    const selectedTab = () => screen.getByRole('tab', { selected: true }).textContent;
    const searchTab = () => new URLSearchParams(screen.getByTestId('search').textContent ?? '').get('tab');

    beforeEach(() => {
      state.access = null;
    });

    it('a user with every domain sees the five tabs in order, and the overview can open the other four', () => {
      signIn(EVERY_DOMAIN);
      renderPage();

      expect(tabNames()).toEqual(['Tổng quan', 'Kinh doanh', 'Tiền', 'Công nợ', 'Kho']);
      expect(lastOverviewProps().openTabs).toEqual(['sales', 'cash', 'debt', 'inventory']);
      // Only the active panel is mounted.
      for (const id of ['sales-tab', 'cash-tab', 'debt-tab', 'inventory-tab']) {
        expect(screen.queryByTestId(id)).not.toBeInTheDocument();
      }
    });

    it('a user with reports.cash only sees Tổng quan and Tiền', () => {
      signIn(['dashboard.executive', 'reports.cash']);
      renderPage();

      expect(tabNames()).toEqual(['Tổng quan', 'Tiền']);
      expect(lastOverviewProps().openTabs).toEqual(['cash']);
    });

    it('?tab=debt opens the debt tab for a user with reports.debt', () => {
      signIn(['dashboard.executive', 'reports.debt']);
      renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-13&tab=debt');

      expect(selectedTab()).toBe('Công nợ');
      expect(screen.getByTestId('debt-tab')).toBeInTheDocument();
      expect(screen.queryByTestId('overview-tab')).not.toBeInTheDocument();
    });

    it('?tab=debt shows the overview for a user without reports.debt', () => {
      signIn(['dashboard.executive', 'reports.cash']);
      renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-13&tab=debt');

      expect(selectedTab()).toBe('Tổng quan');
      expect(screen.getByTestId('overview-tab')).toBeInTheDocument();
      expect(screen.queryByTestId('debt-tab')).not.toBeInTheDocument();
    });

    it('choosing "Kho" writes tab=inventory to the URL and opens the inventory tab', () => {
      signIn(EVERY_DOMAIN);
      renderPage();

      fireEvent.mouseDown(screen.getByRole('tab', { name: 'Kho' }));

      expect(searchTab()).toBe('inventory');
      expect(selectedTab()).toBe('Kho');
      expect(screen.getByTestId('inventory-tab')).toBeInTheDocument();
      expect(screen.queryByTestId('overview-tab')).not.toBeInTheDocument();
    });

    it('the onOpenTab the overview received opens the sales tab, with the params of the page', () => {
      signIn(EVERY_DOMAIN);
      renderPage();

      act(() => lastOverviewProps().onOpenTab('sales'));

      expect(selectedTab()).toBe('Kinh doanh');
      expect(screen.getByTestId('sales-tab')).toBeInTheDocument();
      const props = salesTab.mock.calls[salesTab.mock.calls.length - 1][0];
      expect(props.params).toEqual({ from: '2026-10-01', to: '2026-10-13', branchId: 'b1' });
    });

    it('choosing "Tổng quan" leaves the default out of the URL', () => {
      signIn(EVERY_DOMAIN);
      renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-13&tab=cash');
      expect(screen.getByTestId('cash-tab')).toBeInTheDocument();

      fireEvent.mouseDown(screen.getByRole('tab', { name: 'Tổng quan' }));

      expect(searchTab()).toBeNull();
      expect(screen.getByTestId('overview-tab')).toBeInTheDocument();
    });
  });

  it('a registered panel the user may not open is not on the bar', () => {
    state.access = { sales: true, cash: true, debt: false, inventory: true, tabs: ['overview', 'sales', 'cash', 'inventory'] };
    renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-13&tab=debt');

    expect(screen.getAllByRole('tab').map((t) => t.textContent)).toEqual(['Tổng quan', 'Kinh doanh', 'Tiền', 'Kho']);
    expect(screen.getByTestId('overview-tab')).toBeInTheDocument();
    expect(screen.queryByTestId('debt-tab')).not.toBeInTheDocument();
  });

  // Five tabs are wider than a 375 px screen: the bar scrolls instead of pushing the page wider, starting
  // at its first tab.
  it('the tab bar scrolls sideways on a narrow screen', () => {
    renderPage();

    expect(screen.getByRole('tablist')).toHaveClass('max-w-full', 'overflow-x-auto', 'justify-start');
  });

  // A page left open overnight with the default period moves to the new day: the tabs get the new period (and
  // build new query keys from it), and the comparison period follows.
  describe('without a period in the URL', () => {
    beforeEach(() => {
      vi.useFakeTimers();
      // Saturday 31 October 2026, 23:59:30 in VN time.
      vi.setSystemTime(new Date(2026, 9, 31, 23, 59, 30));
    });
    afterEach(() => vi.useRealTimers());

    it('follows the VN date past midnight', () => {
      renderPage('/dashboard/executive');
      expect(lastOverviewProps().params).toEqual({ from: '2026-10-01', to: '2026-10-31', branchId: 'b1' });
      expect(screen.getByText('So với kỳ trước: 31/08/2026 – 30/09/2026')).toBeInTheDocument();

      act(() => vi.advanceTimersByTime(30_000));

      expect(lastOverviewProps().params).toEqual({ from: '2026-11-01', to: '2026-11-01', branchId: 'b1' });
      expect(screen.getByText('So với kỳ trước: 31/10/2026 – 31/10/2026')).toBeInTheDocument();
      // A preset still (on the 1st "Hôm nay" comes first), not "Tuỳ chỉnh".
      expect(screen.getByRole('button', { name: 'Tuỳ chỉnh ▾' })).not.toHaveClass('bg-foreground');
      expect(screen.getByTestId('search')).toBeEmptyDOMElement();
    });

    it('keeps a period given in the URL', () => {
      renderPage('/dashboard/executive?from=2026-10-01&to=2026-10-31');

      act(() => vi.advanceTimersByTime(60_000));

      expect(lastOverviewProps().params).toEqual({ from: '2026-10-01', to: '2026-10-31', branchId: 'b1' });
    });
  });

  describe('a period the API refuses', () => {
    beforeEach(() => {
      vi.useFakeTimers({ toFake: ['Date'] });
      // Tuesday 13 October 2026.
      vi.setSystemTime(new Date(2026, 9, 13, 10, 0, 0));
    });
    afterEach(() => vi.useRealTimers());

    it('in the URL gives way to the default period and its comparison period', () => {
      renderPage('/dashboard/executive?from=2026-10-20&to=2026-10-01');

      expect(lastOverviewProps().params).toEqual({ from: '2026-10-01', to: '2026-10-13', branchId: 'b1' });
      expect(screen.getByText('So với kỳ trước: 18/09/2026 – 30/09/2026')).toBeInTheDocument();
    });

    it('cannot be applied from "Tuỳ chỉnh", which gives the reason of the API', () => {
      renderPage();
      fireEvent.click(screen.getByRole('button', { name: 'Tuỳ chỉnh ▾' }));
      fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '2022-10-01' } });
      fireEvent.change(screen.getByLabelText('Đến ngày'), { target: { value: '2026-10-13' } });

      expect(screen.getByRole('alert')).toHaveTextContent('Khoảng thời gian tối đa là 3 năm.');
      expect(screen.getByRole('button', { name: 'Áp dụng' })).toBeDisabled();
      fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }));
      expect(new URLSearchParams(screen.getByTestId('search').textContent ?? '').get('from')).toBe('2026-10-01');
    });
  });

  // /me/branches failed for a user of every branch: without a branch id the server would answer for the whole
  // company under a page that names no scope. The page says so and offers a retry instead of the tabs.
  it('an unknown scope shows an error with a retry instead of the tabs', () => {
    const retry = vi.fn();
    state.scope = {
      ...branchScope(), canSwitch: false, branchId: undefined, workingBranch: undefined, branches: [], unknown: true, retry,
    };
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Điều hành' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Không tải được danh sách chi nhánh');
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
    expect(overviewTab).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('a user who cannot switch gets no scope select', () => {
    state.scope = { ...branchScope(), canSwitch: false, branchId: undefined, branches: [B1] };
    renderPage();

    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(lastOverviewProps().params.branchId).toBeUndefined();
  });
});

// The route itself (App.tsx), with the real ProtectedRoute.
describe('route /dashboard/executive', () => {
  afterEach(() => {
    window.history.pushState({}, '', '/');
    useAuthStore.setState({ accessToken: null, user: null });
  });

  it('opens the page for a user with dashboard.executive', () => {
    signIn(['dashboard.executive']);
    window.history.pushState({}, '', '/dashboard/executive');
    render(<App />);

    expect(screen.getByRole('heading', { level: 1, name: 'Điều hành' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/dashboard/executive');
  });

  it('sends a user without it to /403', () => {
    signIn(['quotations.view_all', 'reports.profit']);
    window.history.pushState({}, '', '/dashboard/executive');
    render(<App />);

    expect(window.location.pathname).toBe('/403');
    expect(screen.queryByRole('heading', { name: 'Điều hành' })).not.toBeInTheDocument();
    expect(screen.getByText('Bạn không có quyền truy cập chức năng này.')).toBeInTheDocument();
  });
});
