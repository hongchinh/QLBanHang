import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { REPORT_SCOPE_LABELS, type ReportScope } from '@/lib/report-scope';
import { useAuthStore } from '@/stores/auth-store';

interface Props {
  id: string;
  value: ReportScope;
  onChange: (scope: ReportScope) => void;
}

const SCOPES = Object.keys(REPORT_SCOPE_LABELS) as ReportScope[];

// Phạm vi of a report. Only holders of branches.access_all may read the whole company, so everybody else
// gets no select at all.
export function ReportScopeSelect({ id, value, onChange }: Props) {
  const canReadCompany = useAuthStore((s) => s.hasPermission('branches.access_all'));
  if (!canReadCompany) return null;

  return (
    <div className="space-y-1">
      <Label htmlFor={id}>Phạm vi</Label>
      <Select value={value} onValueChange={(v) => onChange(v as ReportScope)}>
        <SelectTrigger id={id}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {SCOPES.map((scope) => (
            <SelectItem key={scope} value={scope}>
              {REPORT_SCOPE_LABELS[scope]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
