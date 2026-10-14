import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { BrowserRouter, MemoryRouter, useLocation } from 'react-router-dom';
import type { Branch, MyBranches } from '@/features/branches/types';
import { useBranchStore } from '@/stores/branch-store';
import { useExecutiveScope } from './use-executive-scope';

const state: { myBranches: MyBranches | undefined } = { myBranches: undefined };
vi.mock('@/features/branches/hooks', () => ({
  useMyBranches: () => ({ data: state.myBranches }),
}));

const switchWorkingBranch = vi.fn((branchId: string): Promise<void> => {
  void branchId;
  return Promise.resolve();
});
vi.mock('@/features/branches/use-switch-working-branch', () => ({
  useSwitchWorkingBranch: () => switchWorkingBranch,
}));

const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const everyBranch = (): MyBranches => ({
  defaultBranchId: 'b1', workingBranchId: 'b1', canSwitch: true, branches: [B1, B2],
});
const ownBranch = (): MyBranches => ({
  defaultBranchId: 'b1', workingBranchId: 'b1', canSwitch: false, branches: [B1],
});

function setup(url = '/dashboard/executive') {
  function wrapper({ children }: { children: React.ReactNode }) {
    return <MemoryRouter initialEntries={[url]}>{children}</MemoryRouter>;
  }
  const { result } = renderHook(
    () => {
      const scope = useExecutiveScope();
      const location = useLocation();
      return { scope, location };
    },
    { wrapper },
  );
  return {
    scope: () => result.current.scope,
    search: () => result.current.location.search,
  };
}

describe('useExecutiveScope', () => {
  beforeEach(() => {
    switchWorkingBranch.mockReset().mockResolvedValue(undefined);
    state.myBranches = everyBranch();
    useBranchStore.setState({ workingBranchId: 'b1' });
  });
  afterEach(() => useBranchStore.setState({ workingBranchId: null }));

  describe('a user of one branch', () => {
    beforeEach(() => {
      state.myBranches = ownBranch();
    });

    it('always has the branch mode and sends no branch id: the server uses the own branch', () => {
      const { scope } = setup();

      expect(scope().canSwitch).toBe(false);
      expect(scope().mode).toBe('branch');
      expect(scope().branchId).toBeUndefined();
      expect(scope().workingBranch).toEqual(B1);
      expect(scope().branches).toEqual([B1]);
    });

    it('selectBranch and selectCompany do nothing', () => {
      const { scope, search } = setup('/dashboard/executive?scope=branch');

      act(() => scope().selectBranch('b2'));
      expect(switchWorkingBranch).not.toHaveBeenCalled();
      expect(search()).toBe('?scope=branch');

      act(() => scope().selectCompany());
      expect(search()).toBe('?scope=branch');
      expect(scope().mode).toBe('branch');
      expect(scope().branchId).toBeUndefined();
    });

    it('does not write the URL from selectBranch either when it has no scope', () => {
      const { scope, search } = setup();

      act(() => scope().selectBranch('b2'));

      expect(search()).toBe('');
      expect(switchWorkingBranch).not.toHaveBeenCalled();
    });
  });

  describe('a user of every branch', () => {
    it('starts in the company mode without a branch id', () => {
      const { scope } = setup();

      expect(scope().canSwitch).toBe(true);
      expect(scope().mode).toBe('company');
      expect(scope().branchId).toBeUndefined();
      expect(scope().workingBranch).toEqual(B1);
      expect(scope().branches).toEqual([B1, B2]);
    });

    it('scope=branch sends the working branch id', () => {
      const { scope } = setup('/dashboard/executive?scope=branch');

      expect(scope().mode).toBe('branch');
      expect(scope().branchId).toBe('b1');

      // The working branch is the one of the store (what the header switcher shows).
      act(() => useBranchStore.setState({ workingBranchId: 'b2' }));
      expect(scope().branchId).toBe('b2');
      expect(scope().workingBranch).toEqual(B2);
    });

    it('selectBranch(other) writes scope=branch and switches the working branch', () => {
      const { scope, search } = setup();

      act(() => scope().selectBranch('b2'));

      expect(search()).toBe('?scope=branch');
      expect(scope().mode).toBe('branch');
      expect(switchWorkingBranch).toHaveBeenCalledTimes(1);
      expect(switchWorkingBranch).toHaveBeenCalledWith('b2');
      // The store is written by the switch only (mocked here), never by this hook.
      expect(useBranchStore.getState().workingBranchId).toBe('b1');
    });

    it('does not wait for the switch: scope=branch is written although it never resolves', () => {
      switchWorkingBranch.mockImplementation(() => new Promise<void>(() => {}));
      const { scope, search } = setup();

      act(() => scope().selectBranch('b2'));

      expect(search()).toBe('?scope=branch');
      expect(switchWorkingBranch).toHaveBeenCalledWith('b2');
    });

    it('selectBranch(working branch) only writes scope=branch', () => {
      const { scope, search } = setup();

      act(() => scope().selectBranch('b1'));

      expect(search()).toBe('?scope=branch');
      expect(scope().branchId).toBe('b1');
      expect(switchWorkingBranch).not.toHaveBeenCalled();
    });

    it('selectCompany removes the scope and keeps the working branch', () => {
      const { scope, search } = setup('/dashboard/executive?scope=branch&tab=cash');

      act(() => scope().selectCompany());

      expect(search()).toBe('?tab=cash');
      expect(scope().mode).toBe('company');
      expect(scope().branchId).toBeUndefined();
      expect(scope().workingBranch).toEqual(B1);
      expect(switchWorkingBranch).not.toHaveBeenCalled();
      expect(useBranchStore.getState().workingBranchId).toBe('b1');
    });
  });

  // The switch remounts the page (the outlet is keyed by the working branch), so the URL must already
  // carry the scope when it starts. A BrowserRouter writes the address synchronously, which shows the order.
  describe('order of the URL write and the switch', () => {
    function browserWrapper({ children }: { children: React.ReactNode }) {
      return <BrowserRouter>{children}</BrowserRouter>;
    }
    beforeEach(() => window.history.replaceState({}, '', '/dashboard/executive'));
    afterEach(() => window.history.replaceState({}, '', '/'));

    it('the address has scope=branch when the switch starts', () => {
      let searchAtSwitch: string | undefined;
      switchWorkingBranch.mockImplementation(() => {
        searchAtSwitch = window.location.search;
        return new Promise<void>(() => {});
      });
      const { result } = renderHook(() => useExecutiveScope(), { wrapper: browserWrapper });

      act(() => result.current.selectBranch('b2'));

      expect(switchWorkingBranch).toHaveBeenCalledTimes(1);
      expect(searchAtSwitch).toBe('?scope=branch');
    });
  });

  it('has no branch to switch to while /me/branches is loading', () => {
    state.myBranches = undefined;
    const { scope } = setup('/dashboard/executive?scope=branch');

    expect(scope().canSwitch).toBe(false);
    expect(scope().mode).toBe('branch');
    expect(scope().branchId).toBeUndefined();
    expect(scope().workingBranch).toBeUndefined();
    expect(scope().branches).toEqual([]);
  });
});
