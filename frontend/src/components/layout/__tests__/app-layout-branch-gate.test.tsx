import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AppLayout } from '../app-layout';
import { useBranchContext } from '@/features/branches/use-branch-context';

vi.mock('@/features/branches/use-branch-context', () => ({ useBranchContext: vi.fn() }));
vi.mock('@/hooks/useNotificationHub', () => ({ useNotificationHub: vi.fn() }));
vi.mock('../header/app-header', () => ({ AppHeader: () => <header>header</header> }));

function mockReady(ready: boolean) {
  vi.mocked(useBranchContext).mockReturnValue({
    myBranches: undefined,
    workingBranch: null,
    ready,
  });
}

function renderLayout() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route path="/" element={<div>Page content</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('AppLayout branch gate', () => {
  beforeEach(() => {
    vi.mocked(useBranchContext).mockReset();
  });

  it('does not render the page until the working branch is ready', () => {
    mockReady(false);
    const { rerender } = renderLayout();

    expect(screen.getByText('header')).toBeInTheDocument();
    expect(screen.queryByText('Page content')).not.toBeInTheDocument();

    mockReady(true);
    rerender(
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/" element={<div>Page content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText('Page content')).toBeInTheDocument();
  });
});
