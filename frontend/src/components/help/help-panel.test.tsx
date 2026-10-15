import { MemoryRouter, Link } from 'react-router-dom';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HelpPanel } from './help-panel';
import { fixtureDocs } from './test-fixtures';
import { buildHelpIndex } from '@/features/help/registry';
import { loadHelpIndex, useHelpIndex, useHelpLoadFailed } from '@/features/help/load-help';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';

vi.mock('@/features/help/load-help', () => ({
  useHelpIndex: vi.fn(),
  useHelpLoadFailed: vi.fn(() => false),
  loadHelpIndex: vi.fn(() => new Promise(() => {})),
}));

const permissions = ['stock_out.view', 'stock_out.create', 'receipts.view', 'receipts.create', 'products.view'];

function renderPanel(path = '/stock-out/new') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <input aria-label="ô trang" />
      <input aria-label="ô khác" />
      <Link to="/products">sang hàng hóa</Link>
      <Link to="/stock-out/new">về xuất kho</Link>
      <button type="button" data-help-button>?</button>
      <HelpPanel />
    </MemoryRouter>,
  );
}

function setUser(perms: string[]) {
  act(() => {
    useAuthStore.setState({
      accessToken: 'token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: 'u1', username: 'u', email: 'u@x.vn', fullName: 'U', roles: [], permissions: perms },
    });
  });
}

const openPanel = () => act(() => useUiStore.getState().openHelp());
const closePanel = () => act(() => useUiStore.getState().closeHelp());

// A screen of the given width: answers min-width / max-width queries and the reduced-motion one.
function stubScreen(width: number, reducedMotion = false) {
  vi.stubGlobal('matchMedia', (query: string) => {
    const min = /min-width:\s*([\d.]+)px/.exec(query);
    const max = /max-width:\s*([\d.]+)px/.exec(query);
    const matches = query.includes('prefers-reduced-motion')
      ? reducedMotion
      : (!min || width >= Number(min[1])) && (!max || width <= Number(max[1]));
    return { matches, media: query, addEventListener: () => {}, removeEventListener: () => {} };
  });
}

