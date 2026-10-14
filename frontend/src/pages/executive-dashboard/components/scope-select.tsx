import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';

const COMPANY = 'company';

// "Toàn công ty" or one branch, for a user with branches.access_all. Choosing a branch also makes it the
// working branch (useExecutiveScope), so the header switcher and the report links follow it.
export function ScopeSelect() {
  const scope = useExecutiveScope();
  if (!scope.canSwitch) return null;

  const value = scope.mode === 'company' ? COMPANY : (scope.branchId ?? '');
  return (
    <Select
      value={value}
      onValueChange={(next) => (next === COMPANY ? scope.selectCompany() : scope.selectBranch(next))}
    >
      <SelectTrigger aria-label="Phạm vi số liệu" title="Phạm vi số liệu" className="h-8 w-48 text-sm">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={COMPANY}>Toàn công ty</SelectItem>
        {scope.branches.map((b) => (
          <SelectItem key={b.id} value={b.id}>
            {b.code} — {b.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
