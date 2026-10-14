import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { MemoryRouter, Outlet, useLocation } from 'react-router-dom';
import { App } from '@/App';
import type { Branch } from '@/features/branches/types';
import type { useExecutiveAccess } from '@/features/executive-dashboard/use-executive-access';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { useAuthStore } from '@/stores/auth-store';
import type { OverviewTab } from './tabs/overview-tab';
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
});
// Phase 07 registers more panels; this access keeps the expectations below true then.
const overviewOnly = (): Access => ({ sales: true, cash: true, debt: true, inventory: true, tabs: ['overview'] });

const state: { scope: Scope; access: Access } = { scope: branchScope(), access: overviewOnly() };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({ useExecutiveScope: () => state.scope }));
vi.mock('@/features/executive-dashboard/use-executive-access', () => ({ useExecutiveAccess: () => state.access }));

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
    useExecutiveLive.mockClear();
    state.scope = branchScope();
    state.access = overviewOnly();
  });

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
