import { useQueryClient } from '@tanstack/react-query';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useMyBranches } from '@/features/branches/hooks';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';

// Voucher detail/new pages belong to one branch; after a switch go back to the list.
const VOUCHER_PAGE = /^\/(stock-in|stock-out)\/[^/]+/;

export function HeaderBranchSwitcher() {
  const { data: myBranches } = useMyBranches();
  const userId = useAuthStore((s) => s.user?.id);
  const workingBranchId = useBranchStore((s) => s.workingBranchId);
  const setWorkingBranch = useBranchStore((s) => s.setWorkingBranch);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  if (!myBranches?.canSwitch || !userId) return null;

  const handleChange = async (branchId: string) => {
    if (branchId === workingBranchId) return;
    setWorkingBranch(userId, branchId);
    if ('caches' in window) await caches.delete('api-cache');
    // resetQueries also drops inactive cached data (invalidateQueries would keep it).
    void queryClient.resetQueries();
    const voucherPage = VOUCHER_PAGE.exec(pathname);
    if (voucherPage) navigate(`/${voucherPage[1]}`);
  };

  return (
    <Select
      value={workingBranchId ?? myBranches.workingBranchId}
      onValueChange={(id) => void handleChange(id)}
    >
      <SelectTrigger
        aria-label="Chi nhánh làm việc"
        title="Chi nhánh làm việc"
        className="h-9 w-40 border-transparent bg-white text-sm text-foreground md:w-56"
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {myBranches.branches.map((b) => (
          <SelectItem key={b.id} value={b.id}>
            {b.code} — {b.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