describe('HelpPanel', () => {
  beforeEach(() => {
    vi.mocked(useHelpIndex).mockReturnValue(buildHelpIndex(fixtureDocs));
    vi.mocked(useHelpLoadFailed).mockReturnValue(false);
    vi.mocked(loadHelpIndex).mockClear();
    setUser(permissions);
    act(() => useUiStore.setState({ helpOpen: false }));
  });

  afterEach(() => {
    act(() => {
      useAuthStore.getState().logout();
      useUiStore.setState({ helpOpen: false });
    });
  });

  it('renders nothing while closed', () => {
    renderPanel();
    expect(screen.queryByTestId('help-panel')).not.toBeInTheDocument();
  });

  it('shows the doc for the route and focuses the title', () => {
    act(() => useUiStore.setState({ helpOpen: true }));
    renderPanel();
    const heading = screen.getByRole('heading', { name: 'Phiếu xuất kho' });
    expect(screen.getByText('Ghi nhận hàng xuất khỏi kho.')).toBeInTheDocument();
    expect(screen.getByText('Cập nhật 15/10/2026')).toBeInTheDocument();
    expect(heading).toHaveFocus();
  });

  it('gives the focus back to the previous element on close', () => {
    renderPanel();
    act(() => screen.getByLabelText('ô trang').focus());
    openPanel();
    const panel = screen.getByTestId('help-panel');
    fireEvent.click(within(panel).getByRole('button', { name: 'Đóng hướng dẫn' }));
    expect(useUiStore.getState().helpOpen).toBe(false);
    expect(screen.getByLabelText('ô trang')).toHaveFocus();
  });

  it('keeps the focus in another field the user clicked into', () => {
    renderPanel();
    act(() => screen.getByLabelText('ô trang').focus());
    openPanel();
    act(() => screen.getByLabelText('ô khác').focus());
    act(() => useUiStore.getState().closeHelp());
    expect(screen.getByLabelText('ô khác')).toHaveFocus();
  });

  it('closes on Esc inside the panel but not outside it', () => {
    renderPanel();
    openPanel();
    fireEvent.keyDown(screen.getByLabelText('ô trang'), { key: 'Escape' });
    expect(useUiStore.getState().helpOpen).toBe(true);
    fireEvent.keyDown(screen.getByRole('heading', { name: 'Phiếu xuất kho' }), { key: 'Escape' });
    expect(useUiStore.getState().helpOpen).toBe(false);
  });

  it('opens a related doc and goes back', () => {
    renderPanel();
    openPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Phiếu thu' }));
    expect(screen.getByRole('heading', { name: 'Phiếu thu' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Quay lại' }));
    expect(screen.getByRole('heading', { name: 'Phiếu xuất kho' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Quay lại' })).not.toBeInTheDocument();
  });

  it('follows the route and drops the picked doc when the route changes', () => {
    renderPanel();
    openPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Phiếu thu' }));
    fireEvent.click(screen.getByRole('link', { name: 'sang hàng hóa' }));
    expect(useUiStore.getState().helpOpen).toBe(true);
    expect(screen.getByText('Chưa có hướng dẫn cho màn hình này.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Quay lại' })).not.toBeInTheDocument();
  });

  it('does not bring the picked doc back when returning to the same route', () => {
    renderPanel();
    openPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Phiếu thu' }));
    fireEvent.click(screen.getByRole('link', { name: 'sang hàng hóa' }));
    fireEvent.click(screen.getByRole('link', { name: 'về xuất kho' }));
    expect(screen.getByRole('heading', { name: 'Phiếu xuất kho' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Quay lại' })).not.toBeInTheDocument();
  });

  it('moves the focus to the title after opening a related doc and after going back', () => {
    renderPanel();
    openPanel();
    const related = screen.getByRole('button', { name: 'Phiếu thu' });
    act(() => related.focus());
    fireEvent.click(related);
    expect(screen.getByRole('heading', { name: 'Phiếu thu' })).toHaveFocus();
    const back = screen.getByRole('button', { name: 'Quay lại' });
    act(() => back.focus());
    fireEvent.click(back);
    expect(screen.getByRole('heading', { name: 'Phiếu xuất kho' })).toHaveFocus();
  });

  it('hides related docs the user cannot open', () => {
    setUser(['stock_out.view', 'stock_out.create']);
    renderPanel();
    openPanel();
    expect(screen.queryByRole('button', { name: 'Phiếu thu' })).not.toBeInTheDocument();
  });

  it('lists docs of the same group when the screen has no doc', () => {
    renderPanel('/products');
    openPanel();
    expect(screen.getByText('Chưa có hướng dẫn cho màn hình này.')).toBeInTheDocument();
    expect(screen.getByText('Hướng dẫn cùng nhóm:')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Nhóm hàng hóa' })).toBeInTheDocument();
  });

  it('shows a loading text while the index is not loaded', () => {
    vi.mocked(useHelpIndex).mockReturnValue(null);
    renderPanel();
    openPanel();
    expect(screen.getByText('Đang tải hướng dẫn…')).toBeInTheDocument();
  });

  it('shows the failure text with a retry button', () => {
    vi.mocked(useHelpIndex).mockReturnValue(null);
    vi.mocked(useHelpLoadFailed).mockReturnValue(true);
    renderPanel();
    openPanel();
    expect(screen.getByText('Không tải được hướng dẫn. Kiểm tra kết nối mạng rồi thử lại.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(loadHelpIndex).toHaveBeenCalledTimes(1);
  });

  it('has the responsive layout classes', () => {
    renderPanel();
    openPanel();
    expect(screen.getByTestId('help-panel')).toHaveClass(
      'fixed', 'max-h-[85dvh]', 'md:w-[400px]', 'md:absolute', 'min-[1600px]:static', 'min-[1600px]:col-start-3',
    );
    expect(screen.getByTestId('help-panel-body')).toHaveClass('min-h-0', 'flex-1', 'overflow-y-auto');
    const backdrop = screen.getAllByRole('button', { name: 'Đóng hướng dẫn' }).find((b) => b.className.includes('fixed'));
    expect(backdrop).toHaveClass('md:hidden');
  });
});

describe('HelpPanel closing', () => {
  beforeEach(() => {
    vi.mocked(useHelpIndex).mockReturnValue(buildHelpIndex(fixtureDocs));
    vi.mocked(useHelpLoadFailed).mockReturnValue(false);
    setUser(permissions);
    act(() => useUiStore.setState({ helpOpen: false }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
    act(() => {
      useAuthStore.getState().logout();
      useUiStore.setState({ helpOpen: false });
    });
  });

  it('closes on a press outside the panel on a laptop screen', () => {
    stubScreen(1280, true);
    renderPanel();
    openPanel();
    fireEvent.pointerDown(screen.getByTestId('help-panel-body'));
    expect(useUiStore.getState().helpOpen).toBe(true);
    fireEvent.pointerDown(screen.getByRole('button', { name: '?' }));
    expect(useUiStore.getState().helpOpen).toBe(true);
    fireEvent.pointerDown(screen.getByLabelText('ô trang'));
    expect(useUiStore.getState().helpOpen).toBe(false);
  });

  it('ignores presses inside a dialog over the page', () => {
    stubScreen(1280, true);
    renderPanel();
    openPanel();
    const dialog = document.createElement('div');
    dialog.setAttribute('role', 'dialog');
    document.body.append(dialog);
    fireEvent.pointerDown(dialog);
    expect(useUiStore.getState().helpOpen).toBe(true);
    dialog.remove();
  });

  it('stays open on a press outside on a wide screen, where it is a column', () => {
    stubScreen(1600);
    renderPanel();
    openPanel();
    fireEvent.pointerDown(screen.getByLabelText('ô trang'));
    expect(useUiStore.getState().helpOpen).toBe(true);
  });

  it('shrinks toward the help button, then leaves and pings the button', () => {
    stubScreen(1280);
    renderPanel();
    openPanel();
    const panel = screen.getByTestId('help-panel');
    expect(panel).toHaveClass('animate-in', 'zoom-in-[0.04]');
    closePanel();
    expect(panel).toBeInTheDocument();
    expect(panel).toHaveClass('animate-out', 'zoom-out-[0.04]', 'pointer-events-none');
    expect(panel.style.transformOrigin).not.toBe('');
    fireEvent.animationEnd(panel);
    expect(screen.queryByTestId('help-panel')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '?' })).toHaveClass('help-ping');
  });

  it('leaves after a timeout when the animation end never comes', () => {
    vi.useFakeTimers();
    stubScreen(1280);
    renderPanel();
    openPanel();
    closePanel();
    expect(screen.getByTestId('help-panel')).toBeInTheDocument();
    act(() => vi.advanceTimersByTime(1000));
    expect(screen.queryByTestId('help-panel')).not.toBeInTheDocument();
  });

  it('opens again fresh when reopened while shrinking', () => {
    stubScreen(1280);
    renderPanel();
    openPanel();
    closePanel();
    openPanel();
    const panel = screen.getByTestId('help-panel');
    expect(panel).toHaveClass('animate-in');
    expect(panel).not.toHaveClass('pointer-events-none');
    expect(screen.getByRole('heading', { name: 'Phiếu xuất kho' })).toHaveFocus();
  });

  it('slides down on a phone', () => {
    stubScreen(390);
    renderPanel();
    openPanel();
    closePanel();
    expect(screen.getByTestId('help-panel')).toHaveClass('animate-out', 'slide-out-to-bottom');
  });

  it('leaves at once when the user asks for reduced motion', () => {
    stubScreen(1280, true);
    renderPanel();
    openPanel();
    expect(screen.getByTestId('help-panel')).not.toHaveClass('animate-in');
    closePanel();
    expect(screen.queryByTestId('help-panel')).not.toBeInTheDocument();
  });
});
