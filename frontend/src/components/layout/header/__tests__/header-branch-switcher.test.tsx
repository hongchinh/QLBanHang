import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HeaderBranchSwitcher } from '../header-branch-switcher';
import { useMyBranches } from '@/features/branches/hooks';
import type { MyBranches } from '@/features/branches/types';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';

vi.mock('@/features/branches/hooks', () => ({ useMyBranches: vi.fn() }));

const MY_BRANCHES: MyBranches = {
  defaultBranchId: 'b1',
  workingBranchId: 'b1',
  canSwitch: true,
  branches: [
    { id: 'b1', code: 'CN01', name: 'Chi nhánh chính' },
    { id: 'b2', code: 'CN02', name: 'Chi nhánh 2' },
  ],
};

function mockMyBranches(data: MyBranches) {
  vi.mocked(useMyBranches).mockReturnValue({ data } as ReturnType<typeof useMyBranches>);
}

function LocationDisplay() {
  return <div data-testid="location">{useLocation().pathname}</div>;
}

function renderSwitcher(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const resetSpy = vi.spyOn(queryClient, 'resetQueries');
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="*"
            element={
              <>
                <HeaderBranchSwitcher />
                <LocationDisplay />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { resetSpy };
}

describe('HeaderBranchSwitcher', () => {
  beforeEach(() => {
    localStorage.clear();
    act(() => {
      useAuthStore.setState({
        accessToken: 'token',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: {
          id: 'u1',
          username: 'admin',
          email: 'admin@example.com',
          fullName: 'Admin',
          roles: ['ADMIN'],
          permissions: ['branches.access_all'],
        },
      });
      useBranchStore.setState({ workingBranchId: 'b1' });
    });
  });

  afterEach(() => {
    act(() => {
      useAuthStore.getState().logout();
      useBranchStore.getState().clear();
    });
    vi.mocked(useMyBranches).mockReset();
  });

  it('renders nothing when user cannot switch', () => {
    mockMyBranches({ ...MY_BRANCHES, canSwitch: false, branches: [MY_BRANCHES.branches[0]] });

    renderSwitcher('/');

    expect(screen.queryByRole('combobox', { name: 'Chi nhánh làm việc' })).not.toBeInTheDocument();
  });

  it('lists branches and switches working branch', async () => {
    mockMyBranches(MY_BRANCHES);
    const user = userEvent.setup();
    const { resetSpy } = renderSwitcher('/stock-in/123');

    const trigger = screen.getByRole('combobox', { name: 'Chi nhánh làm việc' });
    expect(trigger).toHaveTextContent('CN01 — Chi nhánh chính');

    await user.click(trigger);
    await user.click(await screen.findByRole('option', { name: 'CN02 — Chi nhánh 2' }));

    await waitFor(() => expect(useBranchStore.getState().workingBranchId).toBe('b2'));
    expect(localStorage.getItem('working_branch_u1')).toBe('b2');
    await waitFor(() => expect(resetSpy).toHaveBeenCalled());
    // Its own /me/branches query is kept, so the switcher does not disappear.
    const { predicate } = resetSpy.mock.calls[0][0] as { predicate: (q: { queryKey: unknown[] }) => boolean };
    expect(predicate({ queryKey: ['branches', 'me'] })).toBe(false);
    expect(predicate({ queryKey: ['stock-vouchers', 'list'] })).toBe(true);
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/stock-in$/));
  });
});
