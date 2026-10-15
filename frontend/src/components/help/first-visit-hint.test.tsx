import { useRef } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FirstVisitHint } from './first-visit-hint';
import { fixtureDocs } from './test-fixtures';
import { buildHelpIndex } from '@/features/help/registry';
import { useHelpIndex } from '@/features/help/load-help';
import { useFirstVisitHint } from '@/features/help/use-first-visit-hint';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';

vi.mock('@/features/help/load-help', () => ({
  useHelpIndex: vi.fn(),
  useHelpLoadFailed: vi.fn(() => false),
  loadHelpIndex: vi.fn(() => new Promise(() => {})),
}));

function Harness() {
  const hint = useFirstVisitHint();
  const a = useRef<HTMLButtonElement>(null);
  return (
    <>
      {/* eslint-disable-next-line jsx-a11y/no-autofocus -- the page's own focus, which the hint must not take */}
      <input aria-label="ô trang" autoFocus />
      <input aria-label="ô khác" />
      <button ref={a}>neo</button>
      <FirstVisitHint hint={hint} desktopAnchorRef={a} mobileAnchorRef={a} />
    </>
  );
}

function renderHint(path = '/stock-out/new') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Harness />
    </MemoryRouter>,
  );
}

const TITLE = 'Lần đầu dùng Phiếu xuất kho?';
const resetUi = () =>
  useUiStore.setState({ helpOpen: false, helpHintSeen: {}, helpHintsOff: {} });

describe('FirstVisitHint', () => {
  beforeEach(() => {
    vi.mocked(useHelpIndex).mockReturnValue(buildHelpIndex(fixtureDocs));
    act(() => {
      useAuthStore.setState({
        accessToken: 'token',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'u1', username: 'u', email: 'u@x.vn', fullName: 'U', roles: [], permissions: [] },
      });
      resetUi();
    });
  });

  afterEach(() => {
    act(() => {
      useAuthStore.getState().logout();
      resetUi();
    });
  });

  it('shows the title and the step count', () => {
    renderHint();
    expect(screen.getByText(TITLE)).toBeInTheDocument();
    expect(screen.getByText(/3 bước chính/)).toBeInTheDocument();
  });

  it('does not take the focus and marks its content', () => {
    renderHint();
    expect(screen.getByLabelText('ô trang')).toHaveFocus();
    expect(screen.getByText(TITLE).closest('[data-help-hint]')).not.toBeNull();
  });

  it('survives a focus move to another field', async () => {
    renderHint();
    await userEvent.tab();
    expect(screen.getByLabelText('ô khác')).toHaveFocus();
    expect(screen.getByText(TITLE)).toBeInTheDocument();
    expect(useUiStore.getState().helpHintSeen.u1 ?? []).toEqual([]);
  });

  it('"Ẩn" hides it and marks the screen seen', async () => {
    renderHint();
    await userEvent.click(screen.getByRole('button', { name: 'Ẩn' }));
    expect(screen.queryByText(TITLE)).not.toBeInTheDocument();
    expect(useUiStore.getState().helpHintSeen.u1).toContain('stock-out.form');
  });

  it('"Xem hướng dẫn" opens the panel and marks the screen seen', async () => {
    renderHint();
    await userEvent.click(screen.getByRole('button', { name: 'Xem hướng dẫn' }));
    expect(useUiStore.getState().helpOpen).toBe(true);
    expect(useUiStore.getState().helpHintSeen.u1).toContain('stock-out.form');
  });

  it('"Không hiện gợi ý nữa" turns the hints off', async () => {
    renderHint();
    await userEvent.click(screen.getByRole('button', { name: 'Không hiện gợi ý nữa' }));
    expect(useUiStore.getState().helpHintsOff.u1).toBe(true);
    expect(screen.queryByText(TITLE)).not.toBeInTheDocument();
  });

  it('Esc hides it and marks the screen seen', async () => {
    renderHint();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByText(TITLE)).not.toBeInTheDocument();
    expect(useUiStore.getState().helpHintSeen.u1).toContain('stock-out.form');
  });

  it('a click outside hides it and marks the screen seen', async () => {
    renderHint();
    await userEvent.click(screen.getByText('neo'));
    expect(screen.queryByText(TITLE)).not.toBeInTheDocument();
    expect(useUiStore.getState().helpHintSeen.u1).toContain('stock-out.form');
  });

  it('shows nothing on a screen without help', () => {
    renderHint('/products');
    expect(screen.queryByText(/Lần đầu dùng/)).not.toBeInTheDocument();
  });

  it('shows nothing once the screen was seen', () => {
    act(() => useUiStore.setState({ helpHintSeen: { u1: ['stock-out.form'] } }));
    renderHint();
    expect(screen.queryByText(TITLE)).not.toBeInTheDocument();
  });

  it('shows nothing while the panel is open', () => {
    act(() => useUiStore.setState({ helpOpen: true }));
    renderHint();
    expect(screen.queryByText(TITLE)).not.toBeInTheDocument();
  });
});
