import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AppLayout } from '../app-layout';
import { useBranchContext } from '@/features/branches/use-branch-context';

vi.mock('@/features/branches/use-branch-context', () => ({ useBranchContext: vi.fn() }));
vi.mock('@/hooks/useNotificationHub', () => ({ useNotificationHub: vi.fn() }));
vi.mock('../header/app-header', () => ({ AppHeader: () => <header>header</header> }));

describe('AppLayout grid', () => {
  it('caps the content row at the viewport so the sidebar can scroll', () => {
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

    const grid = screen.getByText('header').parentElement!;
    expect(grid).toHaveClass('grid-rows-[4rem_minmax(0,1fr)]');
    expect(grid).not.toHaveClass('grid-rows-[4rem_1fr]');
  });
});
