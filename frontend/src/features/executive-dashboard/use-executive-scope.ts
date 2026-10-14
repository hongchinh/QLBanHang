import { useCallback } from 'react';
import { useMyBranches } from '@/features/branches/hooks';
import type { Branch } from '@/features/branches/types';
import { useSwitchWorkingBranch } from '@/features/branches/use-switch-working-branch';
import { useBranchStore } from '@/stores/branch-store';
import type { ExecutiveScopeMode } from './types';
import { useExecutiveParams } from './use-executive-params';

const NO_BRANCHES: Branch[] = [];

// Whole company or one branch. A user with branches.access_all chooses (URL parameter `scope`); choosing a
// branch makes it the working branch, so the header switcher, the dashboard and every report link show the
// same branch. Anyone else always sees the own branch: no branch id is sent and the server applies it.
export function useExecutiveScope(): {
  canSwitch: boolean;
  mode: ExecutiveScopeMode;
  // What the API receives.
  branchId: string | undefined;
  workingBranch: Branch | undefined;
  branches: Branch[];
  selectCompany(): void;
  selectBranch(branchId: string): void;
} {
  const { data: myBranches } = useMyBranches();
  const workingBranchId = useBranchStore((s) => s.workingBranchId);
  const switchWorkingBranch = useSwitchWorkingBranch();
  const { scopeParam, setScopeParam } = useExecutiveParams();

  const canSwitch = myBranches?.canSwitch ?? false;
  const branches = myBranches?.branches ?? NO_BRANCHES;
  const workingBranch = branches.find((b) => b.id === workingBranchId);
  const mode: ExecutiveScopeMode = canSwitch ? scopeParam : 'branch';
  const branchId = canSwitch && mode === 'branch' ? (workingBranchId ?? undefined) : undefined;

  const selectCompany = useCallback(() => {
    if (canSwitch) setScopeParam('company');
  }, [canSwitch, setScopeParam]);

  const selectBranch = useCallback(
    (id: string) => {
      if (!canSwitch) return;
      // The URL first, then the switch, in the same tick. The switch is never awaited: it remounts the page
      // (the outlet is keyed by the working branch), and a continuation would run in the unmounted instance
      // after the new one had already fetched in company scope.
      setScopeParam('branch');
      if (id !== workingBranchId) void switchWorkingBranch(id);
    },
    [canSwitch, setScopeParam, switchWorkingBranch, workingBranchId],
  );

  return { canSwitch, mode, branchId, workingBranch, branches, selectCompany, selectBranch };
}
