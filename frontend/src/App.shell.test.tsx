import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { App } from './App';
import type { PushState } from '@/hooks/usePushNotification';

// What the push hook reports; a test sets it before rendering.
const push = vi.hoisted(() => ({ state: 'idle' as PushState }));
// Makes the boot (outside the route error boundary) or the routed page (inside it) throw while rendering.
const crash = vi.hoisted(() => ({ boot: false, page: false }));
// Keeps AuthInit on its loader, as while the session boots.
const boot = vi.hoisted(() => ({ pending: false }));
// Whether the service worker reports a new version (the update popup).
const sw = vi.hoisted(() => ({ needRefresh: false }));

vi.mock('@/hooks/useBeforeInstallPrompt', () => ({
  useBeforeInstallPrompt: () => ({ canShow: false, install: vi.fn(), dismiss: vi.fn() }),
}));
vi.mock('@/hooks/usePushNotification', () => ({
  usePushNotification: () => ({ state: push.state, subscribe: vi.fn() }),
}));
vi.mock('virtual:pwa-register/react', () => ({
  useRegisterSW: () => ({
    needRefresh: [sw.needRefresh, vi.fn()],
    offlineReady: [false, vi.fn()],
    updateServiceWorker: vi.fn(),
  }),
}));
vi.mock('@/routes/auth-init', () => ({
  AuthInit: ({ children }: { children: React.ReactNode }) => {
    if (crash.boot) throw new Error('boot failed');
    if (boot.pending) return <div data-testid="boot-loader">Đang tải...</div>;
    return <>{children}</>;
  },
}));
vi.mock('@/routes/protected-route', () => ({
  ProtectedRoute: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock('@/pages/error-pages', () => ({
  NotFoundPage: () => {
    if (crash.page) throw new Error('page failed');
    return <div>not-found</div>;
  },
  ForbiddenPage: () => <div>forbidden</div>,
}));

// The pixels need a browser; here only the structure: banner and routes live in the same window-high column.
describe('App shell', () => {
  afterEach(() => {
    window.history.pushState({}, '', '/');
    localStorage.clear();
    push.state = 'idle';
    sw.needRefresh = false;
  });

  // Fixed to the viewport, outside the shell's padding: keep it above the iPhone home indicator.
  it('keeps the update popup above the home indicator', () => {
    sw.needRefresh = true;
    window.history.pushState({}, '', '/404');
    render(<App />);
    const popup = screen.getByText('Có phiên bản mới').parentElement!;
    expect(popup).toHaveClass('bottom-[max(1rem,env(safe-area-inset-bottom))]');
    expect(popup).not.toHaveClass('bottom-4');
  });

  it('puts the push banner and the routed page in one shell', () => {
    window.history.pushState({}, '', '/404');
    render(<App />);

    const shell = screen.getByTestId('app-shell');
    expect(shell).toContainElement(screen.getByText(/Bật thông báo để nhận cập nhật/));
    expect(shell).toContainElement(screen.getByText('not-found'));
  });

  // AuthInit's loader fills the room under the banner with flex-1: it has to be a child of the shell's content box.
  it('shows the boot loader inside the shell content box, under the banner', () => {
    boot.pending = true;
    try {
      window.history.pushState({}, '', '/404');
      render(<App />);

      const shell = screen.getByTestId('app-shell');
      expect(screen.getByTestId('boot-loader').parentElement).toBe(shell.lastElementChild);
      expect(shell.firstElementChild).toContainElement(screen.getByRole('button', { name: 'Bật thông báo' }));
    } finally {
      boot.pending = false;
    }
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

  // notificationclick posts NAVIGATE to the open window; the app moves inside the SPA, no reload.
  it('follows a NAVIGATE message from the service worker', () => {
    const original = Object.getOwnPropertyDescriptor(navigator, 'serviceWorker');
    const container = new EventTarget();
    Object.defineProperty(navigator, 'serviceWorker', { value: container, configurable: true });
    try {
      window.history.pushState({}, '', '/404');
      render(<App />);
      act(() => {
        container.dispatchEvent(new MessageEvent('message', { data: { type: 'NAVIGATE', url: '/403' } }));
      });
      expect(window.location.pathname).toBe('/403');
      expect(screen.getByText('forbidden')).toBeInTheDocument();
    } finally {
      if (original) Object.defineProperty(navigator, 'serviceWorker', original);
      else delete (navigator as { serviceWorker?: unknown }).serviceWorker;
    }
  });

  describe('error fallback', () => {
    // A render error a boundary catches still reaches window as an "error" event. Cancelling it tells React and
    // jsdom that it was expected, so neither prints it; the boundary's own report goes to console.error.
    const expectError = (event: Event) => event.preventDefault();

    beforeEach(() => {
      window.addEventListener('error', expectError);
      vi.spyOn(console, 'error').mockImplementation(() => {});
    });

    afterEach(() => {
      window.removeEventListener('error', expectError);
      vi.mocked(console.error).mockRestore();
      crash.boot = false;
      crash.page = false;
    });

    // The route boundary sits inside the shell's content box, under the banner. A fallback that claimed the
    // viewport height there would be taller than its box by the height of the banner, and scroll.
    it('fills the shell when a routed page throws', () => {
      crash.page = true;
      window.history.pushState({}, '', '/404');
      render(<App />);

      const fallback = screen.getByRole('heading', { name: 'Đã có lỗi xảy ra' }).parentElement;
      expect(screen.getByTestId('app-shell')).toContainElement(fallback);
      expect(fallback).toHaveClass('flex-1');
      expect(fallback).not.toHaveClass('min-h-screen');
    });

    it('centres its content in the box it fills', () => {
      crash.page = true;
      window.history.pushState({}, '', '/404');
      render(<App />);

      const fallback = screen.getByRole('heading', { name: 'Đã có lỗi xảy ra' }).parentElement;
      expect(fallback).toHaveClass('flex', 'flex-col', 'items-center', 'justify-center');
    });

    // The outer boundary replaces the whole app, shell included: nothing is left to give its fallback a height.
    it('claims the viewport height itself when the app throws outside the routes', () => {
      crash.boot = true;
      render(<App />);

      const fallback = screen.getByRole('heading', { name: 'Đã có lỗi xảy ra' }).parentElement;
      expect(screen.queryByTestId('app-shell')).not.toBeInTheDocument();
      expect(fallback).toHaveClass('min-h-screen');
      expect(fallback).not.toHaveClass('flex-1');
    });
  });
});
