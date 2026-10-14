import { formatNumber } from '@/features/dashboard/format';

// Texts of the dashboard tables. Null parts are left out.

function joinParts(parts: (string | null | undefined)[], separator: string): string {
  return parts.filter((part): part is string => !!part).join(separator);
}

// "{code} – {name}"; the name alone when the code is null.
export function codeName(code: string | null | undefined, name: string | null | undefined): string {
  return joinParts([code, name], ' – ');
}

// "{branchCode} · {label}": the branch in front (company scope); the label alone without a branch code.
export function withBranch(branchCode: string | null | undefined, label: string): string {
  return joinParts([branchCode, label], ' · ');
}

// A table amount: the full number as on the report pages; "—" when there is none.
export function formatAmount(value: number | null): string {
  return value == null ? '—' : formatNumber(value);
}
