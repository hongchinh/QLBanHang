import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { act, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AppLayout } from '../app-layout';
import { useBranchContext } from '@/features/branches/use-branch-context';
import { useUiStore } from '@/stores/ui-store';

vi.mock('@/features/branches/use-branch-context', () => ({ useBranchContext: vi.fn() }));
vi.mock('@/hooks/useNotificationHub', () => ({ useNotificationHub: vi.fn() }));
vi.mock('../header/app-header', () => ({ AppHeader: () => <header>header</header> }));
vi.mock('@/features/help/load-help', () => ({
  useHelpIndex: () => null,
  useHelpLoadFailed: () => false,
  loadHelpIndex: vi.fn(),
}));

const WIDE_GRID = 'min-[1600px]:grid-cols-[auto_1fr_400px]';

function renderLayout() {
  vi.mocked(useBranchContext).mockReturnValue({ myBranches: undefined, workingBranch: null, ready: true });
  render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route path="/" element={<div>Page content</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
  return screen.getByText('header').parentElement!;
}

describe('AppLayout help panel', () => {
  beforeEach(() => {
    useUiStore.setState({ helpOpen: false });
  });

  it('keeps the grid at two columns and shows no panel while the help is closed', () => {
    const grid = renderLayout();
    expect(grid).toHaveClass('relative');
    expect(grid).not.toHaveClass(WIDE_GRID);
    expect(screen.queryByTestId('help-panel')).not.toBeInTheDocument();
  });

  it('adds the third column and renders the panel while the help is open', () => {
    useUiStore.setState({ helpOpen: true });
    const grid = renderLayout();
    expect(grid).toHaveClass(WIDE_GRID);
    expect(screen.getByTestId('help-panel')).toBeInTheDocument();
  });

  it('toggles the help on F1', () => {
    renderLayout();
    act(() => {
      window.dispatchEvent(new KeyboardEvent('keydown', { key: 'F1', cancelable: true, bubbles: true }));
    });
    expect(useUiStore.getState().helpOpen).toBe(true);
  });
});
