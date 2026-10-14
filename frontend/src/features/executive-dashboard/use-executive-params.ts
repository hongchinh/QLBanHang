import { useCallback } from 'react';
import { useDashboardParams } from '@/features/dashboard/use-dashboard-params';
import type { RangePreset } from '@/lib/date-range-presets';
import { useSearchParamString } from '@/lib/use-search-param-state';
import { periodError } from './period';
import { EXECUTIVE_DOMAINS } from './types';
import type { ExecutiveScopeMode, ExecutiveTab } from './types';

const TABS: readonly string[] = ['overview', ...EXECUTIVE_DOMAINS];
const isAcceptedPeriod = (from: string, to: string) => periodError(from, to) === null;

// URL state of the executive dashboard: the period (from, to), the tab and the scope.
// The defaults are not written to the URL: tab=overview and scope=company. A URL period the API would refuse
// (periodError) gives way to the default period, so the page never asks for it.
export function useExecutiveParams(): {
  from: string;
  to: string;
  tab: ExecutiveTab;
  scopeParam: ExecutiveScopeMode;
  setRange(from: string, to: string): void;
  setPreset(preset: RangePreset): void;
  setTab(tab: ExecutiveTab): void;
  setScopeParam(scope: ExecutiveScopeMode): void;
} {
  const { from, to, setRange, setPreset } = useDashboardParams({ isValid: isAcceptedPeriod });
  const [tabParam, setTabParam] = useSearchParamString('tab');
  const [scope, setScope] = useSearchParamString('scope');

  const tab: ExecutiveTab = TABS.includes(tabParam) ? (tabParam as ExecutiveTab) : 'overview';
  const scopeParam: ExecutiveScopeMode = scope === 'branch' ? 'branch' : 'company';

  const setTab = useCallback((next: ExecutiveTab) => setTabParam(next === 'overview' ? '' : next), [setTabParam]);
  const setScopeParam = useCallback(
    (next: ExecutiveScopeMode) => setScope(next === 'branch' ? 'branch' : ''),
    [setScope],
  );

  return { from, to, tab, scopeParam, setRange, setPreset, setTab, setScopeParam };
}
