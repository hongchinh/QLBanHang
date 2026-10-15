import { useLocation, useNavigate } from 'react-router-dom';
import { badgeVariants } from '@/components/ui/badge-variants';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useMyBranches } from '@/features/branches/hooks';
import { useSwitchWorkingBranch } from '@/features/branches/use-switch-working-branch';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';
import { cn } from '@/lib/utils';

// Voucher detail/new pages belong to one branch; after a switch go back to the list.
const VOUCHER_PAGE = /^\/(stock-in|stock-out)\/[^/]+/;

export function HeaderBranchSwitcher() {
  const { data: myBranches } = useMyBranches();
  const userId = useAuthStore((s) => s.user?.id);
  const workingBranchId = useBranchStore((s) => s.workingBranchId);
  const switchWorkingBranch = useSwitchWorkingBranch();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  if (!myBranches?.canSwitch || !userId) return null;

  const handleChange = async (branchId: string) => {
    if (branchId === workingBranchId) return;
    await switchWorkingBranch(branchId);
    const voucherPage = VOUCHER_PAGE.exec(pathname);
    if (voucherPage) navigate(`/${voucherPage[1]}`);
  };

  const selectedId = workingBranchId ?? myBranches.workingBranchId;
  const selected = myBranches.branches.find((b) => b.id === selectedId);
  // The trigger clips long text, so the full label (with the demo marker) is also its tooltip.
  const selectedLabel = selected
    ? `${selected.code} — ${selected.name}${selected.isDemo ? ' (demo)' : ''}`
    : undefined;

  return (
    <Select
      value={selectedId}
      onValueChange={(id) => void handleChange(id)}
    >
      <SelectTrigger
        aria-label={selected?.isDemo ? 'Chi nhánh làm việc (demo)' : 'Chi nhánh làm việc'}
        title={selectedLabel ?? 'Chi nhánh làm việc'}
        className="h-9 min-w-0 flex-1 gap-2 border-transparent bg-white text-sm text-foreground md:w-56 md:flex-none"
      >
        {/* The trigger clamps its direct child spans ([&>span]:line-clamp-1), so the label truncates. The badge is
            a sibling so it is never clipped with the label, and a span because a button holds phrasing content only;
            it forces its own display and overflow back. Radix drops className on SelectValue, so ml-auto on the
            badge keeps it beside the chevron. */}
        <SelectValue>{selected ? `${selected.code} — ${selected.name}` : undefined}</SelectValue>
        {selected?.isDemo && (
          <span
            className={cn(
              badgeVariants({ variant: 'info' }),
              'ml-auto shrink-0 px-2 py-0 text-xs !inline-flex !overflow-visible',
            )}
          >
            Demo
          </span>
        )}
      </SelectTrigger>
      <SelectContent>
        {myBranches.branches.map((b) => (
          <SelectItem key={b.id} value={b.id}>
            {b.code} — {b.name}{b.isDemo ? ' (demo)' : ''}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
