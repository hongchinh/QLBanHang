import { useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';
import { branchKeys } from './keys';

// Sets the working branch and drops every branch-scoped cache: the service worker API cache and
// all queries except /me/branches (not branch-scoped; it keeps the header switcher rendered).
// resetQueries also drops inactive cached data (invalidateQueries would keep it).
export function useSwitchWorkingBranch() {
  const queryClient = useQueryClient();
  const userId = useAuthStore((s) => s.user?.id);
  const setWorkingBranch = useBranchStore((s) => s.setWorkingBranch);

  return useCallback(
    async (branchId: string) => {
      if (!userId) return;
      setWorkingBranch(userId, branchId);
      if ('caches' in window) await caches.delete('api-cache');
      const meKey = JSON.stringify(branchKeys.me());
      void queryClient.resetQueries({ predicate: (q) => JSON.stringify(q.queryKey) !== meKey });
    },
    [queryClient, userId, setWorkingBranch],
  );
}
