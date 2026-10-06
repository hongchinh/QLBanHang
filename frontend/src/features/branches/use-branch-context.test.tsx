import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useBranchStore } from '@/stores/branch-store';
import { useBranchContext } from './use-branch-context';
import { useMyBranches } from './hooks';
import type { MyBranches } from './types';

vi.mock('./hooks', () => ({ useMyBranches: vi.fn() }));

vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { user: { id: string } }) => unknown) =>
    selector({ user: { id: 'u1' } }),
}));

const MY_BRANCHES: MyBranches = {
  defaultBranchId: 'b1',
  workingBranchId: 'b1',
  canSwitch: true,
  branches: [
    { id: 'b1', code: 'CN01', name: 'Chi nhánh chính' },
    { id: 'b2', code: 'CN02', name: 'Chi nhánh 2' },
  ],
};

function mockMyBranches(data: MyBranches | undefined, isError = false) {
  vi.mocked(useMyBranches).mockReturnValue({ data, isError } as ReturnType<typeof useMyBranches>);
}

describe('useBranchContext', () => {
  beforeEach(() => {
    localStorage.clear();
    useBranchStore.getState().clear();
    vi.mocked(useMyBranches).mockReset();
  });

  it('restores the stored allowed branch', async () => {
    localStorage.setItem('working_branch_u1', 'b2');
    mockMyBranches(MY_BRANCHES);

    const { result } = renderHook(() => useBranchContext());

    await waitFor(() => expect(result.current.ready).toBe(true));
    expect(result.current.workingBranch?.id).toBe('b2');
    expect(useBranchStore.getState().workingBranchId).toBe('b2');
  });

  it('falls back to the default branch', async () => {
    localStorage.setItem('working_branch_u1', 'b-removed');
    mockMyBranches(MY_BRANCHES);

    const { result } = renderHook(() => useBranchContext());

    await waitFor(() => expect(result.current.ready).toBe(true));
    expect(result.current.workingBranch?.id).toBe('b1');
    expect(useBranchStore.getState().workingBranchId).toBe('b1');
  });

  it('ready is false until the data arrives', async () => {
    mockMyBranches(undefined);

    const { result, rerender } = renderHook(() => useBranchContext());

    expect(result.current.ready).toBe(false);
    expect(useBranchStore.getState().workingBranchId).toBeNull();

    mockMyBranches(MY_BRANCHES);
    rerender();

    await waitFor(() => expect(result.current.ready).toBe(true));
    expect(result.current.myBranches).toEqual(MY_BRANCHES);
    expect(useBranchStore.getState().workingBranchId).toBe('b1');
  });

  it('is ready when /me/branches fails (backend default applies)', () => {
    mockMyBranches(undefined, true);

    const { result } = renderHook(() => useBranchContext());

    expect(result.current.ready).toBe(true);
    expect(useBranchStore.getState().workingBranchId).toBeNull();
  });
});
