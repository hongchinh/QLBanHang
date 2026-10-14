import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { App } from './App';
import type { PushState } from '@/hooks/usePushNotification';

// What the push hook reports; a test sets it before rendering.
const push = vi.hoisted(() => ({ state: 'idle' as PushState }));

vi.mock('@/hooks/useBeforeInstallPrompt', () => ({
  useBeforeInstallPrompt: () => ({ canShow: false, install: vi.fn(), dismiss: vi.fn() }),
}));
vi.mock('@/hooks/usePushNotification', () => ({
  usePushNotification: () => ({ state: push.state, subscribe: vi.fn() }),
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
    push.state = 'idle';
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

  // In these states the prompt renders nothing. Its element is truthy all the same, so App has to ask the
  // prompt's own question before it hands a banner to the shell, or the shell keeps an empty slot.
  it.each(['granted', 'denied', 'unsupported'] as const)(
    'has no banner slot while the prompt has nothing to show (%s)',
    (state) => {
      push.state = state;
      window.history.pushState({}, '', '/404');
      render(<App />);

      expect(screen.getByTestId('app-shell').children).toHaveLength(1);
    },
  );

  it.each([
    ['idle', 'Bật thông báo'],
    ['loading', 'Đang xử lý...'],
    ['error', 'Thử lại'],
  ] as const)('keeps the banner slot while the prompt shows (%s)', (state, button) => {
    push.state = state;
    window.history.pushState({}, '', '/404');
    render(<App />);

    const shell = screen.getByTestId('app-shell');
    expect(shell.children).toHaveLength(2);
    expect(shell.firstElementChild).toContainElement(screen.getByRole('button', { name: button }));
  });
});
