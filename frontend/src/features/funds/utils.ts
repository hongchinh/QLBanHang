import type { FundKind } from '@/features/cash-common/types';
import type { Fund } from './types';

// Pickers offer the active funds of the kind (every kind when omitted); an inactive fund stays listed while
// it is the selected value. Ordered by code.
export function selectableFunds(
  funds: readonly Fund[],
  kind?: FundKind,
  selectedId?: string | null,
): Fund[] {
  return funds
    .filter((f) => (kind === undefined || f.kind === kind) && (f.isActive || f.id === selectedId))
    .sort((a, b) => a.code.localeCompare(b.code));
}

// The branch's default fund of that kind (P18), or undefined when it has none.
export function defaultFund(funds: readonly Fund[], kind: FundKind): Fund | undefined {
  return funds.find((f) => f.kind === kind && f.isDefault);
}
