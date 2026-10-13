import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Outlet } from 'react-router-dom';
import { App } from '@/App';
import { useAuthStore } from '@/stores/auth-store';
import { ExecutiveDashboardPage } from './executive-dashboard-page';

vi.mock('@/hooks/useBeforeInstallPrompt', () => ({
  useBeforeInstallPrompt: () => ({ canShow: false, install: vi.fn(), dismiss: vi.fn() }),
}));
vi.mock('@/hooks/usePushNotification', () => ({
  usePushNotification: () => ({ state: 'unsupported', subscribe: vi.fn() }),
}));
vi.mock('@/routes/auth-init', () => ({ AuthInit: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('@/components/layout/app-layout', () => ({ AppLayout: () => <Outlet /> }));

function signIn(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 'token',
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

describe('ExecutiveDashboardPage', () => {
  it('renders the heading', () => {
    render(<ExecutiveDashboardPage />);
    expect(screen.getByRole('heading', { level: 1, name: 'Điều hành' })).toBeInTheDocument();
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
