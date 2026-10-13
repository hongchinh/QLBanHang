import { useSearchParamString } from '@/lib/use-search-param-state';
import type { ReportScope } from '@/lib/report-scope';
import { useAuthStore } from '@/stores/auth-store';

// The scope of a report page lives in the URL (`scope=Company`; Branch is the default and leaves no parameter).
// A URL asking for Company is read as Branch unless the user holds branches.access_all, as the server refuses it.
export function useReportScope(): [ReportScope, (scope: ReportScope) => void] {
  const canReadCompany = useAuthStore((s) => s.hasPermission('branches.access_all'));
  const [param, setParam] = useSearchParamString('scope');
  const scope: ReportScope = canReadCompany && param === 'Company' ? 'Company' : 'Branch';
  return [scope, (next) => setParam(next === 'Company' ? next : '')];
}
