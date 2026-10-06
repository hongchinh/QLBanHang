import { useEffect, useState } from 'react';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';
import { useMyBranches } from './hooks';

// Resolves the working branch once per session: the user's stored choice when
// it is still allowed, otherwise the default branch. `ready` turns true once the
// store holds the branch (or /me/branches failed — the backend default applies),
// so pages never fetch before X-Branch-Id is set.
export function useBranchContext() {
  const userId = useAuthStore((s) => s.user?.id);
  const { data: myBranches, isError } = useMyBranches();
  const restore = useBranchStore((s) => s.restore);
  const workingBranchId = useBranchStore((s) => s.workingBranchId);
  const [restored, setRestored] = useState(false);

  useEffect(() => {
    if (!myBranches || !userId) return;
    restore(userId, myBranches.branches.map((b) => b.id), myBranches.defaultBranchId);
    setRestored(true);
  }, [myBranches, userId, restore]);

  const workingBranch = myBranches?.branches.find((b) => b.id === workingBranchId) ?? null;

  return { myBranches, workingBranch, ready: restored || isError };
}
