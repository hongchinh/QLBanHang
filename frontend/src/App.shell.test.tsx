import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { App } from './App';

vi.mock('@/hooks/useBeforeInstallPrompt', () => ({
  useBeforeInstallPrompt: () => ({ canShow: false, install: vi.fn(), dismiss: vi.fn() }),
}));
vi.mock('@/hooks/usePushNotification', () => ({
  usePushNotification: () => ({ state: 'idle', subscribe: vi.fn() }),
}));
vi.mock('@/routes/auth-init', () => ({ AuthInit: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('@/routes/protected-route', () => ({
  ProtectedRoute: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock('@/pages/error-pages', () => ({
  NotFoundPage: () => <div>not-found</div>,
  ForbiddenPage: () => <div>forbidden</div>,
}));

// The pixels need a browser; here only the structure: banner and routes live in the same window-high column.
describe('App shell', () => {
  afterEach(() => {
    window.history.pushState({}, '', '/');
    localStorage.clear();
  });

  it('puts the push banner and the routed page in one shell', () => {
    window.history.pushState({}, '', '/404');
    render(<App />);

    const shell = screen.getByTestId('app-shell');
    expect(shell).toContainElement(screen.getByText(/Bật thông báo để nhận cập nhật/));
    expect(shell).toContainElement(screen.getByText('not-found'));
  });

  it('has no banner slot once the banner was dismissed', () => {
    localStorage.setItem('push_prompt_dismissed_until', String(Date.now() + 60_000));
    window.history.pushState({}, '', '/404');
    render(<App />);

    expect(screen.getByTestId('app-shell').children).toHaveLength(1);
  });
});
