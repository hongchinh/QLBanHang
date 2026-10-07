import { beforeEach, describe, expect, it } from 'vitest';
import { useBranchStore } from './branch-store';

describe('useBranchStore', () => {
  beforeEach(() => {
    localStorage.clear();
    useBranchStore.getState().clear();
  });

  it('restore returns stored id when allowed', () => {
    localStorage.setItem('working_branch_u1', 'b2');

    const id = useBranchStore.getState().restore('u1', ['b1', 'b2'], 'b1');

    expect(id).toBe('b2');
    expect(useBranchStore.getState().workingBranchId).toBe('b2');
  });

  it('restore falls back to default when stored id not allowed', () => {
    localStorage.setItem('working_branch_u1', 'b9');

    const id = useBranchStore.getState().restore('u1', ['b1', 'b2'], 'b1');

    expect(id).toBe('b1');
    expect(useBranchStore.getState().workingBranchId).toBe('b1');
    expect(localStorage.getItem('working_branch_u1')).toBeNull();
  });

  it('setWorkingBranch persists per user', () => {
    useBranchStore.getState().setWorkingBranch('u1', 'b2');
    useBranchStore.getState().setWorkingBranch('u2', 'b3');

    expect(useBranchStore.getState().workingBranchId).toBe('b3');
    expect(localStorage.getItem('working_branch_u1')).toBe('b2');
    expect(localStorage.getItem('working_branch_u2')).toBe('b3');
    expect(useBranchStore.getState().restore('u1', ['b1', 'b2'], 'b1')).toBe('b2');
  });

  it('clear resets state', () => {
    useBranchStore.getState().setWorkingBranch('u1', 'b2');

    useBranchStore.getState().clear();

    expect(useBranchStore.getState().workingBranchId).toBeNull();
  });
});
